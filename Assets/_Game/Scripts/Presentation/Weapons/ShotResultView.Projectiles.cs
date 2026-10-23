using System.Collections.Generic;
using AFPS.NetCode.Weapons;
using AFPS.Simulation.Weapons;
using UnityEngine;

namespace AFPS.Presentation.Weapons
{
    public sealed partial class ShotResultView
    {
        // 数量、超时和去重窗口仅约束表现；未确认射击不能永久占用代理。
        public const int MaximumProjectileCount = 64;
        public const float PredictionTimeoutSeconds = 1f;
        private const int CompletedProjectileCapacity = 128;
        [SerializeField] private PredictedProjectileVisual projectileVisualPrefab;

        private sealed class ProjectileEntry
        {
            public PredictedProjectileVisual View;
            public bool Confirmed;
            public ulong Order;
        }

        private readonly Dictionary<(uint Entity, uint Shot), ProjectileEntry> projectiles =
            new Dictionary<(uint, uint), ProjectileEntry>();
        private readonly Queue<PredictedProjectileVisual> projectilePool = new Queue<PredictedProjectileVisual>();
        private readonly List<(uint Entity, uint Shot)> expiredProjectiles = new List<(uint, uint)>();
        private readonly HashSet<(uint Entity, uint Shot)> completedProjectiles = new HashSet<(uint, uint)>();
        private readonly Queue<(uint Entity, uint Shot)> completedProjectileOrder = new Queue<(uint, uint)>();
        private ulong projectileOrder;

        public int ActiveProjectileCount => projectiles.Count;
        public ulong ConfirmedProjectileLaunchCount { get; private set; }
        public ulong ConfirmedProjectileImpactCount { get; private set; }
        public int CreatedProjectileVisualCount { get; private set; }

        public bool TryGetProjectilePosition(uint entityId, uint shotSequence, out Vector3 position)
        {
            if (projectiles.TryGetValue((entityId, shotSequence), out var entry))
            {
                position = entry.View.transform.position;
                return true;
            }
            position = default;
            return false;
        }

        /// <summary>本地射击通过预测后创建代理；参数来自共用武器规则和预测玩家状态。</summary>
        public bool PredictProjectile(in AuthoritativeShot shot)
        {
            var key = (shot.EntityId, shot.ShotSequence);
            if (!IsVisualProjectile(shot) || projectiles.ContainsKey(key) || completedProjectiles.Contains(key))
            {
                return false;
            }
            CreateProjectile(key, shot, false, 0f);
            return true;
        }

        private void ConsumeProjectile(in AuthoritativeShotResult result, double estimatedServerTick, float tickDeltaSeconds)
        {
            var shot = result.Shot;
            if (!IsVisualProjectile(shot))
            {
                return;
            }
            var key = (shot.EntityId, shot.ShotSequence);
            if (result.DidHit || result.HitEnvironment)
            {
                ConfirmedProjectileImpactCount++;
                ReleaseProjectile(key);
                RememberCompletedProjectile(key);
                return;
            }
            if (completedProjectiles.Contains(key))
            {
                return;
            }

            ConfirmedProjectileLaunchCount++;

            float age = EstimateProjectileAge(shot.ServerTick, estimatedServerTick, tickDeltaSeconds);
            if (projectiles.TryGetValue(key, out var entry))
            {
                // 无有效时钟时沿用预测已飞行时间；可靠确认仅校正已有代理，不创建第二个。
                if (double.IsNaN(estimatedServerTick) || double.IsInfinity(estimatedServerTick))
                {
                    age = entry.View.AgeSeconds;
                }
                entry.Confirmed = true;
                entry.View.Initialize(shot, age);
            }
            else
            {
                CreateProjectile(key, shot, true, age);
            }
            // 已过射程的延迟发射确认不应在原点闪现，也不等待下一渲染帧才回收。
            if (!projectiles[key].View.Advance(0f))
            {
                ReleaseProjectile(key);
                RememberCompletedProjectile(key);
            }
        }

        /// <summary>服务器在发射 Tick 当次即推进一次弹丸，因此确认位置包含该 Tick 的一步行程。</summary>
        public static float EstimateProjectileAge(uint shotTick, double estimatedServerTick, float tickDeltaSeconds)
        {
            if (double.IsNaN(estimatedServerTick) || double.IsInfinity(estimatedServerTick)
                || float.IsNaN(tickDeltaSeconds) || float.IsInfinity(tickDeltaSeconds) || tickDeltaSeconds <= 0f)
            {
                return 0f;
            }
            double ticks = estimatedServerTick - shotTick;
            // Tick 使用 uint 自然回绕；时钟差在半个序号域内解释，未来时间不反向飞行。
            ticks %= 4294967296d;
            if (ticks < -2147483648d)
            {
                ticks += 4294967296d;
            }
            else if (ticks > 2147483648d)
            {
                ticks -= 4294967296d;
            }
            return (float)System.Math.Min(PredictedProjectileVisual.MaximumLifetimeSeconds,
                (System.Math.Max(0d, ticks) + 1d) * tickDeltaSeconds);
        }

        private void CreateProjectile(
            (uint Entity, uint Shot) key, in AuthoritativeShot shot, bool confirmed, float age)
        {
            if (projectiles.Count == MaximumProjectileCount)
            {
                var oldest = default((uint Entity, uint Shot));
                ulong oldestOrder = ulong.MaxValue;
                foreach (var pair in projectiles)
                {
                    if (pair.Value.Order < oldestOrder)
                    {
                        oldestOrder = pair.Value.Order;
                        oldest = pair.Key;
                    }
                }
                ReleaseProjectile(oldest);
            }

            PredictedProjectileVisual visual = projectilePool.Count > 0 ? projectilePool.Dequeue() : CreateProjectileVisual();
            visual.Initialize(shot, age);
            visual.gameObject.SetActive(true);
            projectiles.Add(key, new ProjectileEntry { View = visual, Confirmed = confirmed, Order = projectileOrder++ });
        }

        private PredictedProjectileVisual CreateProjectileVisual()
        {
            PredictedProjectileVisual visual;
            if (projectileVisualPrefab != null)
            {
                visual = Instantiate(projectileVisualPrefab, transform);
            }
            else
            {
                var target = new GameObject("Tracked Projectile Visual");
                target.transform.SetParent(transform, false);
                visual = target.AddComponent<PredictedProjectileVisual>();
                if (EnsureLineMaterial())
                {
                    var line = target.AddComponent<LineRenderer>();
                    line.sharedMaterial = lineMaterial;
                    line.useWorldSpace = true;
                    line.positionCount = 2;
                    line.widthMultiplier = 0.06f; // 默认弹丸尾迹宽度，米。
                    line.startColor = new Color(1f, 0.6f, 0.1f);
                    line.endColor = Color.yellow;
                    line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    line.receiveShadows = false;
                }
            }
            // 即使配置了带碰撞体的 Prefab，也不能进入玩法碰撞或触发器查询。
            foreach (var collider in visual.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }
            foreach (var body in visual.GetComponentsInChildren<Rigidbody>(true))
            {
                body.isKinematic = true;
                body.useGravity = false;
            }
            CreatedProjectileVisualCount++;
            return visual;
        }

        private void AdvanceProjectiles(float elapsed)
        {
            expiredProjectiles.Clear();
            foreach (var pair in projectiles)
            {
                bool active = pair.Value.View.Advance(elapsed);
                if (!active || (!pair.Value.Confirmed && pair.Value.View.AgeSeconds >= PredictionTimeoutSeconds))
                {
                    expiredProjectiles.Add(pair.Key);
                    if (pair.Value.Confirmed)
                    {
                        RememberCompletedProjectile(pair.Key);
                    }
                }
            }
            foreach (var key in expiredProjectiles)
            {
                ReleaseProjectile(key);
            }
        }

        private void ReleaseProjectile((uint Entity, uint Shot) key)
        {
            if (projectiles.TryGetValue(key, out var entry))
            {
                entry.View.gameObject.SetActive(false);
                projectilePool.Enqueue(entry.View);
                projectiles.Remove(key);
            }
        }

        private void RememberCompletedProjectile((uint Entity, uint Shot) key)
        {
            if (!completedProjectiles.Add(key))
            {
                return;
            }
            completedProjectileOrder.Enqueue(key);
            if (completedProjectileOrder.Count > CompletedProjectileCapacity)
            {
                completedProjectiles.Remove(completedProjectileOrder.Dequeue());
            }
        }

        private void ClearProjectiles()
        {
            ConfirmedProjectileLaunchCount = 0;
            ConfirmedProjectileImpactCount = 0;
            foreach (var entry in projectiles.Values)
            {
                entry.View.gameObject.SetActive(false);
                projectilePool.Enqueue(entry.View);
            }
            projectiles.Clear();
            completedProjectiles.Clear();
            completedProjectileOrder.Clear();
            expiredProjectiles.Clear();
        }

        private static bool IsVisualProjectile(in AuthoritativeShot shot) =>
            shot.FireMode == WeaponFireMode.Projectile && shot.EntityId != 0 && shot.ShotSequence != 0
            && shot.ProjectileSpeedFixed.Raw > 0 && shot.RangeFixed.Raw > 0 && shot.DirectionFixed != AFPS.Math.FixVec3.Zero;
    }
}

using AFPS.NetCode.Weapons;
using AFPS.Simulation.Weapons;
using UnityEngine;

namespace AFPS.Presentation.Weapons
{
    /// <summary>协调预测/确认射击表现：枪口、弹丸代理、曳光/命中、音效及射手/受击反馈。</summary>
    // 保留旧组件类型的迁移信息；文件与.meta成对移动，场景/Prefab继续沿用原GUID。
    [UnityEngine.Scripting.APIUpdating.MovedFrom(
        true, sourceNamespace: "AFPS.Presentation.Weapons", sourceAssembly: "AFPS.Presentation",
        sourceClassName: "ShotResultView")]
    public sealed partial class ShotPresentationController : MonoBehaviour
    {
        // 表现上限与时间均不参与模拟：长度为米，持续时间为真实秒，不受游戏时间缩放影响。
        public const int MaximumLineCount = 96;
        private const float TracerSeconds = 0.08f;
        private const float ImpactSeconds = 0.16f;
        private const float HitMarkerSeconds = 0.2f;
        private const float ImpactHalfSize = 0.08f;
        private readonly LineRenderer[] lines = new LineRenderer[MaximumLineCount];
        private readonly float[] remainingSeconds = new float[MaximumLineCount];
        private Material lineMaterial;
        private int nextLine;
        private float hitMarkerRemaining;

        public ulong ConsumedResultCount { get; private set; }
        public ulong LocalHitConfirmationCount { get; private set; }
        public bool HitMarkerVisible => hitMarkerRemaining > 0f;
        public bool LastHitWasKill { get; private set; }

        public void Consume(
            in AuthoritativeShotResult result,
            uint localEntityId,
            double estimatedServerTick = double.NaN,
            float tickDeltaSeconds = 0.02f)
        {
            if (!isActiveAndEnabled)
            {
                return;
            }
            ConsumedResultCount++;
            ConsumeEffects(result, localEntityId);
            ConsumeProjectile(result, estimatedServerTick, tickDeltaSeconds);
            ConsumeDamage(result, localEntityId);
            Vector3 end = result.HitPoint;
            if (result.Shot.FireMode == WeaponFireMode.Hitscan)
            {
                // 只替换表现起点；终点仍是服务器裁决，不能把枪口偏移带回命中查询。
                DrawLine(VisualOrigin(result.Shot), end, new Color(1f, 0.8f, 0.25f), TracerSeconds);
            }

            if (!result.DidHit && !result.HitEnvironment)
            {
                // Projectile 的未命中结果表示发射确认，不能画出瞬间穿越整个射程的光束。
                return;
            }

            Color impactColor = new Color(1f, 0.3f, 0.2f);
            DrawLine(end - Vector3.right * ImpactHalfSize, end + Vector3.right * ImpactHalfSize,
                impactColor, ImpactSeconds);
            DrawLine(end - Vector3.up * ImpactHalfSize, end + Vector3.up * ImpactHalfSize,
                impactColor, ImpactSeconds);
            if (result.DidHit && localEntityId != 0 && result.Shot.EntityId == localEntityId && result.TargetEntityId != localEntityId)
            {
                // 同一次爆炸可命中多个目标；该提示窗口已确认的击杀不能被随后非致命溅射改回白色。
                LastHitWasKill = (hitMarkerRemaining > 0f && LastHitWasKill) || result.TargetHealthFixed.Raw == 0;
                hitMarkerRemaining = HitMarkerSeconds;
                LocalHitConfirmationCount++;
            }
        }

        public void Advance(float unscaledDeltaSeconds)
        {
            float elapsed = Mathf.Max(0f, unscaledDeltaSeconds);
            AdvanceProjectiles(elapsed);
            AdvanceFeedback(elapsed);
            hitMarkerRemaining = Mathf.Max(0f, hitMarkerRemaining - elapsed);
            for (int i = 0; i < lines.Length; i++)
            {
                remainingSeconds[i] = Mathf.Max(0f, remainingSeconds[i] - elapsed);
                if (lines[i] != null)
                {
                    lines[i].enabled = remainingSeconds[i] > 0f;
                }
            }
        }

        public void Clear()
        {
            hitMarkerRemaining = 0f;
            LastHitWasKill = false;
            ConsumedResultCount = 0;
            LocalHitConfirmationCount = 0;
            ClearProjectiles();
            ClearFeedback();
            ClearEffects();
            localMuzzles.Clear();
            muzzleOrder.Clear();
            for (int i = 0; i < lines.Length; i++)
            {
                remainingSeconds[i] = 0f;
                if (lines[i] != null)
                {
                    lines[i].enabled = false;
                }
            }
        }

        private void DrawLine(Vector3 start, Vector3 end, Color color, float lifetimeSeconds)
        {
            if (!EnsureLineMaterial())
            {
                return;
            }

            int index = nextLine;
            nextLine = (nextLine + 1) % MaximumLineCount;
            if (lines[index] == null)
            {
                var target = new GameObject("Authoritative Shot Line");
                target.transform.SetParent(transform, false);
                var line = target.AddComponent<LineRenderer>();
                line.sharedMaterial = lineMaterial;
                line.useWorldSpace = true;
                line.positionCount = 2;
                line.widthMultiplier = 0.025f; // 世界空间线宽，米。
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                lines[index] = line;
            }

            LineRenderer renderer = lines[index];
            renderer.startColor = color;
            renderer.endColor = color;
            renderer.SetPosition(0, start);
            renderer.SetPosition(1, end);
            renderer.enabled = true;
            remainingSeconds[index] = lifetimeSeconds;
        }

        private bool EnsureLineMaterial()
        {
            if (lineMaterial == null)
            {
                Material template = CombatPresentationAssets.Load<Material>("Effects");
                if (template != null)
                {
                    lineMaterial = new Material(template);
                    return true;
                }
                // Built-in 管线已有的精灵 Shader；缺失时保留确认反馈，不制造粉色错误材质。
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null)
                {
                    return false;
                }
                lineMaterial = new Material(shader);
            }
            return true;
        }

        private void OnDisable()
        {
            // 只停用Behaviour不会自动停掉子节点AudioSource；同时清理旧世代的线和弹丸。
            Clear();
        }

        private void OnDestroy()
        {
            if (lineMaterial != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(lineMaterial);
                }
                else
                {
                    DestroyImmediate(lineMaterial);
                }
            }
        }
    }
}

using AFPS.NetCode.Weapons;
using AFPS.Simulation.Weapons;
using UnityEngine;

namespace AFPS.Presentation.Weapons
{
    /// <summary>只读消费权威结果：世界空间曳光/命中点，以及本地射手的确认反馈。</summary>
    public sealed partial class ShotResultView : MonoBehaviour
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
            ConsumedResultCount++;
            ConsumeEffects(result, localEntityId);
            ConsumeProjectile(result, estimatedServerTick, tickDeltaSeconds);
            ConsumeDamage(result, localEntityId);
            Vector3 end = result.HitPoint;
            if (result.Shot.FireMode == WeaponFireMode.Hitscan)
            {
                // 使用服务器的起终点，不从当前远端胶囊反推枪口，也不重复本地预测枪口、声音或后坐力。
                DrawLine(result.Shot.Origin, end, new Color(1f, 0.8f, 0.25f), TracerSeconds);
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

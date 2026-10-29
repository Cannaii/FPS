using AFPS.NetCode.Weapons;
using UnityEngine;

namespace AFPS.Presentation.Weapons
{
    /// <summary>预测及权威确认共用的无碰撞弹丸视觉代理；由射击表现控制器推进/回收。</summary>
    // 保留旧组件类型的迁移信息；文件与.meta成对移动，场景/Prefab继续沿用原GUID。
    [UnityEngine.Scripting.APIUpdating.MovedFrom(
        true, sourceNamespace: "AFPS.Presentation.Weapons", sourceAssembly: "AFPS.Presentation",
        sourceClassName: "PredictedProjectileVisual")]
    public sealed class ProjectileVisualProxy : MonoBehaviour
    {
        private Vector3 origin;
        private Vector3 direction;
        private float speed;
        private float range;
        private float ageSeconds;
        private LineRenderer trail;
        private Vector3 muzzleOffset;

        // 极慢弹丸也不能永久占用代理；只限制表现，不改变服务器弹丸寿命。
        public const float MaximumLifetimeSeconds = 30f;
        public float AgeSeconds => ageSeconds;

        public void Initialize(in AuthoritativeShot shot, float initialAgeSeconds, Vector3? visualOrigin = null)
        {
            origin = shot.Origin;
            muzzleOffset = visualOrigin.HasValue ? visualOrigin.Value - origin : Vector3.zero;
            direction = shot.Direction;
            speed = shot.ProjectileSpeed;
            range = shot.Range;
            ageSeconds = Mathf.Max(0f, initialAgeSeconds);
            trail = GetComponent<LineRenderer>();
            ApplyPosition();
        }

        public bool Advance(float unscaledDeltaSeconds)
        {
            ageSeconds += Mathf.Max(0f, unscaledDeltaSeconds);
            ApplyPosition();
            return ageSeconds < MaximumLifetimeSeconds && speed * ageSeconds < range;
        }

        private void ApplyPosition()
        {
            float distance = Mathf.Min(range, speed * ageSeconds);
            transform.position = VisualPosition(distance);
            if (direction.sqrMagnitude > 0f)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }
            if (trail != null)
            {
                // 默认视觉仅为 0.25 米短尾迹，不能把剩余射程提前画成光束。
                trail.SetPosition(0, VisualPosition(Mathf.Max(0f, distance - 0.25f)));
                trail.SetPosition(1, transform.position);
            }
        }

        private Vector3 VisualPosition(float distance)
        {
            // 前5米逐渐并入逻辑弹道；只移动无碰撞代理，确认/命中仍使用原始Shot。
            return origin + direction * distance + muzzleOffset * (1f - Mathf.Clamp01(distance / 5f));
        }
    }
}

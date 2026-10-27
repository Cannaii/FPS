using AFPS.NetCode.Messages;
using AFPS.NetCode.Weapons;
using UnityEngine;

namespace AFPS.Presentation.Weapons
{
    public sealed partial class ShotResultView
    {
        private float damageRemaining;
        private float rejectionRemaining;
        public bool DamageFeedbackVisible => damageRemaining > 0f;
        public bool RejectionFeedbackVisible => rejectionRemaining > 0f;
        public Vector3 LastDamageSource { get; private set; }
        private Vector3 incomingDamageDirection;
        private bool damageFromProjectile;
        public WeaponFireRejectReason LastRejectionReason { get; private set; }
        public ulong ReceivedDamageCount { get; private set; }
        public ulong RejectedShotCount { get; private set; }

        public void Reject(in ShotRejection value)
        {
            var key = (value.EntityId, value.ShotSequence);
            ReleaseProjectile(key);
            RememberCompletedProjectile(key);
            LastRejectionReason = value.Reason;
            localMuzzles.Remove(key);
            RejectedShotCount++;
            rejectionRemaining = 0.6f; // 拒绝原因提示，真实秒。
        }

        private void ConsumeDamage(in AuthoritativeShotResult result, uint localEntity)
        {
            if (localEntity != 0 && result.DidHit && result.TargetEntityId == localEntity)
            {
                LastDamageSource = result.Shot.FireMode == AFPS.Simulation.Weapons.WeaponFireMode.Projectile
                    ? result.HitPoint : result.Shot.Origin;
                // 子弹方向是“飞向受击者”，指示器需要相反的“来弹一侧”。直击落点贴身，不能拿它当攻击者位置。
                incomingDamageDirection = -result.Shot.Direction;
                damageFromProjectile = result.Shot.FireMode == AFPS.Simulation.Weapons.WeaponFireMode.Projectile;
                damageRemaining = 0.35f; // 受击闪屏与方向提示，真实秒；不推导或反写生命值。
                ReceivedDamageCount++;
            }
        }

        /// <summary>返回相机水平朝向下的单位方向：上为前、右为右；垂直来弹不伪造方位。</summary>
        public bool TryGetDamageDirection(Vector3 viewerPosition, Quaternion viewerRotation, out Vector2 direction)
        {
            Vector3 source = incomingDamageDirection;
            Vector3 impactOffset = Vector3.ProjectOnPlane(LastDamageSource - viewerPosition, Vector3.up);
            if (damageFromProjectile && impactOffset.sqrMagnitude > 0.25f)
            {
                // 距身体超过0.5米的爆点提供溅射来向；贴身直击回退到飞行反方向。
                source = impactOffset;
            }
            source = Vector3.ProjectOnPlane(source, Vector3.up);
            Vector3 forward = Vector3.ProjectOnPlane(viewerRotation * Vector3.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.000001f)
            {
                Vector3 right = Vector3.ProjectOnPlane(viewerRotation * Vector3.right, Vector3.up).normalized;
                forward = Vector3.Cross(right, Vector3.up);
            }
            forward.Normalize();
            direction = new Vector2(Vector3.Dot(source, Vector3.Cross(Vector3.up, forward)), Vector3.Dot(source, forward));
            if (!DamageFeedbackVisible || direction.sqrMagnitude < 0.000001f)
            {
                direction = default;
                return false;
            }
            direction.Normalize();
            return true;
        }

        private void AdvanceFeedback(float seconds)
        {
            damageRemaining = Mathf.Max(0f, damageRemaining - seconds);
            rejectionRemaining = Mathf.Max(0f, rejectionRemaining - seconds);
        }

        private void ClearFeedback()
        {
            damageRemaining = 0f;
            rejectionRemaining = 0f;
            LastDamageSource = default;
            incomingDamageDirection = default;
            damageFromProjectile = false;
            LastRejectionReason = WeaponFireRejectReason.None;
            ReceivedDamageCount = 0;
            RejectedShotCount = 0;
        }
    }
}

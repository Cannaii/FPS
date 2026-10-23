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
        public WeaponFireRejectReason LastRejectionReason { get; private set; }
        public ulong ReceivedDamageCount { get; private set; }
        public ulong RejectedShotCount { get; private set; }

        public void Reject(in ShotRejection value)
        {
            var key = (value.EntityId, value.ShotSequence);
            ReleaseProjectile(key);
            RememberCompletedProjectile(key);
            LastRejectionReason = value.Reason;
            RejectedShotCount++;
            rejectionRemaining = 0.6f; // 拒绝原因提示，真实秒。
        }

        private void ConsumeDamage(in AuthoritativeShotResult result, uint localEntity)
        {
            if (localEntity != 0 && result.DidHit && result.TargetEntityId == localEntity)
            {
                LastDamageSource = result.Shot.Origin;
                damageRemaining = 0.35f; // 受击闪屏与方向提示，真实秒；不推导或反写生命值。
                ReceivedDamageCount++;
            }
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
            LastRejectionReason = WeaponFireRejectReason.None;
            ReceivedDamageCount = 0;
            RejectedShotCount = 0;
        }
    }
}

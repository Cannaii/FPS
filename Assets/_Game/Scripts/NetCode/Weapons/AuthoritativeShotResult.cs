using UnityEngine;

namespace AFPS.NetCode.Weapons
{
    /// <summary>服务器对一次已接受射击的最终命中、弹药与生命值裁决。</summary>
    public readonly struct AuthoritativeShotResult
    {
        public readonly AuthoritativeShot Shot;
        public readonly int RemainingAmmo;
        public readonly bool DidHit;
        public readonly uint TargetEntityId;
        public readonly float TargetHealth;
        public readonly Vector3 HitPoint;

        public AuthoritativeShotResult(in AuthoritativeShot shot, int remainingAmmo, bool didHit, uint targetEntityId, float targetHealth, Vector3 hitPoint)
        {
            Shot = shot;
            RemainingAmmo = remainingAmmo;
            DidHit = didHit;
            TargetEntityId = targetEntityId;
            TargetHealth = targetHealth;
            HitPoint = hitPoint;
        }
    }
}

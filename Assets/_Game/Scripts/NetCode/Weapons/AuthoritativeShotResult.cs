using AFPS.Math;

namespace AFPS.NetCode.Weapons
{
    /// <summary>权威命中结果；生命值为 Q16.16，命中点为米，尚未迁移的网络边界负责转换。</summary>
    public readonly partial struct AuthoritativeShotResult
    {
        public readonly AuthoritativeShot Shot;
        public readonly int RemainingAmmo;
        public readonly bool DidHit;
        public readonly bool HitEnvironment;
        public readonly uint TargetEntityId;
        public readonly Fix TargetHealthFixed;
        public readonly FixVec3 HitPointFixed;

        public AuthoritativeShotResult(
            in AuthoritativeShot shot,
            int remainingAmmo,
            bool didHit,
            uint targetEntityId,
            Fix targetHealth,
            FixVec3 hitPoint,
            bool hitEnvironment = false)
        {
            Shot = shot;
            RemainingAmmo = remainingAmmo;
            DidHit = didHit;
            HitEnvironment = hitEnvironment;
            TargetEntityId = targetEntityId;
            TargetHealthFixed = targetHealth;
            HitPointFixed = hitPoint;
        }
    }
}

using AFPS.Math;
using UnityEngine;

namespace AFPS.NetCode.Weapons
{
    public readonly partial struct AuthoritativeShotResult
    {
        public float TargetHealth => TargetHealthFixed.ToFloat();
        public Vector3 HitPoint => new Vector3(
            HitPointFixed.X.ToFloat(), HitPointFixed.Y.ToFloat(), HitPointFixed.Z.ToFloat());

        public AuthoritativeShotResult(
            in AuthoritativeShot shot,
            int remainingAmmo,
            bool didHit,
            uint targetEntityId,
            float targetHealth,
            Vector3 hitPoint)
            : this(shot, remainingAmmo, didHit, targetEntityId, Fix.FromFloat(targetHealth),
                new FixVec3(Fix.FromFloat(hitPoint.x), Fix.FromFloat(hitPoint.y), Fix.FromFloat(hitPoint.z)))
        {
        }
    }
}

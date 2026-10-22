using System.Collections.Generic;
using AFPS.Math;
using UnityEngine;

namespace AFPS.NetCode.Weapons
{
    public static partial class ServerHitscanResolver
    {
        public static bool TryResolve(
            in AuthoritativeShot shot,
            IReadOnlyList<ServerCombatTarget> targets,
            out ServerCombatTarget target,
            out Vector3 hitPoint)
        {
            bool found = TryResolveFixed(shot, targets, out target, out FixVec3 point);
            hitPoint = new Vector3(point.X.ToFloat(), point.Y.ToFloat(), point.Z.ToFloat());
            return found;
        }
    }
}

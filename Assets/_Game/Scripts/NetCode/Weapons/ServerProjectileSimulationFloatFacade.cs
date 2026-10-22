using System;
using System.Collections.Generic;
using AFPS.Math;

namespace AFPS.NetCode.Weapons
{
    public sealed partial class ServerProjectileSimulation
    {
        /// <summary>旧浮点步长兼容边界；权威会话直接传入已规范的 Fix 秒数。</summary>
        public int Advance(float deltaTime, IReadOnlyList<ServerCombatTarget> targets, List<AuthoritativeShotResult> impacts)
        {
            if (deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            }
            return Advance(Fix.FromFloat(deltaTime), targets, impacts);
        }
    }
}

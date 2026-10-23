using System.Collections.Generic;
using AFPS.Math;
using AFPS.Simulation.World;

namespace AFPS.NetCode.Weapons
{
    /// <summary>先按静态定点世界限制射线，再查询玩家胶囊；环境不能被玩家回溯绕过。</summary>
    public static class ServerWorldShotResolver
    {
        public static bool TryResolve(
            in AuthoritativeShot shot,
            IReadOnlyList<ServerCombatTarget> targets,
            IWorldQuery world,
            int layerMask,
            out ServerCombatTarget target,
            out FixVec3 point,
            out bool hitEnvironment)
        {
            var environment = world == null ? default : world.Raycast(shot.OriginFixed, shot.DirectionFixed,
                shot.RangeFixed, layerMask, FixedSurface.Solid);
            var limited = shot;
            if (environment.DidHit)
            {
                // 世界距离向下取整最多提前 1 Raw；再收缩 1 Raw，环境与玩家同距时由环境阻挡。
                int range = System.Math.Max(0, environment.Distance.Raw - 1);
                limited = new AuthoritativeShot(shot.EntityId, shot.ShotSequence, shot.ServerTick,
                    shot.WeaponId, shot.FireMode, shot.OriginFixed, shot.DirectionFixed,
                    shot.DamageFixed, Fix.FromRaw(range), shot.ProjectileSpeedFixed, shot.RewindTick, true);
            }
            bool playerHit = ServerHitscanResolver.TryResolveFixed(limited, targets, out target, out point);
            hitEnvironment = !playerHit && environment.DidHit;
            if (hitEnvironment)
            {
                point = environment.Point;
            }
            return playerHit;
        }
    }
}

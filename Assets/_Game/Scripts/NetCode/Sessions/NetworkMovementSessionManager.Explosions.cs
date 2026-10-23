using System.Collections.Generic;
using AFPS.Math;
using AFPS.NetCode.Match;
using AFPS.NetCode.Weapons;
using AFPS.Simulation.Weapons;
using AFPS.Simulation.World;

namespace AFPS.NetCode.Sessions
{
    public sealed partial class NetworkMovementSessionManager
    {
        private readonly List<ExplosionDamage> explosionDamage = new List<ExplosionDamage>();

        private void ResolveProjectileImpact(AuthoritativeShotResult impact)
        {
            if (deathmatchPhase != DeathmatchPhase.Playing)
            {
                return;
            }
            Fix health = impact.DidHit ? ApplyDamage(impact.Shot.EntityId, impact.TargetEntityId,
                impact.Shot.DamageFixed, latestServerWorldTick) : Fix.Zero;
            BroadcastShotResult(new AuthoritativeShotResult(impact.Shot, impact.RemainingAmmo, impact.DidHit,
                impact.TargetEntityId, health, impact.HitPointFixed, impact.HitEnvironment));
            BuildCombatTargets(latestServerWorldTick, latestServerWorldTick);
            if (impact.Shot.WeaponId == WeaponCatalog.SecondaryFor(weaponConfig).WeaponId
                && deathmatchPhase == DeathmatchPhase.Playing)
            {
                var visibilityOrigin = impact.HitPointFixed;
                if (impact.HitEnvironment && WorldContext != null)
                {
                    var wall = WorldContext.World.Raycast(impact.Shot.OriginFixed, impact.Shot.DirectionFixed,
                        impact.Shot.RangeFixed, WorldContext.CollisionLayerMask, FixedSurface.Solid);
                    // 表面点退开 2 Raw，避免包含边界的射线把爆炸自身所在的墙当作所有方向的遮挡。
                    visibilityOrigin = ServerHitscanResolver.PointAtDistance(visibilityOrigin, wall.Normal, 2);
                }
                ServerExplosionResolver.Resolve(impact.HitPointFixed, visibilityOrigin,
                    WeaponCatalog.LauncherExplosionRadius, impact.Shot.DamageFixed, impact.TargetEntityId,
                    combatTargets, WorldContext?.World, WorldContext?.CollisionLayerMask ?? ~0, explosionDamage);
                foreach (var entry in explosionDamage)
                {
                    if (deathmatchPhase != DeathmatchPhase.Playing)
                    {
                        break;
                    }
                    if (!IsServerPlayerAlive(entry.EntityId))
                    {
                        continue;
                    }
                    Fix targetHealth = ApplyDamage(impact.Shot.EntityId, entry.EntityId, entry.Damage, latestServerWorldTick);
                    BroadcastShotResult(new AuthoritativeShotResult(impact.Shot, impact.RemainingAmmo,
                        true, entry.EntityId, targetHealth, impact.HitPointFixed));
                }
                BuildCombatTargets(latestServerWorldTick, latestServerWorldTick);
            }
        }
    }
}

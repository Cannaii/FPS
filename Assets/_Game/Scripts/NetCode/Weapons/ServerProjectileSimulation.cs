using System;
using System.Collections.Generic;
using AFPS.Math;
using AFPS.Simulation.Weapons;
using AFPS.Simulation.World;

namespace AFPS.NetCode.Weapons
{
    /// <summary>服务器定点弹丸：无重力直线运动；不足一 Raw 的行程跨 Tick 累积，不因每帧舍入而停滞。</summary>
    public sealed partial class ServerProjectileSimulation
    {
        private struct ProjectileState
        {
            public AuthoritativeShot Shot;
            public FixVec3 Position;
            public int TravelledRaw;
            public long DistanceRemainder;
            public int RemainingAmmo;
        }

        private readonly List<ProjectileState> active = new List<ProjectileState>();
        public int Count => active.Count;

        /// <summary>丢弃整个对局的在途弹丸；不能让旧房间的射击影响下一批玩家。</summary>
        public void Clear()
        {
            active.Clear();
        }

        public bool Spawn(in AuthoritativeShot shot, int remainingAmmo = 0)
        {
            if (shot.FireMode != WeaponFireMode.Projectile || shot.ProjectileSpeedFixed.Raw <= 0
                || shot.RangeFixed.Raw <= 0 || shot.DirectionFixed == FixVec3.Zero)
            {
                return false;
            }
            active.Add(new ProjectileState
            {
                Shot = shot,
                Position = shot.OriginFixed,
                RemainingAmmo = remainingAmmo
            });
            return true;
        }

        public int Advance(Fix deltaTime, IReadOnlyList<ServerCombatTarget> targets, List<AuthoritativeShotResult> impacts,
            IWorldQuery world = null, int layerMask = ~0)
        {
            if (deltaTime.Raw <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            }
            int impactCount = 0;
            for (int index = active.Count - 1; index >= 0; index--)
            {
                ProjectileState projectile = active[index];
                long distanceProduct = (long)projectile.Shot.ProjectileSpeedFixed.Raw * deltaTime.Raw
                    + projectile.DistanceRemainder;
                long stepRaw = distanceProduct / Fix.Scale;
                projectile.DistanceRemainder = distanceProduct % Fix.Scale;
                int distanceRaw = (int)System.Math.Min(stepRaw, projectile.Shot.RangeFixed.Raw - projectile.TravelledRaw);
                if (distanceRaw == 0)
                {
                    active[index] = projectile;
                    continue;
                }

                AuthoritativeShot segment = new AuthoritativeShot(
                    projectile.Shot.EntityId, projectile.Shot.ShotSequence, projectile.Shot.ServerTick,
                    projectile.Shot.WeaponId, WeaponFireMode.Projectile,
                    projectile.Position, projectile.Shot.DirectionFixed, projectile.Shot.DamageFixed,
                    Fix.FromRaw(distanceRaw), projectile.Shot.ProjectileSpeedFixed, projectile.Shot.RewindTick);
                bool didHit = ServerWorldShotResolver.TryResolve(segment, targets, world, layerMask,
                    out ServerCombatTarget target, out FixVec3 hitPoint, out bool hitEnvironment);
                if (didHit || hitEnvironment)
                {
                    impacts?.Add(new AuthoritativeShotResult(projectile.Shot, projectile.RemainingAmmo,
                        didHit, didHit ? target.EntityId : 0, didHit ? target.HealthFixed : Fix.Zero, hitPoint, hitEnvironment));
                    active.RemoveAt(index);
                    impactCount++;
                    continue;
                }

                projectile.TravelledRaw += distanceRaw;
                // 从原始起点和累计行程生成位置，避免逐 Tick 舍入使弹道随分帧方式漂移。
                projectile.Position = ServerHitscanResolver.PointAtDistance(
                    projectile.Shot.OriginFixed, projectile.Shot.DirectionFixed, projectile.TravelledRaw);
                if (projectile.TravelledRaw >= projectile.Shot.RangeFixed.Raw || OutsideCoordinateDomain(projectile))
                {
                    active.RemoveAt(index);
                }
                else
                {
                    active[index] = projectile;
                }
            }
            return impactCount;
        }

        private static bool OutsideCoordinateDomain(in ProjectileState projectile) =>
            OutsideAxis(projectile.Shot.OriginFixed.X.Raw, projectile.Shot.DirectionFixed.X.Raw, projectile.TravelledRaw)
            || OutsideAxis(projectile.Shot.OriginFixed.Y.Raw, projectile.Shot.DirectionFixed.Y.Raw, projectile.TravelledRaw)
            || OutsideAxis(projectile.Shot.OriginFixed.Z.Raw, projectile.Shot.DirectionFixed.Z.Raw, projectile.TravelledRaw);

        private static bool OutsideAxis(int origin, int direction, int travelled)
        {
            long scaled = (long)origin * Fix.Scale + (long)direction * travelled;
            // 先完成本步宽坐标命中查询，再移除离开表示域的弹丸，不在饱和坐标处继续飞行。
            return scaled < (long)int.MinValue * Fix.Scale || scaled > (long)int.MaxValue * Fix.Scale;
        }
    }
}

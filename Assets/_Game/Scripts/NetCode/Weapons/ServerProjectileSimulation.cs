using System;
using System.Collections.Generic;
using AFPS.Simulation.Weapons;
using UnityEngine;

namespace AFPS.NetCode.Weapons
{
    /// <summary>以服务器 Tick 推进权威弹丸；客户端只根据射击结果创建视觉代理。</summary>
    public sealed class ServerProjectileSimulation
    {
        private struct ProjectileState
        {
            public AuthoritativeShot Shot;
            public Vector3 Position;
            public float RemainingDistance;
            public int RemainingAmmo;
        }

        private readonly List<ProjectileState> active = new List<ProjectileState>();

        public int Count => active.Count;

        public bool Spawn(in AuthoritativeShot shot, int remainingAmmo = 0)
        {
            if (shot.FireMode != WeaponFireMode.Projectile || shot.ProjectileSpeed <= 0f || shot.Range <= 0f)
            {
                return false;
            }

            active.Add(new ProjectileState { Shot = shot, Position = shot.Origin, RemainingDistance = shot.Range, RemainingAmmo = remainingAmmo });
            return true;
        }

        public int Advance(float deltaTime, IReadOnlyList<ServerCombatTarget> targets, List<AuthoritativeShotResult> impacts)
        {
            if (deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            }

            int impactCount = 0;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                ProjectileState projectile = active[i];
                float distance = Mathf.Min(projectile.Shot.ProjectileSpeed * deltaTime, projectile.RemainingDistance);
                AuthoritativeShot segment = new AuthoritativeShot(projectile.Shot.EntityId, projectile.Shot.ShotSequence, projectile.Shot.ServerTick, projectile.Shot.WeaponId, WeaponFireMode.Projectile, projectile.Position, projectile.Shot.Direction, projectile.Shot.Damage, distance, projectile.Shot.ProjectileSpeed);
                if (ServerHitscanResolver.TryResolve(segment, targets, out ServerCombatTarget target, out Vector3 hitPoint))
                {
                    impacts?.Add(new AuthoritativeShotResult(projectile.Shot, projectile.RemainingAmmo, true, target.EntityId, target.Health, hitPoint));
                    active.RemoveAt(i);
                    impactCount++;
                    continue;
                }

                projectile.Position += projectile.Shot.Direction * distance;
                projectile.RemainingDistance -= distance;
                if (projectile.RemainingDistance <= 0.0001f)
                {
                    active.RemoveAt(i);
                }
                else
                {
                    active[i] = projectile;
                }
            }

            return impactCount;
        }
    }
}

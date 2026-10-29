using System.Collections.Generic;
using AFPS.Math;
using AFPS.NetCode.LagCompensation;
using AFPS.NetCode.Weapons;
using AFPS.Simulation.Characters;
using AFPS.Simulation.Weapons;

namespace AFPS.Tests.EditMode
{
    /// <summary>512 Tick 命中/回溯/血量/弹丸共享重放；覆盖 Tick 回绕和大坐标低位。</summary>
    public static class DeterministicCombatScenario
    {
        public const ulong ExpectedHash = 7790476090935781710UL;

        public static ulong Run()
        {
            ulong hash = 14695981039346656037UL;
            Fix health = Fix.FromInt(100);
            RewindHitboxHistory history = new RewindHitboxHistory(16);
            ServerProjectileSimulation projectiles = new ServerProjectileSimulation();
            List<AuthoritativeShotResult> impacts = new List<AuthoritativeShotResult>();
            Fix x = Fix.FromRaw(655360001);
            for (int index = 0; index < 512; index++)
            {
                uint tick = unchecked(uint.MaxValue - 100 + (uint)index);
                ServerCombatTarget target = new ServerCombatTarget(2,
                    new FixVec3(Fix.FromRaw(x.Raw + (index * 7919) % 80001 - 40000), Fix.Zero,
                        Fix.FromRaw(327681 + index * 3)), Fix.Half, Fix.FromInt(2), health);
                history.Record(tick, target);
                bool sampled = history.TryGetAtOrBefore(unchecked(tick - 3), tick, 10,
                    out ServerCombatTarget historical, out uint sampledTick);
                Append(ref hash, sampled ? 1 : 0);
                Append(ref hash, unchecked((int)sampledTick));
                AuthoritativeShot shot = new AuthoritativeShot(1, (uint)index + 1, tick, 1,
                    WeaponFireMode.Hitscan, new FixVec3(x, Fix.One, Fix.Zero), FixVec3.Forward,
                    Fix.FromRaw(101), Fix.FromInt(10), Fix.Zero);
                bool hit = ServerHitscanResolver.TryResolveFixed(shot,
                    sampled ? new[] { historical } : null, out ServerCombatTarget resolved, out FixVec3 point);
                Append(ref hash, hit ? 1 : 0);
                Append(ref hash, unchecked((int)resolved.EntityId));
                Append(ref hash, point.X.Raw);
                Append(ref hash, point.Y.Raw);
                Append(ref hash, point.Z.Raw);
                if (hit)
                {
                    health = CombatHealthMath.ApplyDamage(health, shot.DamageFixed);
                }
                Append(ref hash, health.Raw);
                if ((index % 64) == 0)
                {
                    AuthoritativeShot projectile = new AuthoritativeShot(1, (uint)index + 1, tick, 2,
                        WeaponFireMode.Projectile, shot.OriginFixed, FixVec3.Forward,
                        Fix.FromRaw(196609), Fix.FromInt(20), Fix.FromRaw(196609));
                    projectiles.Spawn(projectile, 17);
                }
                impacts.Clear();
                Append(ref hash, projectiles.Advance(SimulationStep.Step50Hz, new[] { target }, impacts));
                Append(ref hash, projectiles.Count);
                foreach (AuthoritativeShotResult impact in impacts)
                {
                    Append(ref hash, unchecked((int)impact.Shot.ShotSequence));
                    Append(ref hash, impact.HitPointFixed.X.Raw);
                    Append(ref hash, impact.HitPointFixed.Y.Raw);
                    Append(ref hash, impact.HitPointFixed.Z.Raw);
                    Append(ref hash, impact.TargetHealthFixed.Raw);
                }
            }
            return hash;
        }

        private static void Append(ref ulong hash, int value)
        {
            uint bits = unchecked((uint)value);
            for (int shift = 0; shift < 32; shift += 8)
            {
                hash = unchecked((hash ^ (byte)(bits >> shift)) * 1099511628211UL);
            }
        }
    }
}

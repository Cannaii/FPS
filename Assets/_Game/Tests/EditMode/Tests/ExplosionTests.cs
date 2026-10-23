using System.Collections.Generic;
using AFPS.Math;
using AFPS.NetCode.Weapons;
using AFPS.Simulation.Weapons;
using AFPS.Simulation.World;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class ExplosionTests
    {
        [Test]
        public void Damage_UsesSquaredDistanceFalloffAndStableEntityOrder()
        {
            var output = new List<ExplosionDamage>();
            var targets = new[] { Target(9, 0), Target(3, 1), Target(4, 3), Target(5, 4) };
            var center = new FixVec3(Fix.Zero, Fix.One, Fix.Zero);
            ServerExplosionResolver.Resolve(center, center, Fix.FromInt(3), Fix.FromInt(100), 0, targets, null, ~0, output);
            Assert.That(output.Count, Is.EqualTo(2));
            Assert.That(output[0].EntityId, Is.EqualTo(3));
            Assert.That(output[0].Damage.Raw, Is.EqualTo((int)(6553600L * 8 / 9)));
            Assert.That(output[1].EntityId, Is.EqualTo(9));
            Assert.That(output[1].Damage, Is.EqualTo(Fix.FromInt(100)));
        }

        [Test]
        public void Damage_ExcludesDirectTargetAndKeepsOtherNearbyPlayers()
        {
            var output = new List<ExplosionDamage>();
            var center = new FixVec3(Fix.Zero, Fix.One, Fix.Zero);
            ServerExplosionResolver.Resolve(center, center, Fix.FromInt(3), Fix.FromInt(100), 2,
                new[] { Target(1, 1), Target(2, 0) }, null, ~0, output);
            Assert.That(output.Count, Is.EqualTo(1));
            Assert.That(output[0].EntityId, Is.EqualTo(1));
        }

        [Test]
        public void Damage_WallBlocksSplashButSameSideTargetReceivesDamage()
        {
            var world = new FixedWorld(new[] { new FixedWorldBox(1, 0, FixedSurface.Solid,
                new FixedAabb(new FixVec3(Fix.Half, Fix.Zero, -Fix.One),
                    new FixVec3(Fix.FromRatio(3, 2), Fix.FromInt(2), Fix.One))) });
            var center = new FixVec3(-Fix.One, Fix.One, Fix.Zero);
            var output = new List<ExplosionDamage>();
            ServerExplosionResolver.Resolve(center, center, Fix.FromInt(5), Fix.FromInt(100), 0,
                new[] { Target(2, 2), Target(3, -2) }, world, ~0, output);
            Assert.That(output.Count, Is.EqualTo(1));
            Assert.That(output[0].EntityId, Is.EqualTo(3));
        }

        [Test]
        public void Damage_LargeCoordinatesAndZeroRadiusDoNotOverflowOrRetainResults()
        {
            var output = new List<ExplosionDamage>();
            var target = new ServerCombatTarget(2, new FixVec3(Fix.MinValue, Fix.Zero, Fix.MinValue),
                Fix.Half, Fix.FromInt(2), Fix.FromInt(100));
            var center = new FixVec3(Fix.MaxValue, Fix.One, Fix.MaxValue);
            ServerExplosionResolver.Resolve(center, center, Fix.MaxValue, Fix.MaxValue, 0,
                new[] { target }, null, ~0, output);
            Assert.That(output.Count, Is.Zero);
            ServerExplosionResolver.Resolve(center, center, Fix.Zero, Fix.One, 0,
                new[] { target }, null, ~0, output);
            Assert.That(output.Count, Is.Zero);
        }

        [Test]
        public void Projectile_ImpactCallbackRefreshesTargetsBeforeNextProjectile()
        {
            var simulation = new ServerProjectileSimulation();
            var targets = new List<ServerCombatTarget>
            {
                new ServerCombatTarget(2, new FixVec3(Fix.Zero, Fix.Zero, Fix.One), Fix.Half, Fix.FromInt(2), Fix.FromInt(100))
            };
            foreach (uint entity in new[] { 1u, 3u })
            {
                simulation.Spawn(new AuthoritativeShot(entity, 1, 100, 2, WeaponFireMode.Projectile,
                    new FixVec3(Fix.Zero, Fix.One, Fix.Zero), FixVec3.Forward, Fix.FromInt(100), Fix.FromInt(10), Fix.FromInt(10)));
            }
            var impacts = new List<AuthoritativeShotResult>();
            simulation.Advance(Fix.One, targets, impacts, onImpact: value => targets.Clear());
            Assert.That(impacts.Count, Is.EqualTo(1));
        }

        private static ServerCombatTarget Target(uint entity, int x) =>
            new ServerCombatTarget(entity, new FixVec3(Fix.FromInt(x), Fix.Zero, Fix.Zero),
                Fix.Half, Fix.FromInt(2), Fix.FromInt(100));
    }
}

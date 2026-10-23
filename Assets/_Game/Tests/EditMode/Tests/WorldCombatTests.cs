using System;
using System.Collections.Generic;
using AFPS.Math;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Weapons;
using AFPS.Simulation.Weapons;
using AFPS.Simulation.World;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class WorldCombatTests
    {
        [TestCase(1, true)]
        [TestCase(6, false)]
        public void Hitscan_OnlyPlayerBeforeWallCanBeHit(int targetZ, bool expectedHit)
        {
            bool hit = ServerWorldShotResolver.TryResolve(Shot(), new[] { Target(Fix.FromInt(targetZ)) },
                World(), ~0, out _, out var point, out bool environment);
            Assert.That(hit, Is.EqualTo(expectedHit));
            Assert.That(environment, Is.EqualTo(!expectedHit));
            if (environment)
            {
                Assert.That(point.Z, Is.EqualTo(Fix.FromInt(3)));
            }
        }

        [Test]
        public void Hitscan_EnvironmentWinsAtTangentBoundaryAndForInsideOrigin()
        {
            Assert.That(ServerWorldShotResolver.TryResolve(Shot(), new[] { Target(Fix.FromRatio(7, 2)) },
                World(), ~0, out _, out _, out bool environment), Is.False);
            Assert.That(environment, Is.True);
            var inside = new AuthoritativeShot(1, 1, 100, 1, WeaponFireMode.Hitscan,
                new FixVec3(Fix.Zero, Fix.One, Fix.FromRatio(7, 2)), FixVec3.Forward,
                Fix.One, Fix.FromInt(10), Fix.Zero);
            Assert.That(ServerWorldShotResolver.TryResolve(inside, new[] { Target(Fix.FromRatio(7, 2)) },
                World(), ~0, out _, out _, out environment), Is.False);
            Assert.That(environment, Is.True);
        }

        [Test]
        public void Hitscan_RespectsWorldLayerMask()
        {
            Assert.That(ServerWorldShotResolver.TryResolve(Shot(), new[] { Target(Fix.FromInt(6)) },
                World(), 1 << 1, out _, out _, out bool environment), Is.True);
            Assert.That(environment, Is.False);
        }

        [Test]
        public void Projectile_StopsAtWallBeforePlayerAndEmitsEnvironmentImpact()
        {
            var shot = new AuthoritativeShot(1, 1, 100, 1, WeaponFireMode.Projectile,
                new FixVec3(Fix.Zero, Fix.One, Fix.Zero), FixVec3.Forward,
                Fix.FromInt(20), Fix.FromInt(10), Fix.FromInt(100));
            var simulation = new ServerProjectileSimulation();
            var impacts = new List<AuthoritativeShotResult>();
            simulation.Spawn(shot);
            Assert.That(simulation.Advance(Fix.One, new[] { Target(Fix.FromInt(6)) }, impacts, World()), Is.EqualTo(1));
            Assert.That(simulation.Count, Is.Zero);
            Assert.That(impacts[0].HitEnvironment, Is.True);
            Assert.That(impacts[0].DidHit, Is.False);
            Assert.That(impacts[0].TargetEntityId, Is.Zero);
            Assert.That(impacts[0].HitPointFixed.Z, Is.EqualTo(Fix.FromInt(3)));
        }

        [Test]
        public void EnvironmentResult_CodecPreservesFlagAndRejectsPlayerEnvironmentCombination()
        {
            var source = new AuthoritativeShotResult(Shot(), 29, false, 0, Fix.Zero,
                new FixVec3(Fix.Zero, Fix.One, Fix.FromInt(3)), true);
            byte[] bytes = new byte[AuthoritativeShotResultCodec.PacketSize];
            Assert.That(AuthoritativeShotResultCodec.TrySerialize(source, 1, new ArraySegment<byte>(bytes), out _), Is.True);
            Assert.That(AuthoritativeShotResultCodec.TryDeserialize(new ArraySegment<byte>(bytes), out _, out var decoded), Is.True);
            Assert.That(decoded.HitEnvironment, Is.True);
            Assert.That(decoded.DidHit, Is.False);
            var invalid = new AuthoritativeShotResult(Shot(), 29, true, 2, Fix.One, FixVec3.Zero, true);
            Assert.That(AuthoritativeShotResultCodec.TrySerialize(invalid, 2, new ArraySegment<byte>(bytes), out _), Is.False);
        }

        private static FixedWorld World() => new FixedWorld(new[]
        {
            new FixedWorldBox(1, 0, FixedSurface.Solid, new FixedAabb(
                new FixVec3(-Fix.One, Fix.Zero, Fix.FromInt(3)),
                new FixVec3(Fix.One, Fix.FromInt(2), Fix.FromInt(4))))
        });

        private static AuthoritativeShot Shot() => new AuthoritativeShot(1, 1, 100,
            WeaponSimulationConfig.DefaultRifle, new FixVec3(Fix.Zero, Fix.One, Fix.Zero), FixVec3.Forward);
        private static ServerCombatTarget Target(Fix z) =>
            new ServerCombatTarget(2, new FixVec3(Fix.Zero, Fix.Zero, z), Fix.Half, Fix.FromInt(2), Fix.FromInt(100));
    }
}

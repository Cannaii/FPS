using System;
using System.Collections.Generic;
using AFPS.Math;
using AFPS.NetCode.LagCompensation;
using AFPS.NetCode.Weapons;
using AFPS.Simulation.Weapons;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class DeterministicCombatTests
    {
        [TestCase(32768, true)]
        [TestCase(32769, false)]
        public void Hitscan_IncludesTangentAndRejectsOneRawOutside(int x, bool expected)
        {
            ServerCombatTarget target = Target(2, new FixVec3(Fix.FromRaw(x), Fix.Zero, Fix.FromInt(5)));
            Assert.That(ServerHitscanResolver.TryResolveFixed(Shot(), new[] { target }, out _, out _), Is.EqualTo(expected));
        }

        [TestCase(0, true)]
        [TestCase(-1, false)]
        [TestCase(131072, true)]
        [TestCase(131073, false)]
        public void Hitscan_IncludesSphericalCapBoundary(int originY, bool expected)
        {
            AuthoritativeShot shot = Shot(new FixVec3(Fix.Zero, Fix.FromRaw(originY), Fix.Zero));
            Assert.That(ServerHitscanResolver.TryResolveFixed(shot,
                new[] { Target(2, new FixVec3(Fix.Zero, Fix.Zero, Fix.FromInt(5))) }, out _, out _), Is.EqualTo(expected));
        }

        [TestCase(688128, true)]
        [TestCase(688129, false)]
        public void Hitscan_IncludesSegmentEndRadius(int targetZ, bool expected)
        {
            Assert.That(ServerHitscanResolver.TryResolveFixed(Shot(),
                new[] { Target(2, new FixVec3(Fix.Zero, Fix.Zero, Fix.FromRaw(targetZ))) }, out _, out _), Is.EqualTo(expected));
        }

        [Test]
        public void Hitscan_StableTieBreakAndUnroundedNearestDistance()
        {
            ServerCombatTarget a = Target(7, new FixVec3(Fix.Zero, Fix.Zero, Fix.FromInt(5)));
            ServerCombatTarget b = Target(2, a.PositionFixed);
            Assert.That(ServerHitscanResolver.TryResolveFixed(Shot(), new[] { a, b }, out ServerCombatTarget hit, out _), Is.True);
            Assert.That(hit.EntityId, Is.EqualTo(2));
            Assert.That(ServerHitscanResolver.TryResolveFixed(Shot(), new[] { b, a }, out hit, out _), Is.True);
            Assert.That(hit.EntityId, Is.EqualTo(2));
            b = Target(2, new FixVec3(Fix.Zero, Fix.Zero, Fix.FromRaw(a.PositionFixed.Z.Raw + 1)));
            Assert.That(ServerHitscanResolver.TryResolveFixed(Shot(), new[] { b, a }, out hit, out _), Is.True);
            Assert.That(hit.EntityId, Is.EqualTo(7));
        }

        [Test]
        public void Hitscan_HandlesVerticalParallelAxisAndSphereDegeneracy()
        {
            AuthoritativeShot vertical = new AuthoritativeShot(1, 1, 1, 1, WeaponFireMode.Hitscan,
                new FixVec3(Fix.Zero, Fix.FromInt(-2), Fix.Zero), FixVec3.Up,
                Fix.One, Fix.FromInt(10), Fix.Zero);
            ServerCombatTarget capsule = Target(2, FixVec3.Zero);
            Assert.That(ServerHitscanResolver.TryResolveFixed(vertical, new[] { capsule }, out _, out FixVec3 hit), Is.True);
            Assert.That(hit.Y, Is.EqualTo(Fix.Half));
            ServerCombatTarget sphere = new ServerCombatTarget(2, new FixVec3(Fix.Zero, Fix.Half, Fix.FromInt(5)),
                Fix.Half, Fix.One, Fix.One);
            Assert.That(ServerHitscanResolver.TryResolveFixed(Shot(), new[] { sphere }, out _, out hit), Is.True);
            Assert.That(hit.Z, Is.EqualTo(Fix.FromInt(5)));
        }

        [Test]
        public void Hitscan_RejectsInvalidDeadAndShooterTargets()
        {
            FixVec3 position = new FixVec3(Fix.Zero, Fix.Zero, Fix.FromInt(5));
            ServerCombatTarget[] targets =
            {
                Target(0, position), Target(1, position),
                new ServerCombatTarget(2, position, Fix.Half, Fix.FromInt(2), Fix.Zero),
                new ServerCombatTarget(3, position, Fix.Zero, Fix.FromInt(2), Fix.One),
                new ServerCombatTarget(4, position, Fix.One, Fix.One, Fix.One)
            };
            Assert.That(ServerHitscanResolver.TryResolveFixed(Shot(), targets, out _, out _), Is.False);
            Assert.That(ServerHitscanResolver.TryResolveFixed(Shot(), null, out _, out _), Is.False);
            AuthoritativeShot invalid = default;
            Assert.That(ServerHitscanResolver.TryResolveFixed(invalid, targets, out _, out _), Is.False);
        }

        [Test]
        public void Hitscan_PreservesLargeCoordinateLowBitsAndHandlesRawExtremes()
        {
            Fix x = Fix.FromRaw(655360001);
            AuthoritativeShot shot = Shot(new FixVec3(x, Fix.One, Fix.Zero));
            Assert.That(ServerHitscanResolver.TryResolveFixed(shot,
                new[] { Target(2, new FixVec3(x, Fix.Zero, Fix.FromRaw(327681))) }, out _, out FixVec3 point), Is.True);
            Assert.That(point.X.Raw, Is.EqualTo(655360001));
            Assert.That(point.Z.Raw, Is.EqualTo(327681));
            shot = new AuthoritativeShot(1, 1, 1, 1, WeaponFireMode.Hitscan,
                new FixVec3(Fix.MinValue, Fix.MinValue, Fix.MinValue), FixVec3.Right,
                Fix.MaxValue, Fix.MaxValue, Fix.Zero);
            ServerCombatTarget extreme = new ServerCombatTarget(2,
                new FixVec3(Fix.MaxValue, Fix.MaxValue, Fix.MaxValue), Fix.FromRaw(int.MaxValue / 2), Fix.MaxValue, Fix.One);
            Assert.That(ServerHitscanResolver.TryResolveFixed(shot, new[] { extreme }, out _, out _), Is.False);
            Assert.That(ServerHitscanResolver.PointAtDistance(
                new FixVec3(Fix.MaxValue, Fix.Zero, Fix.Zero), FixVec3.Right, int.MaxValue).X, Is.EqualTo(Fix.MaxValue));
        }

        [Test]
        public void Hitscan_MatchesIndependentConvexDistanceReference()
        {
            Random random = new Random(71937);
            for (int index = 0; index < 512; index++)
            {
                FixVec3 direction = new FixVec3(Fix.FromRaw(random.Next(-65536, 65537)),
                    Fix.FromRaw(random.Next(-65536, 65537)), Fix.FromRaw(random.Next(1, 65537))).Normalized();
                AuthoritativeShot shot = new AuthoritativeShot(1, 1, 1, 1, WeaponFireMode.Hitscan,
                    new FixVec3(Fix.Zero, Fix.One, Fix.Zero), direction, Fix.One, Fix.FromInt(10), Fix.Zero);
                ServerCombatTarget target = Target(2, new FixVec3(Fix.FromRaw(random.Next(-196608, 196609)),
                    Fix.FromRaw(random.Next(-65536, 65537)), Fix.FromRaw(random.Next(1, 655361))));
                double low = 0d;
                double high = 1d;
                // 点到竖直线段的平方距离是凸函数；三分搜索作为独立参考，不复用生产线段公式。
                for (int iteration = 0; iteration < 90; iteration++)
                {
                    double a = (2d * low + high) / 3d;
                    double b = (low + 2d * high) / 3d;
                    if (DistanceSquared(shot, target, a) <= DistanceSquared(shot, target, b))
                    {
                        high = b;
                    }
                    else
                    {
                        low = a;
                    }
                }
                double minimum = DistanceSquared(shot, target, (low + high) * 0.5d);
                Assert.That(ServerHitscanResolver.TryResolveFixed(shot, new[] { target }, out _, out _),
                    Is.EqualTo(minimum <= 0.25d), "case " + index);
            }
        }

        [Test]
        public void History_PreservesRawAcrossWrapAndRingOverwrite()
        {
            RewindHitboxHistory history = new RewindHitboxHistory(2);
            ServerCombatTarget target = new ServerCombatTarget(2,
                new FixVec3(Fix.FromRaw(655360001), Fix.MinValue, Fix.MaxValue),
                Fix.FromRaw(32769), Fix.FromRaw(131073), Fix.FromRaw(6553599));
            history.Record(uint.MaxValue, target);
            history.Record(0, Target(2, FixVec3.Zero));
            Assert.That(history.TryGetAtOrBefore(uint.MaxValue, 1, 3, out ServerCombatTarget sampled, out uint tick), Is.True);
            Assert.That(tick, Is.EqualTo(uint.MaxValue));
            Assert.That(sampled.PositionFixed, Is.EqualTo(target.PositionFixed));
            Assert.That(sampled.RadiusFixed, Is.EqualTo(target.RadiusFixed));
            Assert.That(sampled.HeightFixed, Is.EqualTo(target.HeightFixed));
            Assert.That(sampled.HealthFixed, Is.EqualTo(target.HealthFixed));
            history.Record(1, target);
            Assert.That(history.TryGetAtOrBefore(uint.MaxValue, 1, 3, out _, out _), Is.False);
            Assert.That(history.TryGetAtOrBefore(2, 1, 3, out _, out _), Is.False);
        }

        [Test]
        public void Health_AccumulatesSingleRawDamageAndClampsOverkill()
        {
            Fix health = Fix.FromInt(100);
            for (int index = 0; index < 1000; index++)
            {
                health = ServerCombatHealth.ApplyDamage(health, Fix.Epsilon);
            }
            Assert.That(health.Raw, Is.EqualTo(6552600));
            Assert.That(ServerCombatHealth.ApplyDamage(health, Fix.MaxValue), Is.EqualTo(Fix.Zero));
            Assert.Throws<ArgumentOutOfRangeException>(() => ServerCombatHealth.ApplyDamage(Fix.One, Fix.FromRaw(-1)));
        }

        [TestCase(163840, 2)]
        [TestCase(229376, 4)]
        [TestCase(int.MaxValue, 32768)]
        public void Health_DisplayBoundaryRoundsHalfToEven(int raw, int expected)
        {
            Assert.That(ServerCombatHealth.ToDisplayHealth(Fix.FromRaw(raw)), Is.EqualTo(expected));
        }

        [Test]
        public void Projectile_AccumulatesSubRawDistanceAndExpiresExactlyAtRange()
        {
            ServerProjectileSimulation simulation = new ServerProjectileSimulation();
            AuthoritativeShot shot = Projectile(FixVec3.Zero, Fix.Epsilon, Fix.Epsilon);
            Assert.That(simulation.Spawn(shot), Is.True);
            for (int index = 0; index < 65535; index++)
            {
                simulation.Advance(Fix.Epsilon, null, null);
            }
            Assert.That(simulation.Count, Is.EqualTo(1));
            simulation.Advance(Fix.Epsilon, null, null);
            Assert.That(simulation.Count, Is.Zero);
        }

        [Test]
        public void Projectile_PreservesImpactRawAndRewindMetadata()
        {
            Fix x = Fix.FromRaw(655360001);
            AuthoritativeShot shot = Projectile(new FixVec3(x, Fix.One, Fix.Zero), Fix.FromInt(20), Fix.FromInt(10));
            ServerProjectileSimulation simulation = new ServerProjectileSimulation();
            Assert.That(simulation.Spawn(shot, 17), Is.True);
            List<AuthoritativeShotResult> impacts = new List<AuthoritativeShotResult>();
            ServerCombatTarget target = Target(2, new FixVec3(x, Fix.Zero, Fix.FromInt(5)));
            Assert.That(simulation.Advance(Fix.One, new[] { target }, impacts), Is.EqualTo(1));
            Assert.That(impacts.Count, Is.EqualTo(1));
            Assert.That(impacts[0].HitPointFixed.X, Is.EqualTo(x));
            Assert.That(impacts[0].HitPointFixed.Z, Is.EqualTo(Fix.FromInt(5)));
            Assert.That(impacts[0].Shot.RewindTick, Is.EqualTo(9));
            Assert.That(impacts[0].TargetHealthFixed, Is.EqualTo(target.HealthFixed));
            Assert.That(impacts[0].RemainingAmmo, Is.EqualTo(17));
        }

        [Test]
        public void Projectile_HandlesLargeProductsAndWorldBoundaryWithoutOverflow()
        {
            ServerProjectileSimulation simulation = new ServerProjectileSimulation();
            simulation.Spawn(Projectile(FixVec3.Zero, Fix.MaxValue, Fix.MaxValue));
            simulation.Advance(Fix.MaxValue, null, null);
            Assert.That(simulation.Count, Is.Zero);
            simulation.Spawn(Projectile(new FixVec3(Fix.Zero, Fix.Zero, Fix.MaxValue), Fix.FromInt(10), Fix.One));
            simulation.Advance(Fix.One, null, null);
            Assert.That(simulation.Count, Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => simulation.Advance(Fix.Zero, null, null));
            Assert.That(simulation.Spawn(default), Is.False);
        }

        [Test]
        public void Combat_GoldenRawReplay()
        {
            Assert.That(DeterministicCombatScenario.Run(), Is.EqualTo(DeterministicCombatScenario.ExpectedHash));
        }

        private static double DistanceSquared(in AuthoritativeShot shot, in ServerCombatTarget target, double fraction)
        {
            double x = shot.OriginFixed.X.ToDouble()
                + shot.DirectionFixed.X.ToDouble() * shot.RangeFixed.ToDouble() * fraction
                - target.PositionFixed.X.ToDouble();
            double y = shot.OriginFixed.Y.ToDouble() + shot.DirectionFixed.Y.ToDouble() * shot.RangeFixed.ToDouble() * fraction;
            double z = shot.OriginFixed.Z.ToDouble()
                + shot.DirectionFixed.Z.ToDouble() * shot.RangeFixed.ToDouble() * fraction
                - target.PositionFixed.Z.ToDouble();
            double bottom = target.PositionFixed.Y.ToDouble() + target.RadiusFixed.ToDouble();
            double top = target.PositionFixed.Y.ToDouble() + target.HeightFixed.ToDouble() - target.RadiusFixed.ToDouble();
            y -= System.Math.Max(bottom, System.Math.Min(top, y));
            return x * x + y * y + z * z;
        }

        private static ServerCombatTarget Target(uint entityId, FixVec3 position) =>
            new ServerCombatTarget(entityId, position, Fix.Half, Fix.FromInt(2), Fix.FromInt(100));

        private static AuthoritativeShot Shot() => Shot(new FixVec3(Fix.Zero, Fix.One, Fix.Zero));

        private static AuthoritativeShot Shot(FixVec3 origin) => new AuthoritativeShot(1, 1, 1, 1,
            WeaponFireMode.Hitscan, origin, FixVec3.Forward, Fix.FromInt(20), Fix.FromInt(10), Fix.Zero);

        private static AuthoritativeShot Projectile(FixVec3 origin, Fix range, Fix speed) => new AuthoritativeShot(1, 5, 10, 2,
            WeaponFireMode.Projectile, origin, FixVec3.Forward, Fix.FromRaw(1966081), range, speed, 9);
    }
}

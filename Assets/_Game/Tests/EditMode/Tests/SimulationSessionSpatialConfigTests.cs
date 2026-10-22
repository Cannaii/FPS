using System;
using AFPS.Math;
using AFPS.Simulation.Characters;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class SimulationSessionSpatialConfigTests
    {
        private static FixVec3 V(int x, int y, int z) => new FixVec3(
            Fix.FromRaw(x), Fix.FromRaw(y), Fix.FromRaw(z));

        [TestCase(3, 4, 0, 5, true)]
        [TestCase(3, 4, 1, 5, false)]
        [TestCase(5, 0, 0, 5, true)]
        [TestCase(6, 0, 0, 5, false)]
        [TestCase(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, false)]
        [TestCase(int.MinValue, 0, 0, int.MaxValue, false)]
        public void InterestSphere_UsesExactSquaredRawAndIncludesBoundary(
            int x, int y, int z, int radius, bool expected)
        {
            var config = new SimulationSessionSpatialConfig(FixVec3.Zero, Fix.Zero, Fix.FromRaw(radius));
            Assert.That(config.TryMeasureInterestDistance(FixVec3.Zero, V(x, y, z), out ulong distance), Is.EqualTo(expected));
            if (expected)
            {
                Assert.That(distance, Is.EqualTo((ulong)((long)x * x + (long)y * y + (long)z * z)));
            }
            else
            {
                Assert.That(distance, Is.Zero);
            }
        }

        [Test]
        public void InterestSphere_ExtremeEndpointsDoNotOverflowBeforeRejection()
        {
            var config = new SimulationSessionSpatialConfig(FixVec3.Zero, Fix.Zero, Fix.MaxValue);
            var a = V(int.MinValue, int.MinValue, int.MinValue);
            var b = V(int.MaxValue, int.MaxValue, int.MaxValue);
            Assert.That(config.TryMeasureInterestDistance(a, b, out _), Is.False);
            Assert.That(config.TryMeasureInterestDistance(b, a, out _), Is.False);
            Assert.That(config.TryMeasureInterestDistance(a, a, out ulong distance), Is.True);
            Assert.That(distance, Is.Zero);
        }

        [Test]
        public void InterestSphere_MatchesIndependentDecimalOracle()
        {
            var random = new FixRandom(20261022);
            for (int i = 0; i < 1000; i++)
            {
                int radius = (int)(random.NextUInt() & int.MaxValue) | 1;
                var config = new SimulationSessionSpatialConfig(FixVec3.Zero, Fix.Zero, Fix.FromRaw(radius));
                var a = V(unchecked((int)random.NextUInt()), unchecked((int)random.NextUInt()), unchecked((int)random.NextUInt()));
                var b = V(unchecked((int)random.NextUInt()), unchecked((int)random.NextUInt()), unchecked((int)random.NextUInt()));
                if ((i & 1) == 0)
                {
                    // 半数样本落在原点附近，避免随机极值样本几乎全部被轴向筛掉。
                    a = FixVec3.Zero;
                    b = V(random.NextInt(-radius, radius), random.NextInt(-radius, radius), random.NextInt(-radius, radius));
                }
                decimal dx = (decimal)b.X.Raw - a.X.Raw;
                decimal dy = (decimal)b.Y.Raw - a.Y.Raw;
                decimal dz = (decimal)b.Z.Raw - a.Z.Raw;
                decimal oracle = dx * dx + dy * dy + dz * dz;
                bool expected = oracle <= (decimal)radius * radius;
                Assert.That(config.TryMeasureInterestDistance(a, b, out ulong actual), Is.EqualTo(expected), "sample " + i);
                if (expected)
                {
                    Assert.That(actual, Is.EqualTo((ulong)oracle));
                }
            }
        }

        [Test]
        public void Spawn_PreservesLowRawBitsAndOtherAxes()
        {
            var origin = V(655360001, -12345, 987654321);
            var config = new SimulationSessionSpatialConfig(origin, Fix.Epsilon, Fix.One);
            Assert.That(config.GetSpawnPosition(1), Is.EqualTo(origin));
            Assert.That(config.GetSpawnPosition(64), Is.EqualTo(V(655360064, -12345, 987654321)));
        }

        [Test]
        public void Spawn_UsesFullUnsignedEntityRangeWithoutFixConversion()
        {
            var config = new SimulationSessionSpatialConfig(V(int.MinValue, 0, 0), Fix.Epsilon, Fix.One);
            Assert.That(config.GetSpawnPosition(uint.MaxValue), Is.EqualTo(V(int.MaxValue - 1, 0, 0)));
        }

        [Test]
        public void Spawn_RejectsUnrepresentablePositionWithoutPartialOutput()
        {
            var config = new SimulationSessionSpatialConfig(V(int.MaxValue, 0, 0), Fix.Epsilon, Fix.One);
            Assert.That(config.TryGetSpawnPosition(2, out var position), Is.False);
            Assert.That(position, Is.EqualTo(FixVec3.Zero));
            Assert.That(config.TryGetSpawnPosition(0, out _), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => config.GetSpawnPosition(2));
            var huge = new SimulationSessionSpatialConfig(V(int.MaxValue, 0, 0), Fix.MaxValue, Fix.One);
            Assert.That(huge.TryGetSpawnPosition(uint.MaxValue, out _), Is.False);
        }

        [Test]
        public void InvalidConfigurationFailsBeforeUse()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SimulationSessionSpatialConfig(FixVec3.Zero, -Fix.Epsilon, Fix.One));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SimulationSessionSpatialConfig(FixVec3.Zero, Fix.Zero, Fix.Zero));
            Assert.That(default(SimulationSessionSpatialConfig).TryGetSpawnPosition(1, out _), Is.False);
            Assert.That(default(SimulationSessionSpatialConfig).TryMeasureInterestDistance(FixVec3.Zero, FixVec3.Zero, out _), Is.False);
        }
    }
}
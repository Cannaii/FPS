using System;
using AFPS.Math;
using AFPS.Simulation.Characters;
using AFPS.Simulation.Characters.Collision;
using AFPS.Simulation.Weapons;
using AFPS.Simulation.World;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class SimulationWorldPackageTests
    {
        [Test]
        public void FullRawConfigurationAndGeometry_RoundTripWithExactIdentityAndNoSharedBuffers()
        {
            var original = Context();
            byte[] package = SimulationWorldPackageCodec.Serialize(original);
            byte[] envelope = new byte[package.Length + 6];
            Array.Copy(package, 0, envelope, 3, package.Length);
            Assert.That(SimulationWorldPackageCodec.TryDeserialize(new ArraySegment<byte>(envelope, 3, package.Length),
                out var loaded, out var error), Is.True, error.ToString());
            Assert.That(loaded.Identity.Equals(original.Identity), Is.True);
            Assert.That(loaded.WeaponConfiguration.FireIntervalTicks, Is.EqualTo(uint.MaxValue));
            Assert.That(loaded.Configuration.Collision.MaxSlopeAngleFixedDegrees,
                Is.EqualTo(original.Configuration.Collision.MaxSlopeAngleFixedDegrees));
            Assert.That(loaded.SessionSpatial.SpawnOrigin, Is.EqualTo(original.SessionSpatial.SpawnOrigin));
            CollectionAssert.AreEqual(package, SimulationWorldPackageCodec.Serialize(loaded));
            Array.Clear(envelope, 0, envelope.Length);
            Assert.That(loaded.Identity.Equals(original.Identity), Is.True);
            CollectionAssert.AreEqual(original.World.CreateSnapshot(), loaded.World.CreateSnapshot());
        }

        [Test]
        public void EverySerializedConfigurationWord_IsProtectedByValidationOrCompleteFingerprint()
        {
            byte[] package = SimulationWorldPackageCodec.Serialize(Context());
            for (int word = 0; word < SimulationWorldPackageCodec.ConfigurationWordCount; word++)
            {
                byte[] altered = (byte[])package.Clone();
                altered[SimulationWorldPackageCodec.HeaderSize + word * 4] ^= 1;
                Assert.That(SimulationWorldPackageCodec.TryDeserialize(new ArraySegment<byte>(altered),
                    out var loaded, out _), Is.False, "配置字段=" + word);
                Assert.That(loaded, Is.Null);
            }
        }

        [TestCase(0, SimulationWorldPackageError.InvalidMagic)]
        [TestCase(4, SimulationWorldPackageError.UnsupportedVersion)]
        [TestCase(8, SimulationWorldPackageError.AlgorithmMismatch)]
        [TestCase(12, SimulationWorldPackageError.IdentityMismatch)]
        [TestCase(20, SimulationWorldPackageError.IdentityMismatch)]
        [TestCase(28, SimulationWorldPackageError.InvalidLength)]
        [TestCase(32, SimulationWorldPackageError.InvalidLength)]
        [TestCase(36, SimulationWorldPackageError.ReservedBits)]
        public void HeaderCorruption_FailsClosed(int offset, SimulationWorldPackageError expected)
        {
            byte[] package = SimulationWorldPackageCodec.Serialize(Context());
            package[offset] ^= 1;
            Assert.That(SimulationWorldPackageCodec.TryDeserialize(new ArraySegment<byte>(package),
                out var loaded, out var error), Is.False);
            Assert.That(error, Is.EqualTo(expected));
            Assert.That(loaded, Is.Null);
        }

        [Test]
        public void TruncationTrailingBytesAndOversizedLength_DoNotFallBackToDefaultWorld()
        {
            byte[] package = SimulationWorldPackageCodec.Serialize(Context());
            for (int length = 0; length < package.Length; length++)
            {
                Assert.That(SimulationWorldPackageCodec.TryDeserialize(new ArraySegment<byte>(package, 0, length),
                    out var loaded, out _), Is.False);
                Assert.That(loaded, Is.Null);
            }
            byte[] extra = new byte[package.Length + 1];
            Array.Copy(package, extra, package.Length);
            Assert.That(SimulationWorldPackageCodec.TryDeserialize(new ArraySegment<byte>(extra), out _, out _), Is.False);
            Write(package, 32, uint.MaxValue);
            Assert.That(SimulationWorldPackageCodec.TryDeserialize(new ArraySegment<byte>(package), out _, out _), Is.False);
            Assert.That(SimulationWorldPackageCodec.TryDeserialize(default, out _, out _), Is.False);
        }

        [TestCase(62, 65537)]
        [TestCase(63, 257)]
        public void NonCanonicalIntegerEnums_CannotNarrowToValidValues(int word, int raw)
        {
            byte[] package = SimulationWorldPackageCodec.Serialize(Context());
            Write(package, SimulationWorldPackageCodec.HeaderSize + word * 4, (uint)raw);
            Assert.That(SimulationWorldPackageCodec.TryDeserialize(new ArraySegment<byte>(package),
                out _, out var error), Is.False);
            Assert.That(error, Is.EqualTo(SimulationWorldPackageError.InvalidConfiguration));
        }

        [Test]
        public void CorruptGeometry_IsRejectedBeforeContextAssembly()
        {
            byte[] package = SimulationWorldPackageCodec.Serialize(Context());
            package[SimulationWorldPackageCodec.HeaderSize + SimulationWorldPackageCodec.ConfigurationSize] ^= 1;
            Assert.That(SimulationWorldPackageCodec.TryDeserialize(new ArraySegment<byte>(package),
                out _, out var error), Is.False);
            Assert.That(error, Is.EqualTo(SimulationWorldPackageError.InvalidGeometry));
            Assert.Throws<ArgumentNullException>(() => SimulationWorldPackageCodec.Serialize(null));
        }

        private static SimulationWorldContext Context()
        {
            var world = new FixedWorld(null, new[]
            {
                new FixedWorldPlane(1, 8, FixedSurface.Solid | FixedSurface.Ground, FixedAxis.Y, Fix.Zero, true)
            });
            var collision = new CharacterCollisionConfig(Fix.Half, Fix.FromInt(2), Fix.FromRaw(701),
                Fix.FromRaw(6543), Fix.FromRaw(19999), Fix.FromRaw(5301862), 5);
            var configuration = new PlayerSimulationConfig(Fix.FromRaw(400001), Fix.FromRaw(1400001),
                Fix.FromRaw(1300001), Fix.FromRaw(600001), collision,
                new PlayerSprintConfig(Fix.FromRaw(100001), Fix.FromRaw(20001), Fix.FromRaw(17001), Fix.FromRaw(13001)),
                new PlayerDashConfig(Fix.FromRaw(1000001), Fix.FromRaw(16001), Fix.FromRaw(100001), 3, Fix.FromRaw(14001)),
                new PlayerJumpConfig(Fix.FromRaw(6001), 2),
                new PlayerWallConfig(Fix.FromRaw(400001), Fix.FromRaw(600001), Fix.FromRaw(1500001), Fix.FromRaw(18001), Fix.FromRaw(10001)),
                new PlayerStanceConfig(Fix.FromRaw(80001), Fix.FromRaw(33001), Fix.FromRaw(700001),
                    Fix.FromRaw(410001), Fix.FromRaw(47001), Fix.FromRaw(540001), Fix.FromRaw(140001)),
                new PlayerWallRunConfig(Fix.FromRaw(600001), Fix.FromRaw(1900001), Fix.FromRaw(110001),
                    Fix.FromRaw(17001), Fix.FromRaw(140001), Fix.FromRaw(140001), Fix.FromRaw(22001), Fix.FromRaw(10001), Fix.FromRaw(18001)),
                new PlayerClimbConfig(Fix.FromRaw(200001), Fix.FromRaw(14001), Fix.FromRaw(200001), Fix.FromRaw(18001), Fix.FromRaw(35001)),
                new PlayerGrappleConfig(Fix.FromRaw(2000001), Fix.FromRaw(1000001), Fix.FromRaw(140001),
                    Fix.FromRaw(1600001), Fix.FromRaw(540001), Fix.FromRaw(100001), Fix.FromRaw(110001)));
            var spatial = new SimulationSessionSpatialConfig(new FixVec3(Fix.FromRaw(123), Fix.FromRaw(-234), Fix.FromRaw(345)),
                Fix.FromRaw(170001), Fix.FromRaw(10000001));
            var weapon = new WeaponSimulationConfig(1, WeaponFireMode.Hitscan, 37, uint.MaxValue,
                Fix.FromRaw(1400001), Fix.FromRaw(14000001), Fix.Zero, Fix.FromRaw(110001));
            return new SimulationWorldContext(world, configuration, Fix.FromRaw(1311), 1 << 8, spatial, weapon);
        }

        private static void Write(byte[] bytes, int offset, uint value)
        {
            for (int i = 0; i < 4; i++)
            {
                bytes[offset + i] = unchecked((byte)(value >> (i * 8)));
            }
        }
    }
}

using System;
using AFPS.Math;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.Weapons;
using AFPS.Simulation.Characters;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class SessionRecoveryCodecTests
    {
        [Test]
        public void AtomicSeed_RoundTripsRawStateAndMetadataInsideArraySlice()
        {
            var seed = Seed();
            byte[] bytes = new byte[SessionRecoveryCodec.SeedPacketSize + 6];
            bytes[0] = 0xCC;
            bytes[bytes.Length - 1] = 0xCC;
            Assert.That(SessionRecoveryCodec.TryWriteSeed(seed, uint.MaxValue,
                new ArraySegment<byte>(bytes, 3, SessionRecoveryCodec.SeedPacketSize), out int count), Is.True);
            Assert.That(count, Is.EqualTo(223));
            Assert.That(SessionRecoveryCodec.TryReadSeed(new ArraySegment<byte>(bytes, 3, count),
                out uint epoch, out var decoded), Is.True);
            Assert.That(epoch, Is.EqualTo(uint.MaxValue));
            Assert.That(decoded.Attempt, Is.EqualTo(seed.Attempt));
            Assert.That(decoded.EntityId, Is.EqualTo(seed.EntityId));
            Assert.That(decoded.LastShotSequence, Is.EqualTo(uint.MaxValue));
            Assert.That(decoded.ReplayWindow, Is.EqualTo(16));
            Assert.That(decoded.Movement.State.RawEquals(seed.Movement.State), Is.True);
            Assert.That(decoded.Weapon.Ammo, Is.EqualTo(17));
            Assert.That(bytes[0], Is.EqualTo(0xCC));
            Assert.That(bytes[bytes.Length - 1], Is.EqualTo(0xCC));
        }

        [Test]
        public void AtomicSeed_RejectsTruncationsTrailingDataAndMixedNestedEpoch()
        {
            byte[] bytes = new byte[SessionRecoveryCodec.SeedPacketSize];
            Assert.That(SessionRecoveryCodec.TryWriteSeed(Seed(), 5, new ArraySegment<byte>(bytes), out _), Is.True);
            for (int length = 0; length < bytes.Length; length++)
            {
                Assert.That(SessionRecoveryCodec.TryReadSeed(new ArraySegment<byte>(bytes, 0, length),
                    out uint epoch, out var seed), Is.False);
                Assert.That(epoch, Is.Zero);
                Assert.That(seed.EntityId, Is.Zero);
            }
            byte[] extra = new byte[bytes.Length + 1];
            Array.Copy(bytes, extra, bytes.Length);
            Assert.That(SessionRecoveryCodec.TryReadSeed(new ArraySegment<byte>(extra), out _, out _), Is.False);
            bytes[PacketHeader.Size + 16 + 8]++;
            Assert.That(SessionRecoveryCodec.TryReadSeed(new ArraySegment<byte>(bytes), out _, out _), Is.False);
            Assert.That(SessionRecoveryCodec.TryReadSeed(default, out _, out _), Is.False);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void AtomicSeed_RejectsInconsistentEntityFrameAckAndWindow(int mutation)
        {
            var original = Seed();
            var weapon = new AuthoritativeWeaponState(mutation == 0 ? 2u : 1,
                mutation == 1 ? 101u : 100, mutation == 2 ? 18u : 17,
                17, 30, 0, true);
            var invalid = new SessionRecoverySeed(2, 1, 0, original.Movement, weapon, mutation == 3 ? 33u : 16);
            byte[] bytes = new byte[SessionRecoveryCodec.SeedPacketSize];
            Assert.That(SessionRecoveryCodec.TryWriteSeed(invalid, 5, new ArraySegment<byte>(bytes), out int count), Is.False);
            Assert.That(count, Is.Zero);
        }

        [TestCase(NetworkMessageType.SessionRecoveryAck)]
        [TestCase(NetworkMessageType.SessionRecoveryConfirmed)]
        public void Receipt_BindsAttemptEpochEntityAndRejectsZeroOrWrongType(NetworkMessageType type)
        {
            byte[] bytes = new byte[SessionRecoveryCodec.ReceiptPacketSize];
            Assert.That(SessionRecoveryCodec.TryWriteReceipt(type, uint.MaxValue, 5, 1,
                new ArraySegment<byte>(bytes), out int count), Is.True);
            Assert.That(count, Is.EqualTo(20));
            Assert.That(SessionRecoveryCodec.TryReadReceipt(new ArraySegment<byte>(bytes), type,
                out uint attempt, out uint epoch, out uint entity), Is.True);
            Assert.That(attempt, Is.EqualTo(uint.MaxValue));
            Assert.That(epoch, Is.EqualTo(5));
            Assert.That(entity, Is.EqualTo(1));
            Assert.That(SessionRecoveryCodec.TryReadReceipt(new ArraySegment<byte>(bytes),
                NetworkMessageType.InputCommandBatch, out _, out _, out _), Is.False);
            for (int offset = 8; offset < bytes.Length; offset += 4)
            {
                byte[] bad = (byte[])bytes.Clone();
                Array.Clear(bad, offset, 4);
                Assert.That(SessionRecoveryCodec.TryReadReceipt(new ArraySegment<byte>(bad), type,
                    out attempt, out epoch, out entity), Is.False);
                Assert.That(attempt | epoch | entity, Is.Zero);
            }
            Assert.That(SessionRecoveryCodec.TryWriteReceipt(type, 0, 1, 1,
                new ArraySegment<byte>(bytes), out count), Is.False);
            Assert.That(count, Is.Zero);
        }

        private static SessionRecoverySeed Seed()
        {
            var state = new PlayerState
            {
                Tick = 17, PositionFixed = new FixVec3(Fix.FromRaw(123), Fix.FromInt(2), Fix.FromRaw(-123)),
                StaminaSpentFixed = Fix.Half, DashSpentCharges = 1, DashRechargeRemainingFixed = Fix.Half
            };
            return new SessionRecoverySeed(2, 1, uint.MaxValue,
                new AuthoritativePlayerState(100, 17, state),
                new AuthoritativeWeaponState(1, 100, 17, 17, 30, 0, true), 16);
        }
    }
}

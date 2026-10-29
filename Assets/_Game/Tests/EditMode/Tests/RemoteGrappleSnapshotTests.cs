using System;
using AFPS.Math;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.SnapshotInterpolation;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class RemoteGrappleSnapshotTests
    {
        private static readonly FixVec3 Anchor = new FixVec3(Fix.FromInt(2), Fix.FromInt(5), Fix.FromInt(10));
        private static RemotePlayerMovementSnapshot Snapshot(uint tick, bool active, FixVec3? point = null) =>
            new RemotePlayerMovementSnapshot(1, tick, -FixVec3.Right * Fix.FromInt(5), Fix.Zero, Fix.Zero, FixVec3.Zero,
                isGrappling: active, grapplePoint: point ?? (active ? Anchor : FixVec3.Zero));

        [TestCase(false)]
        [TestCase(true)]
        public void Full_RoundtripsInactiveAndActiveWorldOriginAnchor(bool active)
        {
            var source = Snapshot(10, active, FixVec3.Zero);
            var bytes = new byte[RemotePlayerMovementSnapshotCodec.PacketSize];
            Assert.That(RemotePlayerMovementSnapshotCodec.TrySerialize(source, 1, new ArraySegment<byte>(bytes), out int count), Is.True);
            Assert.That(count, Is.EqualTo(65));
            Assert.That(RemotePlayerMovementSnapshotCodec.TryDeserialize(new ArraySegment<byte>(bytes), out _, out var decoded), Is.True);
            Assert.That(decoded.RawEquals(source), Is.True);
        }

        [Test]
        public void Delta_OnlyAnchorChangesPreserveSingleRawAndUnchangedBaseline()
        {
            var baseline = Snapshot(uint.MaxValue, true);
            var current = Snapshot(0, true, Anchor + FixVec3.Up * Fix.Epsilon);
            var bytes = new byte[RemotePlayerMovementSnapshotDeltaCodec.MaximumPacketSize];
            Assert.That(RemotePlayerMovementSnapshotDeltaCodec.TrySerialize(current, baseline, 2, new ArraySegment<byte>(bytes), out int count), Is.True);
            Assert.That(count, Is.EqualTo(37));
            Assert.That(bytes[24], Is.EqualTo(16));
            Assert.That(RemotePlayerMovementSnapshotDeltaCodec.TryDeserialize(new ArraySegment<byte>(bytes, 0, count), baseline,
                out _, out var decoded), Is.True);
            Assert.That(decoded.RawEquals(current), Is.True);
            Assert.That(RemotePlayerMovementSnapshotDeltaCodec.TrySerialize(baseline, baseline, 3, new ArraySegment<byte>(bytes), out count), Is.True);
            Assert.That(count, Is.EqualTo(25));
            Assert.That(RemotePlayerMovementSnapshotDeltaCodec.TryDeserialize(new ArraySegment<byte>(bytes, 0, count), baseline,
                out _, out decoded), Is.True);
            Assert.That(decoded.GrapplePointFixed, Is.EqualTo(Anchor));
            Assert.That(decoded.IsGrappling, Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DetachDelta_ClearsAnchorIncludingZeroWorldPoint(bool zeroPoint)
        {
            var baseline = Snapshot(10, true, zeroPoint ? FixVec3.Zero : Anchor);
            var current = Snapshot(11, false);
            var bytes = new byte[RemotePlayerMovementSnapshotDeltaCodec.MaximumPacketSize];
            Assert.That(RemotePlayerMovementSnapshotDeltaCodec.TrySerialize(current, baseline, 2, new ArraySegment<byte>(bytes), out int count), Is.True);
            Assert.That(count, Is.EqualTo(zeroPoint ? 26 : 38));
            Assert.That(RemotePlayerMovementSnapshotDeltaCodec.TryDeserialize(new ArraySegment<byte>(bytes, 0, count), baseline,
                out _, out var decoded), Is.True);
            Assert.That(decoded.RawEquals(current), Is.True);
        }

        [Test]
        public void Decoder_RejectsInactiveAnchorResidueWithoutPublishingState()
        {
            var source = Snapshot(10, true);
            var bytes = new byte[RemotePlayerMovementSnapshotCodec.PacketSize];
            RemotePlayerMovementSnapshotCodec.TrySerialize(source, 1, new ArraySegment<byte>(bytes), out _);
            bytes[52] = 0;
            Assert.That(RemotePlayerMovementSnapshotCodec.TryDeserialize(new ArraySegment<byte>(bytes), out _, out var decoded), Is.False);
            Assert.That(decoded.EntityId, Is.Zero);
            // 构造只清活动位、不清基线锚点的合法长度delta，不能把残留锚点当成正常脱钩。
            var malformed = new byte[26];
            PacketHeaderCodec.TryWrite(new PacketHeader(NetworkMessageType.RemotePlayerSnapshotDelta, 14, 2),
                new ArraySegment<byte>(malformed));
            Buffer.BlockCopy(BitConverter.GetBytes(1u), 0, malformed, 12, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(11u), 0, malformed, 16, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(10u), 0, malformed, 20, 4);
            malformed[24] = 8;
            malformed[25] = 0;
            Assert.That(RemotePlayerMovementSnapshotDeltaCodec.TryDeserialize(new ArraySegment<byte>(malformed), source,
                out _, out decoded), Is.False);
            Assert.That(decoded.EntityId, Is.Zero);
        }

        [Test]
        public void InvalidWideSpanAndCrouchedGrapple_AreRejected()
        {
            var bytes = new byte[RemotePlayerMovementSnapshotCodec.PacketSize];
            var invalid = new RemotePlayerMovementSnapshot(1, 10, new FixVec3(Fix.MinValue, Fix.Zero, Fix.Zero),
                Fix.Zero, Fix.Zero, FixVec3.Zero, isGrappling: true, grapplePoint: new FixVec3(Fix.MaxValue, Fix.Zero, Fix.Zero));
            Assert.That(RemotePlayerMovementSnapshotCodec.TrySerialize(invalid, 1, new ArraySegment<byte>(bytes), out _), Is.False);
            invalid = new RemotePlayerMovementSnapshot(1, 10, FixVec3.Zero, Fix.Zero, Fix.Zero, FixVec3.Zero,
                isCrouched: true, isGrappling: true, grapplePoint: Anchor);
            Assert.That(RemotePlayerMovementSnapshotCodec.TrySerialize(invalid, 1, new ArraySegment<byte>(bytes), out _), Is.False);
        }
    }
}

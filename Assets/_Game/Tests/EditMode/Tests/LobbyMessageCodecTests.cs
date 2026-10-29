using System;
using AFPS.NetCode.Match;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Protocol;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class LobbyMessageCodecTests
    {
        [Test]
        public void ReadyWrapperRoundTripsSliceAndRejectsEveryTruncationAndTrailingByte()
        {
            var command = new LobbyReadyCommand(1, 2, 3, true);
            var bytes = new byte[LobbyMessageCodec.ReadyPacketSize + 4];
            var slice = new ArraySegment<byte>(bytes, 2, LobbyMessageCodec.ReadyPacketSize);
            Assert.That(LobbyMessageCodec.TryWriteReady(command, slice), Is.True);
            Assert.That(bytes[6], Is.EqualTo(PacketHeader.CurrentProtocolVersion));
            Assert.That(LobbyMessageCodec.TryReadReady(slice, out var decoded), Is.True);
            Assert.That(decoded.MembershipEpoch, Is.EqualTo(2));
            for (int length = 0; length < slice.Count; length++)
            {
                Assert.That(LobbyMessageCodec.TryReadReady(new ArraySegment<byte>(bytes, 2, length), out _), Is.False);
            }
            Assert.That(LobbyMessageCodec.TryReadReady(new ArraySegment<byte>(bytes, 2, slice.Count + 1), out _), Is.False);
            Assert.That(LobbyMessageCodec.TryWriteReady(command, default), Is.False);
        }

        [Test]
        public void SnapshotWrapperPreservesCanonicalStateAndRejectsWrongTypeVersionAndZeroSequence()
        {
            var snapshot = new LobbySnapshot(1, 100, 0, LobbyPhase.Waiting,
                new[] { new LobbyPlayerState(1, 1, true, false, false) });
            var bytes = new byte[LobbyMessageCodec.MaximumSnapshotPacketSize];
            Assert.That(LobbyMessageCodec.TryWriteSnapshot(snapshot, uint.MaxValue,
                new ArraySegment<byte>(bytes), out int written), Is.True);
            var slice = new ArraySegment<byte>(bytes, 0, written);
            Assert.That(LobbyMessageCodec.TryReadSnapshot(slice, out uint sequence, out var decoded), Is.True);
            Assert.That(sequence, Is.EqualTo(uint.MaxValue));
            Assert.That(decoded.ServerTick, Is.EqualTo(100));
            bytes[4] = 26;
            Assert.That(LobbyMessageCodec.TryReadSnapshot(slice, out _, out _), Is.False);
            bytes[4] = PacketHeader.CurrentProtocolVersion;
            bytes[5] = (byte)NetworkMessageType.LobbyReady;
            Assert.That(LobbyMessageCodec.TryReadSnapshot(slice, out _, out _), Is.False);
            Assert.That(LobbyMessageCodec.TryWriteSnapshot(snapshot, 0, new ArraySegment<byte>(bytes), out _), Is.False);
        }
    }
}

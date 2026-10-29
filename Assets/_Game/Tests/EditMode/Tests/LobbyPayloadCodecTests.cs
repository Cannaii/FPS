using System;
using AFPS.NetCode.Match;
using AFPS.NetCode.Messages;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class LobbyPayloadCodecTests
    {
        [Test]
        public void ReadyHasStableLittleEndianPayloadAndDoesNotWriteOutsideSlice()
        {
            var bytes = new byte[17];
            bytes[0] = 123;
            bytes[16] = 234;
            var slice = new ArraySegment<byte>(bytes, 2, 13);
            var command = new LobbyReadyCommand(0x01020304, 0x11121314, 0x21222324, true);
            Assert.That(LobbyPayloadCodec.TryWriteReady(command, slice), Is.True);
            CollectionAssert.AreEqual(new byte[] { 4, 3, 2, 1, 0x14, 0x13, 0x12, 0x11,
                0x24, 0x23, 0x22, 0x21, 1 }, slice);
            Assert.That(bytes[0], Is.EqualTo(123));
            Assert.That(bytes[16], Is.EqualTo(234));
            Assert.That(LobbyPayloadCodec.TryReadReady(slice, out var decoded), Is.True);
            Assert.That(decoded.RoundEpoch, Is.EqualTo(command.RoundEpoch));
            Assert.That(decoded.MembershipEpoch, Is.EqualTo(command.MembershipEpoch));
            Assert.That(decoded.Sequence, Is.EqualTo(command.Sequence));
            Assert.That(decoded.Ready, Is.True);
        }

        [Test]
        public void ReadyRejectsAllTruncationTrailingDataZeroTokensAndInvalidBoolean()
        {
            var bytes = new byte[14];
            var command = new LobbyReadyCommand(1, 2, 3, true);
            for (int length = 0; length <= 14; length++)
            {
                LobbyPayloadCodec.TryWriteReady(command, new ArraySegment<byte>(bytes));
                if (length != 13)
                {
                    Assert.That(LobbyPayloadCodec.TryReadReady(
                        new ArraySegment<byte>(bytes, 0, length), out var rejected), Is.False);
                    Assert.That(rejected.Sequence, Is.Zero);
                }
            }
            foreach (int offset in new[] { 0, 4, 8, 12 })
            {
                LobbyPayloadCodec.TryWriteReady(command, new ArraySegment<byte>(bytes));
                if (offset == 12)
                {
                    bytes[offset] = 2;
                }
                else
                {
                    Array.Clear(bytes, offset, 4);
                }
                Assert.That(LobbyPayloadCodec.TryReadReady(new ArraySegment<byte>(bytes, 0, 13), out _), Is.False);
            }
            Assert.That(LobbyPayloadCodec.TryWriteReady(default, new ArraySegment<byte>(bytes)), Is.False);
            Assert.That(LobbyPayloadCodec.TryReadReady(default, out _), Is.False);
        }

        [Test]
        public void SnapshotHasStableGoldenAndCopiesCallerArray()
        {
            var players = new[] { new LobbyPlayerState(0x21222324, 0x31323334, true, false, true, 1) };
            var snapshot = new LobbySnapshot(0x01020304, 0x11121314, 0, LobbyPhase.Waiting, players);
            players[0] = default;
            var bytes = new byte[27];
            Assert.That(LobbyPayloadCodec.TryWriteSnapshot(snapshot, new ArraySegment<byte>(bytes), out int written), Is.True);
            Assert.That(written, Is.EqualTo(27));
            CollectionAssert.AreEqual(new byte[]
            {
                4, 3, 2, 1, 0x14, 0x13, 0x12, 0x11, 0, 0, 0, 0, 0, 1,
                0x24, 0x23, 0x22, 0x21, 0x34, 0x33, 0x32, 0x31, 1, 0, 0, 0, 5
            }, bytes);
            Assert.That(LobbyPayloadCodec.TryReadSnapshot(new ArraySegment<byte>(bytes), out var decoded), Is.True);
            Assert.That(decoded.GetPlayer(0).Ready, Is.True);
            Assert.That(decoded.GetPlayer(0).ReadySequence, Is.EqualTo(1));
        }

        [Test]
        public void MaximumRosterFitsAndEveryTruncationAndTrailingByteIsRejected()
        {
            var players = new LobbyPlayerState[64];
            for (int index = 0; index < players.Length; index++)
            {
                players[index] = new LobbyPlayerState((uint)index + 1, (uint)index + 100, true, false, false);
            }
            var snapshot = new LobbySnapshot(1, uint.MaxValue, 0, LobbyPhase.Waiting, players);
            var bytes = new byte[LobbyPayloadCodec.MaximumSnapshotSize + 1];
            Assert.That(LobbyPayloadCodec.TryWriteSnapshot(snapshot, new ArraySegment<byte>(bytes), out int written), Is.True);
            Assert.That(written, Is.EqualTo(846));
            Assert.That(LobbyPayloadCodec.TryReadSnapshot(new ArraySegment<byte>(bytes, 0, written), out var decoded), Is.True);
            Assert.That(decoded.PlayerCount, Is.EqualTo(64));
            for (int length = 0; length < bytes.Length; length++)
            {
                if (length != written)
                {
                    Assert.That(LobbyPayloadCodec.TryReadSnapshot(new ArraySegment<byte>(bytes, 0, length), out _), Is.False);
                }
            }
            Assert.That(LobbyPayloadCodec.TryReadSnapshot(new ArraySegment<byte>(bytes), out _), Is.False);
        }

        [TestCase(0, 0)]
        [TestCase(12, 4)]
        [TestCase(13, 65)]
        [TestCase(14, 0)]
        [TestCase(18, 0)]
        [TestCase(22, 0)]
        [TestCase(26, 8)]
        [TestCase(26, 2)]
        [TestCase(26, 4)]
        [TestCase(26, 7)]
        public void MalformedSnapshotRejectsUnknownFlagsAndImpossibleMembership(int offset, int value)
        {
            var snapshot = new LobbySnapshot(1, 100, 0, LobbyPhase.Waiting,
                new[] { new LobbyPlayerState(1, 1, true, false, true, 1) });
            var bytes = new byte[27];
            LobbyPayloadCodec.TryWriteSnapshot(snapshot, new ArraySegment<byte>(bytes), out _);
            bytes[offset] = (byte)value;
            Assert.That(LobbyPayloadCodec.TryReadSnapshot(new ArraySegment<byte>(bytes), out var rejected), Is.False);
            Assert.That(rejected, Is.Null);
        }

        [Test]
        public void CountdownRequiresPositiveTimeTwoReadyPlayersAndCanonicalEntityOrder()
        {
            var ready = new[]
            {
                new LobbyPlayerState(1, 1, true, false, true, 1),
                new LobbyPlayerState(2, 2, true, false, true, 1)
            };
            var bytes = new byte[64];
            var valid = new LobbySnapshot(1, 100, 150, LobbyPhase.Countdown, ready);
            Assert.That(LobbyPayloadCodec.TryWriteSnapshot(valid, new ArraySegment<byte>(bytes), out int written), Is.True);
            Assert.That(LobbyPayloadCodec.TryReadSnapshot(new ArraySegment<byte>(bytes, 0, written), out _), Is.True);
            foreach (var invalid in new[]
            {
                new LobbySnapshot(1, 100, 0, LobbyPhase.Countdown, ready),
                new LobbySnapshot(1, 100, 1, LobbyPhase.Waiting, ready),
                new LobbySnapshot(1, 100, 1, LobbyPhase.Countdown, new[] { ready[0] }),
                new LobbySnapshot(1, 100, 0, LobbyPhase.Finished, ready),
                new LobbySnapshot(1, 100, 0, LobbyPhase.Waiting, new[] { ready[1], ready[0] }),
                new LobbySnapshot(1, 100, 0, LobbyPhase.Waiting, new[] { ready[0], ready[0] })
            })
            {
                Assert.That(LobbyPayloadCodec.TryWriteSnapshot(invalid, new ArraySegment<byte>(bytes), out written), Is.False);
                Assert.That(written, Is.Zero);
            }
        }
    }
}

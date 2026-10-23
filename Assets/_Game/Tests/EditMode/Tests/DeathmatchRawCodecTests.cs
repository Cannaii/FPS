using System;
using AFPS.Math;
using AFPS.NetCode.Match;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Protocol;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class DeathmatchRawCodecTests
    {
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(6553599)]
        [TestCase(int.MaxValue)]
        public void State_PreservesRawHealthAndIntegerDisplayBoundary(int raw)
        {
            DeathmatchPlayerState player = new DeathmatchPlayerState(1, Fix.FromRaw(raw), 2, 3, 4, raw > 0);
            DeathmatchState state = new DeathmatchState(DeathmatchPhase.Playing, uint.MaxValue, 8, 0, new[] { player });
            byte[] bytes = new byte[DeathmatchStateCodec.GetPacketSize(1) + 4];
            ArraySegment<byte> packet = new ArraySegment<byte>(bytes, 2, bytes.Length - 4);
            Assert.That(DeathmatchStateCodec.TrySerialize(state, 7, packet, out int written), Is.True);
            Assert.That(written, Is.EqualTo(41));
            // 固定负载 14B 后是 EntityId 4B，再是 int32 Raw 血量，小端。
            for (int index = 0; index < 4; index++)
            {
                Assert.That(bytes[2 + PacketHeader.Size + 18 + index],
                    Is.EqualTo(unchecked((byte)((uint)raw >> (index * 8)))));
            }
            Assert.That(DeathmatchStateCodec.TryDeserialize(packet, out var header, out var decoded), Is.True);
            Assert.That(header.Sequence, Is.EqualTo(7));
            Assert.That(decoded.GetPlayer(0).HealthFixed.Raw, Is.EqualTo(raw));
            Assert.That(decoded.GetPlayer(0).Health, Is.EqualTo(player.Health));
            Assert.That(decoded.GetPlayer(0).IsAlive, Is.EqualTo(raw > 0));
        }

        [Test]
        public void State_RejectsNegativeHealthAndOldVersionWithoutPartialState()
        {
            DeathmatchState state = new DeathmatchState(DeathmatchPhase.Playing, 1, 1, 0,
                new[] { new DeathmatchPlayerState(1, Fix.One, 0, 0, 0, true) });
            byte[] bytes = new byte[DeathmatchStateCodec.GetPacketSize(1)];
            Assert.That(DeathmatchStateCodec.TrySerialize(state, 1, new ArraySegment<byte>(bytes), out _), Is.True);
            bytes[4] = 5;
            Assert.That(DeathmatchStateCodec.TryDeserialize(new ArraySegment<byte>(bytes), out _, out _), Is.False);
            bytes[4] = 8;
            bytes[PacketHeader.Size + 21] = 128;
            Assert.That(DeathmatchStateCodec.TryDeserialize(new ArraySegment<byte>(bytes), out _, out var decoded), Is.False);
            Assert.That(decoded, Is.Null);
            DeathmatchState invalid = new DeathmatchState(DeathmatchPhase.Playing, 1, 1, 0,
                new[] { new DeathmatchPlayerState(1, Fix.FromRaw(-1), 0, 0, 0, true) });
            Assert.That(DeathmatchStateCodec.TrySerialize(invalid, 1, new ArraySegment<byte>(bytes), out int written), Is.False);
            Assert.That(written, Is.Zero);
        }

        [Test]
        public void State_MaximumRosterFitsAndTruncationIsRejected()
        {
            DeathmatchPlayerState[] players = new DeathmatchPlayerState[64];
            for (int index = 0; index < players.Length; index++)
            {
                players[index] = new DeathmatchPlayerState((uint)index + 1, Fix.FromRaw(index + 1), 0, 0, 0, true);
            }
            byte[] bytes = new byte[DeathmatchStateCodec.MaximumPacketSize];
            DeathmatchState state = new DeathmatchState(DeathmatchPhase.Playing, 0, 0, 0, players);
            Assert.That(DeathmatchStateCodec.TrySerialize(state, 1, new ArraySegment<byte>(bytes), out int written), Is.True);
            Assert.That(written, Is.EqualTo(986));
            Assert.That(DeathmatchStateCodec.TryDeserialize(new ArraySegment<byte>(bytes), out _, out var decoded), Is.True);
            Assert.That(decoded.GetPlayer(63).HealthFixed.Raw, Is.EqualTo(64));
            Assert.That(DeathmatchStateCodec.TryDeserialize(new ArraySegment<byte>(bytes, 0, bytes.Length - 1), out _, out _), Is.False);
        }
    }
}

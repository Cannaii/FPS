using System;
using AFPS.Math;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.SnapshotInterpolation;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class FixedRemoteMovementSnapshotTests
    {
        [TestCase(false, false, false)]
        [TestCase(false, true, false)]
        [TestCase(true, false, false)]
        [TestCase(true, true, false)]
        [TestCase(false, false, true)]
        [TestCase(false, true, true)]
        [TestCase(true, false, true)]
        [TestCase(true, true, true)]
        public void StateFlags_RoundtripAndUnchangedDeltaPreservesGrounded(bool teleport, bool grounded, bool crouched)
        {
            var baseline = new RemotePlayerMovementSnapshot(7, 10, FixVec3.Zero, Fix.Zero, Fix.Zero,
                FixVec3.Zero, !teleport, !grounded, !crouched);
            var current = new RemotePlayerMovementSnapshot(7, 11, FixVec3.Zero, Fix.Zero, Fix.Zero,
                FixVec3.Zero, teleport, grounded, crouched);
            var bytes = new byte[RemotePlayerMovementSnapshotDeltaCodec.MaximumPacketSize];
            Assert.That(RemotePlayerMovementSnapshotCodec.TrySerialize(current, 1, new ArraySegment<byte>(bytes), out int count), Is.True);
            Assert.That(count, Is.EqualTo(65));
            Assert.That(RemotePlayerMovementSnapshotCodec.TryDeserialize(new ArraySegment<byte>(bytes, 0, count),
                out _, out var full), Is.True);
            Assert.That(full.RawEquals(current), Is.True);
            Assert.That(RemotePlayerMovementSnapshotDeltaCodec.TrySerialize(current, baseline, 2,
                new ArraySegment<byte>(bytes), out count), Is.True);
            Assert.That(count, Is.EqualTo(26));
            Assert.That(RemotePlayerMovementSnapshotDeltaCodec.TryDeserialize(new ArraySegment<byte>(bytes, 0, count),
                baseline, out _, out var delta), Is.True);
            Assert.That(delta.RawEquals(current), Is.True);
            Assert.That(RemotePlayerMovementSnapshotDeltaCodec.TrySerialize(current, current, 3,
                new ArraySegment<byte>(bytes), out count), Is.True);
            Assert.That(count, Is.EqualTo(25));
            Assert.That(RemotePlayerMovementSnapshotDeltaCodec.TryDeserialize(new ArraySegment<byte>(bytes, 0, count),
                current, out _, out var unchanged), Is.True);
            Assert.That(unchanged.IsGrounded, Is.EqualTo(grounded));
            Assert.That(unchanged.IsCrouched, Is.EqualTo(crouched));
            Assert.That(unchanged.IsTeleport, Is.EqualTo(teleport));
        }

        private static FixVec3 V(int x, int y, int z) => new FixVec3(
            Fix.FromRaw(x), Fix.FromRaw(y), Fix.FromRaw(z));

        private static RemotePlayerMovementSnapshot Baseline() => new RemotePlayerMovementSnapshot(
            7, uint.MaxValue, V(int.MinValue, 0, int.MaxValue),
            Fix.FromRaw(1), Fix.FromRaw(-1), V(int.MaxValue, int.MinValue, 2));

        private static RemotePlayerMovementSnapshot Current(int mask) => new RemotePlayerMovementSnapshot(
            7, 0,
            (mask & 1) != 0 ? V(int.MaxValue, -1, int.MinValue) : Baseline().PositionFixed,
            (mask & 4) != 0 ? Fix.FromRaw(360 * 65536 - 1) : Baseline().YawFixedDegrees,
            (mask & 4) != 0 ? Fix.FromRatio(899, 10) : Baseline().PitchFixedDegrees,
            (mask & 2) != 0 ? V(int.MinValue, int.MaxValue, -2) : Baseline().VelocityFixed,
            (mask & 8) != 0);

        [Test]
        public void FullPacket_HasIndependentLittleEndianGoldenPayload()
        {
            var source = new RemotePlayerMovementSnapshot(0x11223344, 0xaabbccdd,
                V(0x01020304, int.MinValue, int.MaxValue),
                Fix.FromRaw(0x00010203), Fix.FromRaw(-0x00010203), V(-1, 1, -2), true);
            byte[] envelope = new byte[RemotePlayerMovementSnapshotCodec.PacketSize + 6];
            Assert.That(RemotePlayerMovementSnapshotCodec.TrySerialize(source, uint.MaxValue,
                new ArraySegment<byte>(envelope, 3, RemotePlayerMovementSnapshotCodec.PacketSize), out int count), Is.True);
            byte[] golden =
            {
                0x44, 0x33, 0x22, 0x11, 0xdd, 0xcc, 0xbb, 0xaa,
                4, 3, 2, 1, 0, 0, 0, 0x80, 0xff, 0xff, 0xff, 0x7f,
                0xff, 0xff, 0xff, 0xff, 1, 0, 0, 0, 0xfe, 0xff, 0xff, 0xff,
                3, 2, 1, 0, 0xfd, 0xfd, 0xfe, 0xff, 1
            };
            Assert.That(count, Is.EqualTo(65));
            Assert.That(envelope[3 + 4], Is.EqualTo(PacketHeader.CurrentProtocolVersion)); // v3 的毫米/厘米布局不能按新 Raw 解释。
            for (int i = 0; i < golden.Length; i++)
            {
                Assert.That(envelope[3 + PacketHeader.Size + i], Is.EqualTo(golden[i]), "offset " + i);
            }
            Assert.That(RemotePlayerMovementSnapshotCodec.TryDeserialize(
                new ArraySegment<byte>(envelope, 3, count), out PacketHeader header, out var decoded), Is.True);
            Assert.That(header.Sequence, Is.EqualTo(uint.MaxValue));
            Assert.That(decoded.RawEquals(source), Is.True);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        [TestCase(8)]
        [TestCase(9)]
        [TestCase(10)]
        [TestCase(11)]
        [TestCase(12)]
        [TestCase(13)]
        [TestCase(14)]
        [TestCase(15)]
        public void Delta_AllMasksPreserveRawExtremesAndTickWrap(int mask)
        {
            var baseline = Baseline();
            var current = Current(mask);
            int expectedSize = 25 + ((mask & 1) != 0 ? 12 : 0)
                + ((mask & 2) != 0 ? 12 : 0) + ((mask & 4) != 0 ? 8 : 0)
                + ((mask & 8) != 0 ? 1 : 0);
            byte[] envelope = new byte[expectedSize + 6];
            // 恰好够大的目标和非零 offset 也必须成功，无需预留最大包空间。
            Assert.That(RemotePlayerMovementSnapshotDeltaCodec.TrySerialize(current, baseline, 0x12345678,
                new ArraySegment<byte>(envelope, 3, expectedSize), out int count), Is.True);
            Assert.That(count, Is.EqualTo(expectedSize));
            Assert.That(envelope[3 + PacketHeader.Size + 12], Is.EqualTo(mask));
            Assert.That(RemotePlayerMovementSnapshotDeltaCodec.TryDeserialize(
                new ArraySegment<byte>(envelope, 3, count), baseline, out PacketHeader header, out var decoded), Is.True);
            Assert.That(header.Sequence, Is.EqualTo(0x12345678));
            Assert.That(decoded.RawEquals(current), Is.True);
        }

        [Test]
        public void Delta_PreservesSingleRawChangesInEveryNumericGroup()
        {
            var baseline = new RemotePlayerMovementSnapshot(1, 10, V(1234567890, 0, 0),
                Fix.FromRaw(123456), Fix.Zero, V(0, 987654321, 0));
            var current = new RemotePlayerMovementSnapshot(1, 11, V(1234567891, 0, 0),
                Fix.FromRaw(123457), Fix.FromRaw(-1), V(0, 987654322, 0));
            byte[] bytes = new byte[RemotePlayerMovementSnapshotDeltaCodec.MaximumPacketSize];
            Assert.That(RemotePlayerMovementSnapshotDeltaCodec.TrySerialize(current, baseline, 1, new ArraySegment<byte>(bytes), out int count), Is.True);
            Assert.That(RemotePlayerMovementSnapshotDeltaCodec.TryDeserialize(
                new ArraySegment<byte>(bytes, 0, count), baseline, out _, out var decoded), Is.True);
            Assert.That(decoded.RawEquals(current), Is.True);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        public void Full_RejectsInvalidPacketsWithoutPublishingPartialState(int corruption)
        {
            byte[] bytes = new byte[RemotePlayerMovementSnapshotCodec.PacketSize];
            RemotePlayerMovementSnapshotCodec.TrySerialize(Current(15), 1, new ArraySegment<byte>(bytes), out _);
            switch (corruption)
            {
                case 0:
                    Array.Clear(bytes, 12, 4);
                    break;
                case 1:
                    bytes[4] = 3;
                    break;
                case 2:
                    bytes[52] = 16;
                    break;
                case 3:
                    WriteInt(bytes, 44, -1);
                    break;
                case 4:
                    WriteInt(bytes, 44, 360 * 65536);
                    break;
                case 5:
                    WriteInt(bytes, 48, 899 * 65536 / 10 + 1);
                    break;
                case 6:
                    Array.Resize(ref bytes, bytes.Length - 1);
                    break;
            }
            Assert.That(RemotePlayerMovementSnapshotCodec.TryDeserialize(new ArraySegment<byte>(bytes), out _, out var decoded), Is.False);
            Assert.That(decoded.EntityId, Is.Zero);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        public void Delta_RejectsMalformedFieldsAndWrongBaseline(int corruption)
        {
            byte[] bytes = new byte[RemotePlayerMovementSnapshotDeltaCodec.MaximumPacketSize];
            RemotePlayerMovementSnapshotDeltaCodec.TrySerialize(Current(15), Baseline(), 1, new ArraySegment<byte>(bytes), out _);
            switch (corruption)
            {
                case 0:
                    bytes[24] |= 0x80;
                    break;
                case 1:
                    bytes[57] = 16;
                    break;
                case 2:
                    WriteInt(bytes, 49, -1);
                    break;
                case 3:
                    WriteInt(bytes, 53, -899 * 65536 / 10 - 1);
                    break;
                case 4:
                    bytes[20] = 0;
                    break;
                case 5:
                    Array.Resize(ref bytes, bytes.Length - 1);
                    break;
                case 6:
                    Array.Resize(ref bytes, bytes.Length + 1);
                    bytes[6]++;
                    break;
            }
            Assert.That(RemotePlayerMovementSnapshotDeltaCodec.TryDeserialize(new ArraySegment<byte>(bytes), Baseline(), out _, out var decoded), Is.False);
            Assert.That(decoded.EntityId, Is.Zero);
        }

        [Test]
        public void InvalidInputAndSmallDestination_DoNotWritePackets()
        {
            var valid = Current(15);
            var invalid = new RemotePlayerMovementSnapshot(0, valid.ServerTick, valid.PositionFixed,
                valid.YawFixedDegrees, valid.PitchFixedDegrees, valid.VelocityFixed);
            byte[] bytes = new byte[RemotePlayerMovementSnapshotDeltaCodec.MaximumPacketSize];
            Assert.That(RemotePlayerMovementSnapshotCodec.TrySerialize(invalid, 1, new ArraySegment<byte>(bytes), out int fullCount), Is.False);
            Assert.That(RemotePlayerMovementSnapshotDeltaCodec.TrySerialize(valid, invalid, 1, new ArraySegment<byte>(bytes), out int deltaCount), Is.False);
            Assert.That(fullCount, Is.Zero);
            Assert.That(deltaCount, Is.Zero);
            Assert.That(bytes, Is.All.Zero);
            Assert.That(RemotePlayerMovementSnapshotDeltaCodec.TrySerialize(valid, Baseline(), 1,
                // 此夹具只改变位置/速度/角度/标志，共33B；不再用含锚点的最大包尺寸判断不足。
                new ArraySegment<byte>(bytes, 0, PacketHeader.Size + RemotePlayerMovementSnapshotDeltaCodec.FixedPayloadSize + 32),
                out deltaCount), Is.False);
            Assert.That(deltaCount, Is.Zero);
            Assert.That(bytes, Is.All.Zero);
        }

        private static void WriteInt(byte[] bytes, int offset, int value)
        {
            uint raw = unchecked((uint)value);
            for (int i = 0; i < 4; i++)
            {
                bytes[offset + i] = (byte)((raw >> (8 * i)) & 255u);
            }
        }
    }
}

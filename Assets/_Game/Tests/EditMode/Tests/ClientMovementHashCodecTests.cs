using System;
using AFPS.NetCode.Messages;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class ClientMovementHashCodecTests
    {
        [Test]
        public void RoundTrip_PreservesFullHashesTickWrapAndArraySlice()
        {
            byte[] bytes = new byte[ClientMovementHashReportCodec.PacketSize + 6];
            bytes[0] = 0xCC;
            bytes[bytes.Length - 1] = 0xCC;
            var report = new ClientMovementHashReport(0x04030201, uint.MaxValue, ulong.MaxValue, 0);
            Assert.That(ClientMovementHashReportCodec.TrySerialize(report, 0,
                new ArraySegment<byte>(bytes, 3, ClientMovementHashReportCodec.PacketSize), out int count), Is.True);
            Assert.That(count, Is.EqualTo(36));
            Assert.That(ClientMovementHashReportCodec.TryDeserialize(new ArraySegment<byte>(bytes, 3, count),
                out uint sequence, out var decoded), Is.True);
            Assert.That(sequence, Is.Zero);
            Assert.That(decoded.EntityId, Is.EqualTo(report.EntityId));
            Assert.That(decoded.InputTick, Is.EqualTo(uint.MaxValue));
            Assert.That(decoded.StateHash, Is.EqualTo(ulong.MaxValue));
            Assert.That(decoded.InputHash, Is.Zero);
            Assert.That(bytes[0], Is.EqualTo(0xCC));
            Assert.That(bytes[bytes.Length - 1], Is.EqualTo(0xCC));
        }

        [Test]
        public void MalformedPacket_DoesNotExposePartialReport()
        {
            byte[] bytes = new byte[ClientMovementHashReportCodec.PacketSize];
            ClientMovementHashReportCodec.TrySerialize(new ClientMovementHashReport(1, 25, 3, 4), 1,
                new ArraySegment<byte>(bytes), out _);
            for (int length = 0; length < bytes.Length; length++)
            {
                Assert.That(ClientMovementHashReportCodec.TryDeserialize(new ArraySegment<byte>(bytes, 0, length),
                    out uint sequence, out var report), Is.False);
                Assert.That(sequence | report.EntityId, Is.Zero);
            }
            bytes[5] = 1;
            Assert.That(ClientMovementHashReportCodec.TryDeserialize(new ArraySegment<byte>(bytes), out _, out _), Is.False);
            Assert.That(ClientMovementHashReportCodec.TrySerialize(default, 1, new ArraySegment<byte>(bytes), out int count), Is.False);
            Assert.That(count, Is.Zero);
            Assert.That(ClientMovementHashReportCodec.TryDeserialize(default, out _, out _), Is.False);
        }
    }
}

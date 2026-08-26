using System;
using AFPS.NetCode.Protocol;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class PacketHeaderCodecTests
    {
        [Test]
        public void Header_RoundTripsAllFields()
        {
            byte[] packet = new byte[PacketHeader.Size + 3];
            PacketHeader expected = new PacketHeader(NetworkMessageType.InputCommandBatch, 3, 0x11223344);

            Assert.That(PacketHeaderCodec.TryWrite(expected, new ArraySegment<byte>(packet)), Is.True);
            Assert.That(PacketHeaderCodec.TryRead(new ArraySegment<byte>(packet), out PacketHeader actual), Is.True);
            Assert.That(actual.Magic, Is.EqualTo(PacketHeader.ExpectedMagic));
            Assert.That(actual.ProtocolVersion, Is.EqualTo(PacketHeader.CurrentProtocolVersion));
            Assert.That(actual.MessageType, Is.EqualTo(NetworkMessageType.InputCommandBatch));
            Assert.That(actual.PayloadLength, Is.EqualTo(3));
            Assert.That(actual.Sequence, Is.EqualTo(0x11223344));
        }

        [Test]
        public void Header_RejectsWrongMagicAndPayloadLength()
        {
            byte[] packet = new byte[PacketHeader.Size + 3];
            PacketHeader header = new PacketHeader(NetworkMessageType.InputCommandBatch, 3, 7);
            Assert.That(PacketHeaderCodec.TryWrite(header, new ArraySegment<byte>(packet)), Is.True);

            packet[0] = 0;
            Assert.That(PacketHeaderCodec.TryRead(new ArraySegment<byte>(packet), out _), Is.False);

            Assert.That(PacketHeaderCodec.TryWrite(header, new ArraySegment<byte>(packet)), Is.True);
            packet[6] = 4;
            Assert.That(PacketHeaderCodec.TryRead(new ArraySegment<byte>(packet), out _), Is.False);
        }
    }
}

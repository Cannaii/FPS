using System;
using AFPS.NetCode.Serialization;

namespace AFPS.NetCode.Protocol
{
    /// <summary>
    /// 负责固定包头的小端序编码、解码和基础合法性检查。
    /// </summary>
    public static class PacketHeaderCodec
    {
        public static bool TryWrite(PacketHeader header, ArraySegment<byte> destination)
        {
            if (destination.Count < PacketHeader.Size)
            {
                return false;
            }

            PacketBufferWriter writer = new PacketBufferWriter(destination);
            return writer.TryWriteUInt32(header.Magic) && writer.TryWriteByte(header.ProtocolVersion) && writer.TryWriteByte((byte)header.MessageType) && writer.TryWriteUInt16(header.PayloadLength) && writer.TryWriteUInt32(header.Sequence);
        }

        public static bool TryRead(ArraySegment<byte> packet, out PacketHeader header)
        {
            header = default;
            if (packet.Count < PacketHeader.Size)
            {
                return false;
            }

            PacketBufferReader reader = new PacketBufferReader(packet);
            if (!reader.TryReadUInt32(out uint magic) || !reader.TryReadByte(out byte version) || !reader.TryReadByte(out byte messageType) || !reader.TryReadUInt16(out ushort payloadLength) || !reader.TryReadUInt32(out uint sequence))
            {
                return false;
            }

            if (magic != PacketHeader.ExpectedMagic || version != PacketHeader.CurrentProtocolVersion || payloadLength != packet.Count - PacketHeader.Size)
            {
                return false;
            }

            header = new PacketHeader(magic, version, (NetworkMessageType)messageType, payloadLength, sequence);
            return true;
        }
    }
}

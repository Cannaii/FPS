using System;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.Serialization;

namespace AFPS.NetCode.Messages
{
    /// <summary>编解码服务器可靠广播的远端玩家销毁消息。</summary>
    public static class PlayerDespawnCodec
    {
        public const int PayloadSize = 4;
        public const int PacketSize = PacketHeader.Size + PayloadSize;

        public static bool TrySerialize(uint entityId, uint sequence, ArraySegment<byte> destination, out int bytesWritten)
        {
            bytesWritten = 0;
            if (entityId == 0 || destination.Count < PacketSize)
            {
                return false;
            }

            PacketHeader header = new PacketHeader(NetworkMessageType.PlayerDespawn, PayloadSize, sequence);
            if (!PacketHeaderCodec.TryWrite(header, destination))
            {
                return false;
            }

            PacketBufferWriter writer = new PacketBufferWriter(new ArraySegment<byte>(destination.Array, destination.Offset + PacketHeader.Size, PayloadSize));
            if (!writer.TryWriteUInt32(entityId))
            {
                return false;
            }

            bytesWritten = PacketSize;
            return true;
        }

        public static bool TryDeserialize(ArraySegment<byte> packet, out uint entityId)
        {
            entityId = 0;
            if (!PacketHeaderCodec.TryRead(packet, out PacketHeader header) || header.MessageType != NetworkMessageType.PlayerDespawn || header.PayloadLength != PayloadSize)
            {
                return false;
            }

            PacketBufferReader reader = new PacketBufferReader(new ArraySegment<byte>(packet.Array, packet.Offset + PacketHeader.Size, PayloadSize));
            return reader.TryReadUInt32(out entityId) && entityId != 0 && reader.BytesRemaining == 0;
        }
    }
}

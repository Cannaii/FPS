using System;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.Serialization;

namespace AFPS.NetCode.Messages
{
    /// <summary>编解码服务器分配的本地玩家实体 ID。</summary>
    public static class PlayerSessionAssignmentCodec
    {
        public const int PayloadSize = 4;
        public const int PacketSize = PacketHeader.Size + PayloadSize;

        public static bool TrySerialize(in PlayerSessionAssignment assignment, uint sequence, ArraySegment<byte> destination, out int bytesWritten)
        {
            bytesWritten = 0;
            if (assignment.EntityId == 0 || destination.Count < PacketSize)
            {
                return false;
            }

            PacketHeader header = new PacketHeader(NetworkMessageType.PlayerSessionAssignment, PayloadSize, sequence);
            if (!PacketHeaderCodec.TryWrite(header, destination))
            {
                return false;
            }

            PacketBufferWriter writer = new PacketBufferWriter(new ArraySegment<byte>(destination.Array, destination.Offset + PacketHeader.Size, PayloadSize));
            if (!writer.TryWriteUInt32(assignment.EntityId))
            {
                return false;
            }

            bytesWritten = PacketSize;
            return true;
        }

        public static bool TryDeserialize(ArraySegment<byte> packet, out PlayerSessionAssignment assignment)
        {
            assignment = default;
            if (!PacketHeaderCodec.TryRead(packet, out PacketHeader header) || header.MessageType != NetworkMessageType.PlayerSessionAssignment || header.PayloadLength != PayloadSize)
            {
                return false;
            }

            PacketBufferReader reader = new PacketBufferReader(new ArraySegment<byte>(packet.Array, packet.Offset + PacketHeader.Size, PayloadSize));
            if (!reader.TryReadUInt32(out uint entityId) || entityId == 0 || reader.BytesRemaining != 0)
            {
                return false;
            }

            assignment = new PlayerSessionAssignment(entityId);
            return true;
        }
    }
}

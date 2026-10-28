using System;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.Serialization;

namespace AFPS.NetCode.Messages
{
    public readonly struct ClientStateHashReport
    {
        public readonly uint EntityId;
        public readonly uint InputTick;
        public readonly ulong StateHash;
        public readonly ulong InputHash;

        public ClientStateHashReport(uint entityId, uint inputTick, ulong stateHash, ulong inputHash)
        {
            EntityId = entityId;
            InputTick = inputTick;
            StateHash = stateHash;
            InputHash = inputHash;
        }
    }

    /// <summary>客户端回滚校正前的原始预测样本；不是校正后权威状态的回声，也不是防作弊凭据。</summary>
    public static class ClientStateHashReportCodec
    {
        public const int PayloadSize = 24;
        public const int PacketSize = PacketHeader.Size + PayloadSize;

        public static bool TrySerialize(in ClientStateHashReport report, uint sequence,
            ArraySegment<byte> destination, out int written)
        {
            written = 0;
            if (report.EntityId == 0 || destination.Count < PacketSize
                || !PacketHeaderCodec.TryWrite(new PacketHeader(NetworkMessageType.ClientStateHashReport,
                    PayloadSize, sequence), destination))
            {
                return false;
            }
            var writer = new PacketBufferWriter(new ArraySegment<byte>(
                destination.Array, destination.Offset + PacketHeader.Size, PayloadSize));
            if (!writer.TryWriteUInt32(report.EntityId) || !writer.TryWriteUInt32(report.InputTick)
                || !writer.TryWriteUInt64(report.StateHash) || !writer.TryWriteUInt64(report.InputHash))
            {
                return false;
            }
            written = PacketSize;
            return true;
        }

        public static bool TryDeserialize(ArraySegment<byte> packet, out uint sequence, out ClientStateHashReport report)
        {
            sequence = 0;
            report = default;
            if (!PacketHeaderCodec.TryRead(packet, out var header)
                || header.MessageType != NetworkMessageType.ClientStateHashReport || header.PayloadLength != PayloadSize)
            {
                return false;
            }
            var reader = new PacketBufferReader(new ArraySegment<byte>(
                packet.Array, packet.Offset + PacketHeader.Size, PayloadSize));
            if (!reader.TryReadUInt32(out uint entityId) || entityId == 0
                || !reader.TryReadUInt32(out uint tick) || !reader.TryReadUInt64(out ulong stateHash)
                || !reader.TryReadUInt64(out ulong inputHash) || reader.BytesRemaining != 0)
            {
                return false;
            }
            sequence = header.Sequence;
            report = new ClientStateHashReport(entityId, tick, stateHash, inputHash);
            return true;
        }
    }
}

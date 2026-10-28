using System;
using AFPS.NetCode.Protocol;

namespace AFPS.NetCode.TimeSync
{
    /// <summary>仅回显服务器响应序号，不接受客户端自报RTT或时钟；固定12B包头，无负载。</summary>
    public static class TimeSyncAcknowledgementCodec
    {
        public const int PacketSize = PacketHeader.Size;

        public static bool TrySerialize(uint responseSequence, ArraySegment<byte> destination, out int bytesWritten)
        {
            bytesWritten = 0;
            if (!PacketHeaderCodec.TryWrite(new PacketHeader(
                NetworkMessageType.TimeSyncAcknowledgement, 0, responseSequence), destination))
            {
                return false;
            }
            bytesWritten = PacketSize;
            return true;
        }

        public static bool TryDeserialize(ArraySegment<byte> packet, out uint responseSequence)
        {
            responseSequence = 0;
            if (!PacketHeaderCodec.TryRead(packet, out var header)
                || header.MessageType != NetworkMessageType.TimeSyncAcknowledgement || header.PayloadLength != 0)
            {
                return false;
            }
            responseSequence = header.Sequence;
            return true;
        }
    }
}

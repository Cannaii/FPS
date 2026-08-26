using System;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.Serialization;

namespace AFPS.NetCode.TimeSync
{
    /// <summary>
    /// 编解码客户端时间同步请求和服务器 Tick 响应。
    /// </summary>
    public static class TimeSyncCodec
    {
        /// <summary>时间同步请求负载的固定字节数。</summary>
        public const int RequestPayloadSize = 8;

        /// <summary>时间同步请求包头与负载的总字节数。</summary>
        public const int RequestPacketSize = PacketHeader.Size + RequestPayloadSize;

        /// <summary>时间同步响应负载的固定字节数。</summary>
        public const int ResponsePayloadSize = 34;

        /// <summary>时间同步响应包头与负载的总字节数。</summary>
        public const int ResponsePacketSize = PacketHeader.Size + ResponsePayloadSize;

        public static bool TrySerializeRequest(uint requestSequence, ulong clientSendTimestampMicroseconds, ArraySegment<byte> destination, out int bytesWritten)
        {
            bytesWritten = 0;
            if (destination.Count < RequestPacketSize)
            {
                return false;
            }

            PacketHeader header = new PacketHeader(NetworkMessageType.TimeSyncRequest, RequestPayloadSize, requestSequence);
            if (!PacketHeaderCodec.TryWrite(header, destination))
            {
                return false;
            }

            PacketBufferWriter writer = new PacketBufferWriter(new ArraySegment<byte>(destination.Array, destination.Offset + PacketHeader.Size, RequestPayloadSize));
            if (!writer.TryWriteUInt64(clientSendTimestampMicroseconds))
            {
                return false;
            }

            bytesWritten = PacketHeader.Size + writer.BytesWritten;
            return true;
        }

        public static bool TryDeserializeRequest(ArraySegment<byte> packet, out TimeSyncRequest request)
        {
            request = default;
            if (!PacketHeaderCodec.TryRead(packet, out PacketHeader header) || header.MessageType != NetworkMessageType.TimeSyncRequest || header.PayloadLength != RequestPayloadSize)
            {
                return false;
            }

            PacketBufferReader reader = new PacketBufferReader(new ArraySegment<byte>(packet.Array, packet.Offset + PacketHeader.Size, RequestPayloadSize));
            if (!reader.TryReadUInt64(out ulong clientSendTimestamp) || reader.BytesRemaining != 0)
            {
                return false;
            }

            request = new TimeSyncRequest(header.Sequence, clientSendTimestamp);
            return true;
        }

        public static bool TrySerializeResponse(in TimeSyncResponse response, uint responseSequence, ArraySegment<byte> destination, out int bytesWritten)
        {
            bytesWritten = 0;
            if (destination.Count < ResponsePacketSize || response.ServerSendTimestampMicroseconds < response.ServerReceiveTimestampMicroseconds)
            {
                return false;
            }

            PacketHeader header = new PacketHeader(NetworkMessageType.TimeSyncResponse, ResponsePayloadSize, responseSequence);
            if (!PacketHeaderCodec.TryWrite(header, destination))
            {
                return false;
            }

            PacketBufferWriter writer = new PacketBufferWriter(new ArraySegment<byte>(destination.Array, destination.Offset + PacketHeader.Size, ResponsePayloadSize));
            bool success = writer.TryWriteUInt32(response.RequestSequence) && writer.TryWriteUInt64(response.ClientSendTimestampMicroseconds) && writer.TryWriteUInt64(response.ServerReceiveTimestampMicroseconds) && writer.TryWriteUInt64(response.ServerSendTimestampMicroseconds) && writer.TryWriteUInt32(response.ServerWorldTick) && writer.TryWriteUInt16(response.ServerTickFraction);
            if (!success)
            {
                return false;
            }

            bytesWritten = PacketHeader.Size + writer.BytesWritten;
            return true;
        }

        public static bool TryDeserializeResponse(ArraySegment<byte> packet, out TimeSyncResponse response)
        {
            response = default;
            if (!PacketHeaderCodec.TryRead(packet, out PacketHeader header) || header.MessageType != NetworkMessageType.TimeSyncResponse || header.PayloadLength != ResponsePayloadSize)
            {
                return false;
            }

            PacketBufferReader reader = new PacketBufferReader(new ArraySegment<byte>(packet.Array, packet.Offset + PacketHeader.Size, ResponsePayloadSize));
            if (!reader.TryReadUInt32(out uint requestSequence) || !reader.TryReadUInt64(out ulong clientSendTimestamp) || !reader.TryReadUInt64(out ulong serverReceiveTimestamp) || !reader.TryReadUInt64(out ulong serverSendTimestamp) || !reader.TryReadUInt32(out uint serverWorldTick) || !reader.TryReadUInt16(out ushort serverTickFraction))
            {
                return false;
            }

            if (reader.BytesRemaining != 0 || serverSendTimestamp < serverReceiveTimestamp)
            {
                return false;
            }

            response = new TimeSyncResponse(requestSequence, clientSendTimestamp, serverReceiveTimestamp, serverSendTimestamp, serverWorldTick, serverTickFraction);
            return true;
        }
    }
}

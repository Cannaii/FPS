using System;
using AFPS.NetCode.Transport;

namespace AFPS.NetCode.TimeSync
{
    /// <summary>
    /// 服务器为一个客户端连接生成时间同步响应，并返回当前服务器世界 Tick 相位。
    /// </summary>
    public sealed class ServerTimeSyncResponder
    {
        private readonly IGameTransport transport;
        private readonly TransportConnectionId clientConnectionId;
        private readonly byte[] responseBuffer = new byte[TimeSyncCodec.ResponsePacketSize];
        private uint nextResponseSequence = 1;

        public ServerTimeSyncResponder(IGameTransport transport, TransportConnectionId clientConnectionId)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            if (!clientConnectionId.IsValid)
            {
                throw new ArgumentException("客户端连接 ID 必须有效。", nameof(clientConnectionId));
            }

            this.clientConnectionId = clientConnectionId;
        }

        /// <summary>
        /// 解码请求并立即发送响应。serverReceive 和 serverSend 必须来自同一个服务器单调时钟。
        /// </summary>
        public bool TryRespond(ArraySegment<byte> requestPacket, ulong serverReceiveTimestampMicroseconds, ulong serverSendTimestampMicroseconds, uint serverWorldTick, float serverTickAlpha, out TransportSendResult sendResult)
        {
            sendResult = TransportSendResult.TransportError;
            if (serverTickAlpha < 0f || serverTickAlpha > 1f || float.IsNaN(serverTickAlpha) || serverSendTimestampMicroseconds < serverReceiveTimestampMicroseconds)
            {
                return false;
            }

            if (!TimeSyncCodec.TryDeserializeRequest(requestPacket, out TimeSyncRequest request))
            {
                return false;
            }

            ushort tickFraction = (ushort)Math.Round(serverTickAlpha * 65535.0, MidpointRounding.AwayFromZero);
            TimeSyncResponse response = new TimeSyncResponse(request.RequestSequence, request.ClientSendTimestampMicroseconds, serverReceiveTimestampMicroseconds, serverSendTimestampMicroseconds, serverWorldTick, tickFraction);
            uint responseSequence = nextResponseSequence;
            if (!TimeSyncCodec.TrySerializeResponse(response, responseSequence, new ArraySegment<byte>(responseBuffer), out int packetBytes))
            {
                return false;
            }

            sendResult = transport.Send(clientConnectionId, TransportDelivery.Unreliable, new ArraySegment<byte>(responseBuffer, 0, packetBytes));
            if (sendResult == TransportSendResult.Success)
            {
                nextResponseSequence = unchecked(nextResponseSequence + 1);
            }

            return sendResult == TransportSendResult.Success;
        }
    }
}

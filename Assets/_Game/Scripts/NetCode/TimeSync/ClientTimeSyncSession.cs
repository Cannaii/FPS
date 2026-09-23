using System;
using AFPS.NetCode.Transport;

namespace AFPS.NetCode.TimeSync
{
    /// <summary>
    /// 客户端通过一个服务器连接周期性发送时间同步请求，并把响应交给 Tick 估算器。
    /// </summary>
    public sealed class ClientTimeSyncSession
    {
        private readonly IGameTransport transport;
        private readonly TransportConnectionId serverConnectionId;
        private readonly byte[] requestBuffer = new byte[TimeSyncCodec.RequestPacketSize];
        private uint nextRequestSequence = 1;

        public ClientServerTickSynchronizer Synchronizer { get; }

        public ClientTimeSyncSession(IGameTransport transport, TransportConnectionId serverConnectionId, ClientServerTickSynchronizer synchronizer)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            Synchronizer = synchronizer ?? throw new ArgumentNullException(nameof(synchronizer));
            if (!serverConnectionId.IsValid)
            {
                throw new ArgumentException("服务器连接 ID 必须有效。", nameof(serverConnectionId));
            }

            this.serverConnectionId = serverConnectionId;
        }

        /// <summary>
        /// 发送一次不可靠时间采样请求；只有成功交给传输层后才记录为待响应请求。
        /// </summary>
        public TransportSendResult SendRequest(ulong clientSendTimestampMicroseconds, out uint requestSequence)
        {
            requestSequence = nextRequestSequence;
            if (!TimeSyncCodec.TrySerializeRequest(requestSequence, clientSendTimestampMicroseconds, new ArraySegment<byte>(requestBuffer), out int packetBytes))
            {
                return TransportSendResult.TransportError;
            }

            TransportSendResult result = transport.Send(serverConnectionId, TransportDelivery.Unreliable, new ArraySegment<byte>(requestBuffer, 0, packetBytes));
            if (result == TransportSendResult.Success)
            {
                Synchronizer.RegisterSentRequest(requestSequence, clientSendTimestampMicroseconds);
                nextRequestSequence = unchecked(nextRequestSequence + 1);
            }

            return result;
        }

        public bool TryReceiveResponse(ArraySegment<byte> packet, ulong clientReceiveTimestampMicroseconds, out ServerTickSyncSample sample)
        {
            sample = default;
            return TimeSyncCodec.TryDeserializeResponse(packet, out TimeSyncResponse response) && Synchronizer.TryProcessResponse(response, clientReceiveTimestampMicroseconds, out sample);
        }
    }
}

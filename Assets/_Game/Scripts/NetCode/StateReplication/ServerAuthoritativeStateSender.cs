using System;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Transport;

namespace AFPS.NetCode.StateReplication
{
    /// <summary>
    /// 为一个客户端连接编码并发送服务器权威玩家状态。
    /// 每个连接使用独立实例，从而维护独立的包序号。
    /// </summary>
    public sealed class ServerAuthoritativeStateSender
    {
        private readonly IGameTransport transport;
        private readonly TransportConnectionId clientConnectionId;
        private readonly byte[] packetBuffer = new byte[AuthoritativePlayerStateCodec.PacketSize];
        private uint nextPacketSequence;

        public ServerAuthoritativeStateSender(IGameTransport transport, TransportConnectionId clientConnectionId, uint initialPacketSequence = 1)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            if (!clientConnectionId.IsValid)
            {
                throw new ArgumentException("客户端连接 ID 必须有效。", nameof(clientConnectionId));
            }

            this.clientConnectionId = clientConnectionId;
            nextPacketSequence = initialPacketSequence;
        }

        /// <summary>
        /// 将最新权威状态交给不可靠有序通道；只有成功入队后才消耗包序号。
        /// </summary>
        public bool TrySend(in AuthoritativePlayerState authoritativeState, out AuthoritativeStateSendResult result)
        {
            uint packetSequence = nextPacketSequence;
            if (!AuthoritativePlayerStateCodec.TrySerialize(authoritativeState, packetSequence, new ArraySegment<byte>(packetBuffer), out int packetBytes))
            {
                result = new AuthoritativeStateSendResult(TransportSendResult.TransportError, packetSequence, authoritativeState.ServerTick, authoritativeState.LastProcessedInputTick, 0);
                return false;
            }

            TransportSendResult transportResult = transport.Send(clientConnectionId, TransportDelivery.UnreliableSequenced, new ArraySegment<byte>(packetBuffer, 0, packetBytes));
            if (transportResult == TransportSendResult.Success)
            {
                nextPacketSequence = unchecked(nextPacketSequence + 1);
            }

            result = new AuthoritativeStateSendResult(transportResult, packetSequence, authoritativeState.ServerTick, authoritativeState.LastProcessedInputTick, packetBytes);
            return result.Succeeded;
        }
    }
}

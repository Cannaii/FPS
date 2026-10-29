using System;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Transport;

namespace AFPS.NetCode.StateReplication
{
    /// <summary>
    /// 为一个客户端连接编码并发送服务器权威玩家状态。
    /// 每个连接使用独立实例，从而维护独立的包序号。
    /// </summary>
    public sealed class ServerMovementStateSender
    {
        private readonly IGameTransport transport;
        private readonly TransportConnectionId clientConnectionId;
        private readonly byte[] packetBuffer = new byte[AuthoritativeMovementStateCodec.PacketSize];
        private uint nextPacketSequence;

        public ServerMovementStateSender(IGameTransport transport, TransportConnectionId clientConnectionId, uint initialPacketSequence = 1)
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
        /// 普通确认默认走不可靠有序通道，恢复种子可显式选可靠通道；只有成功入队后才消耗包序号。
        /// </summary>
        public bool TrySend(in AuthoritativeMovementState authoritativeState, out MovementStateSendResult result,
            TransportDelivery delivery = TransportDelivery.UnreliableSequenced)
        {
            uint packetSequence = nextPacketSequence;
            if (!AuthoritativeMovementStateCodec.TrySerialize(authoritativeState, packetSequence, new ArraySegment<byte>(packetBuffer), out int packetBytes))
            {
                result = new MovementStateSendResult(TransportSendResult.TransportError, packetSequence, authoritativeState.ServerTick, authoritativeState.LastProcessedInputTick, 0);
                return false;
            }

            TransportSendResult transportResult = transport.Send(clientConnectionId, delivery, new ArraySegment<byte>(packetBuffer, 0, packetBytes));
            if (transportResult == TransportSendResult.Success)
            {
                nextPacketSequence = unchecked(nextPacketSequence + 1);
            }

            result = new MovementStateSendResult(transportResult, packetSequence, authoritativeState.ServerTick, authoritativeState.LastProcessedInputTick, packetBytes);
            return result.Succeeded;
        }
    }
}

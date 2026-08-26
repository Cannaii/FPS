using AFPS.NetCode.Transport;

namespace AFPS.NetCode.StateReplication
{
    /// <summary>
    /// 服务器一次权威玩家状态发送的结果。
    /// </summary>
    public readonly struct AuthoritativeStateSendResult
    {
        /// <summary>
        /// 状态包是否成功交给底层传输层。
        /// </summary>
        public bool Succeeded => TransportResult == TransportSendResult.Success;

        /// <summary>
        /// 底层传输层返回的即时发送结果。
        /// </summary>
        public readonly TransportSendResult TransportResult;

        /// <summary>
        /// 本次状态包使用的应用层序号。
        /// </summary>
        public readonly uint PacketSequence;

        /// <summary>
        /// 状态生成时的服务器世界 Tick。
        /// </summary>
        public readonly uint ServerTick;

        /// <summary>
        /// 状态确认的最后一个客户端输入 Tick。
        /// </summary>
        public readonly uint LastProcessedInputTick;

        /// <summary>
        /// 包头和状态负载合计的发送字节数。
        /// </summary>
        public readonly int PacketBytes;

        public AuthoritativeStateSendResult(TransportSendResult transportResult, uint packetSequence, uint serverTick, uint lastProcessedInputTick, int packetBytes)
        {
            TransportResult = transportResult;
            PacketSequence = packetSequence;
            ServerTick = serverTick;
            LastProcessedInputTick = lastProcessedInputTick;
            PacketBytes = packetBytes;
        }
    }
}

using AFPS.NetCode.Transport;

namespace AFPS.NetCode.InputReplication
{
    /// <summary>
    /// 客户端输入批次在组包和发送流程中结束的位置。
    /// </summary>
    public enum InputBatchSendStatus
    {
        Sent,
        MissingLatestInput,
        SerializationFailed,
        TransportRejected
    }

    /// <summary>
    /// 客户端一次输入批量发送的结果和调试信息。
    /// </summary>
    public readonly struct InputBatchSendResult
    {
        /// <summary>
        /// 数据是否成功交给底层传输层。
        /// </summary>
        public bool Succeeded => Status == InputBatchSendStatus.Sent;

        /// <summary>
        /// 区分历史缺失、序列化失败和传输层拒绝。
        /// </summary>
        public readonly InputBatchSendStatus Status;

        /// <summary>
        /// 底层传输层返回的即时发送结果。
        /// </summary>
        public readonly TransportSendResult TransportResult;

        /// <summary>
        /// 本包携带的第一条客户端输入 Tick。
        /// </summary>
        public readonly uint FirstTick;

        /// <summary>
        /// 本包携带的最后一条客户端输入 Tick。
        /// </summary>
        public readonly uint LastTick;

        /// <summary>
        /// 本包携带的连续输入数量。
        /// </summary>
        public readonly int CommandCount;

        /// <summary>
        /// 本包的应用层发送序号。
        /// </summary>
        public readonly uint PacketSequence;

        /// <summary>
        /// 包头与负载合计的实际发送字节数。
        /// </summary>
        public readonly int PacketBytes;

        public InputBatchSendResult(InputBatchSendStatus status, TransportSendResult transportResult, uint firstTick, uint lastTick, int commandCount, uint packetSequence, int packetBytes)
        {
            Status = status;
            TransportResult = transportResult;
            FirstTick = firstTick;
            LastTick = lastTick;
            CommandCount = commandCount;
            PacketSequence = packetSequence;
            PacketBytes = packetBytes;
        }
    }
}

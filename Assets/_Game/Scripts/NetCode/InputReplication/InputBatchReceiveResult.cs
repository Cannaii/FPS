namespace AFPS.NetCode.InputReplication
{
    /// <summary>
    /// 服务器处理一个输入批量包后的分类统计。
    /// </summary>
    public readonly struct InputBatchReceiveResult
    {
        /// <summary>
        /// 数据包的应用层序号，仅用于诊断丢包、重复和乱序。
        /// </summary>
        public readonly uint PacketSequence;

        /// <summary>
        /// 本包携带的第一条客户端输入 Tick。
        /// </summary>
        public readonly uint FirstTick;

        /// <summary>
        /// 本包携带的最后一条客户端输入 Tick。
        /// </summary>
        public readonly uint LastTick;

        /// <summary>
        /// 首次进入服务器接收窗口的命令数量。
        /// </summary>
        public readonly int AcceptedCommandCount;

        /// <summary>
        /// 已处理或已经存在于窗口中的重复命令数量。
        /// </summary>
        public readonly int DuplicateCommandCount;

        /// <summary>
        /// 因超出服务器未来 Tick 接收窗口而被拒绝的命令数量。
        /// </summary>
        public readonly int RejectedCommandCount;

        public InputBatchReceiveResult(uint packetSequence, uint firstTick, uint lastTick, int acceptedCommandCount, int duplicateCommandCount, int rejectedCommandCount)
        {
            PacketSequence = packetSequence;
            FirstTick = firstTick;
            LastTick = lastTick;
            AcceptedCommandCount = acceptedCommandCount;
            DuplicateCommandCount = duplicateCommandCount;
            RejectedCommandCount = rejectedCommandCount;
        }
    }
}

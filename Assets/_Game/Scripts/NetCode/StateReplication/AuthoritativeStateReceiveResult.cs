namespace AFPS.NetCode.StateReplication
{
    /// <summary>
    /// 客户端处理一个权威状态包后的结果分类。
    /// </summary>
    public enum AuthoritativeStateReceiveStatus
    {
        Accepted,
        InvalidPacket,
        StaleOrDuplicateSequence,
        RegressiveServerTick,
        RegressiveInputAcknowledgement
    }

    /// <summary>
    /// 客户端权威状态接收结果及其诊断字段。
    /// </summary>
    public readonly struct AuthoritativeStateReceiveResult
    {
        public bool Accepted => Status == AuthoritativeStateReceiveStatus.Accepted;

        /// <summary>
        /// 状态包被接受或拒绝的具体原因。
        /// </summary>
        public readonly AuthoritativeStateReceiveStatus Status;

        /// <summary>
        /// 成功解码时得到的应用层包序号。
        /// </summary>
        public readonly uint PacketSequence;

        /// <summary>
        /// 成功解码时得到的服务器世界 Tick。
        /// </summary>
        public readonly uint ServerTick;

        /// <summary>
        /// 成功解码时得到的最后处理输入 Tick。
        /// </summary>
        public readonly uint LastProcessedInputTick;

        public AuthoritativeStateReceiveResult(AuthoritativeStateReceiveStatus status, uint packetSequence = 0, uint serverTick = 0, uint lastProcessedInputTick = 0)
        {
            Status = status;
            PacketSequence = packetSequence;
            ServerTick = serverTick;
            LastProcessedInputTick = lastProcessedInputTick;
        }
    }
}

namespace AFPS.NetCode.TimeSync
{
    /// <summary>
    /// 客户端发起的一次服务器 Tick 采样请求。
    /// </summary>
    public readonly struct TimeSyncRequest
    {
        /// <summary>
        /// 请求包的应用层序号，由响应原样返回。
        /// </summary>
        public readonly uint RequestSequence;

        /// <summary>
        /// 客户端发送请求时的单调时钟微秒值。
        /// </summary>
        public readonly ulong ClientSendTimestampMicroseconds;

        public TimeSyncRequest(uint requestSequence, ulong clientSendTimestampMicroseconds)
        {
            RequestSequence = requestSequence;
            ClientSendTimestampMicroseconds = clientSendTimestampMicroseconds;
        }
    }

    /// <summary>
    /// 服务器对一次 Tick 采样请求的响应。
    /// 客户端和服务器时间戳可以使用不同起点，只要求各自时钟单调且单位相同。
    /// </summary>
    public readonly struct TimeSyncResponse
    {
        /// <summary>本响应对应的客户端请求包序号。</summary>
        public readonly uint RequestSequence;

        /// <summary>客户端发送原请求时的单调时钟微秒值，由服务器原样返回。</summary>
        public readonly ulong ClientSendTimestampMicroseconds;

        /// <summary>服务器收到请求时的服务器单调时钟微秒值。</summary>
        public readonly ulong ServerReceiveTimestampMicroseconds;

        /// <summary>服务器生成响应时的服务器单调时钟微秒值。</summary>
        public readonly ulong ServerSendTimestampMicroseconds;

        /// <summary>服务器生成响应时已经完成的世界 Tick。</summary>
        public readonly uint ServerWorldTick;

        /// <summary>服务器在当前 Tick 内的进度，0 到 65535 映射为 0 到 1。</summary>
        public readonly ushort ServerTickFraction;

        /// <summary>包含 Tick 小数相位的服务器连续时间。</summary>
        public double ServerTickTime => ServerWorldTick + ServerTickFraction / 65535.0;

        public TimeSyncResponse(uint requestSequence, ulong clientSendTimestampMicroseconds, ulong serverReceiveTimestampMicroseconds, ulong serverSendTimestampMicroseconds, uint serverWorldTick, ushort serverTickFraction)
        {
            RequestSequence = requestSequence;
            ClientSendTimestampMicroseconds = clientSendTimestampMicroseconds;
            ServerReceiveTimestampMicroseconds = serverReceiveTimestampMicroseconds;
            ServerSendTimestampMicroseconds = serverSendTimestampMicroseconds;
            ServerWorldTick = serverWorldTick;
            ServerTickFraction = serverTickFraction;
        }
    }
}

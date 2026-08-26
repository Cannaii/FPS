namespace AFPS.NetCode.TimeSync
{
    /// <summary>
    /// 一次有效时间同步响应计算出的网络与服务器时间线样本。
    /// </summary>
    public readonly struct ServerTickSyncSample
    {
        /// <summary>产生本样本的客户端请求序号。</summary>
        public readonly uint RequestSequence;

        /// <summary>扣除服务器处理耗时后的网络往返时间，单位为毫秒。</summary>
        public readonly double NetworkRoundTripMilliseconds;

        /// <summary>按对称路径假设估算的单向网络时间，单位为毫秒。</summary>
        public readonly double EstimatedOneWayMilliseconds;

        /// <summary>客户端收到响应时估算的连续服务器世界 Tick。</summary>
        public readonly double EstimatedServerTickAtClientReceive;

        /// <summary>平滑后的客户端时间到服务器 Tick 偏移量，单位为 Tick。</summary>
        public readonly double SmoothedServerTickOffset;

        public ServerTickSyncSample(uint requestSequence, double networkRoundTripMilliseconds, double estimatedOneWayMilliseconds, double estimatedServerTickAtClientReceive, double smoothedServerTickOffset)
        {
            RequestSequence = requestSequence;
            NetworkRoundTripMilliseconds = networkRoundTripMilliseconds;
            EstimatedOneWayMilliseconds = estimatedOneWayMilliseconds;
            EstimatedServerTickAtClientReceive = estimatedServerTickAtClientReceive;
            SmoothedServerTickOffset = smoothedServerTickOffset;
        }
    }
}

namespace AFPS.NetCode.Metrics
{
    /// <summary>服务器连接的只读诊断副本；时间毫秒，窗口/输入序号使用Tick，不含恢复凭据。</summary>
    public readonly struct ServerConnectionDiagnostics
    {
        public readonly uint EntityId;
        public readonly uint InputTick;
        public readonly bool HasFreshQuality;
        public readonly double RoundTripMilliseconds;
        public readonly double JitterMilliseconds;
        public readonly int WaitLimitTicks;
        public readonly int RepeatLimitTicks;

        public ServerConnectionDiagnostics(uint entityId, uint inputTick, bool fresh, double rtt, double jitter,
            int waitLimit, int repeatLimit)
        {
            EntityId = entityId;
            InputTick = inputTick;
            HasFreshQuality = fresh;
            RoundTripMilliseconds = rtt;
            JitterMilliseconds = jitter;
            WaitLimitTicks = waitLimit;
            RepeatLimitTicks = repeatLimit;
        }
    }
}

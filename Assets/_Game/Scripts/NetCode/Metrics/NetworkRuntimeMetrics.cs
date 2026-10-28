namespace AFPS.NetCode.Metrics
{
    /// <summary>运行期网络诊断计数器；由会话管理器单线程更新。</summary>
    public sealed class NetworkRuntimeMetrics
    {
        public ulong TimeSyncRequestsSent { get; internal set; }
        public ulong TimeSyncResponsesReceived { get; internal set; }
        public ulong TimeSyncAcknowledgementsSent { get; internal set; }
        public ulong ServerRoundTripSamplesReceived { get; internal set; }
        /// <summary>累计服务器推进尝试/替代输入次数，单位Tick；不包含恢复确认门禁跳过的会话。</summary>
        public ulong ServerInputWaits { get; internal set; }
        public ulong ServerRepeatedInputTicks { get; internal set; }
        public ulong ServerNeutralInputTicks { get; internal set; }
        /// <summary>扣除服务器处理时间的真实往返毫秒；不表示单向延迟。</summary>
        public double LastNetworkRoundTripMilliseconds { get; internal set; }
        public double RoundTripJitterMilliseconds { get; internal set; }
        /// <summary>当前/目标远端表现延迟，单位服务器Tick；不影响权威模拟。</summary>
        public double RemoteInterpolationDelayTicks { get; internal set; }
        public double TargetRemoteInterpolationDelayTicks { get; internal set; }
        public ulong SnapshotPacketsSent { get; internal set; }
        public ulong SnapshotBytesSent { get; internal set; }
        public ulong ShotResultsSent { get; internal set; }
        public ulong CorrectionsApplied { get; internal set; }
        public ulong StateHashReportsSent { get; internal set; }
        public ulong StateHashReportsSuperseded { get; internal set; }
        public ulong StateHashReportsReceived { get; internal set; }
        public ulong StateHashReportsRejected { get; internal set; }
        public ulong StateHashMatches { get; internal set; }
        public ulong StateHashMismatches { get; internal set; }
        public ulong StateHashDifferentInputs { get; internal set; }
        public ulong StateHashHistoryMisses { get; internal set; }
        public ulong RewindQueries { get; internal set; }
        public long LastServerTickMicroseconds { get; internal set; }
        public long MaximumServerTickMicroseconds { get; internal set; }
        public long LastRewindMicroseconds { get; internal set; }
    }
}

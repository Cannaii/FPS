namespace AFPS.NetCode.Metrics
{
    /// <summary>运行期网络诊断计数器；由会话管理器单线程更新。</summary>
    public sealed class NetworkRuntimeMetrics
    {
        public ulong SnapshotPacketsSent { get; internal set; }
        public ulong SnapshotBytesSent { get; internal set; }
        public ulong ShotResultsSent { get; internal set; }
        public ulong CorrectionsApplied { get; internal set; }
        public ulong RewindQueries { get; internal set; }
        public long LastServerTickMicroseconds { get; internal set; }
        public long MaximumServerTickMicroseconds { get; internal set; }
        public long LastRewindMicroseconds { get; internal set; }
    }
}

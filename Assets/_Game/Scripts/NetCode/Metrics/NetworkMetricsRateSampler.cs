namespace AFPS.NetCode.Metrics
{
    /// <summary>累计计数转每秒速率；单调时间单位微秒，窗口至少0.5秒，不将重置当作ulong回绕流量。</summary>
    public sealed class NetworkMetricsRateSampler
    {
        private ulong previousTime;
        private ulong previousBytes;
        private ulong previousPackets;
        private bool initialized;
        public bool HasRate { get; private set; }
        public double BytesPerSecond { get; private set; }
        public double PacketsPerSecond { get; private set; }

        public void Update(ulong now, ulong bytes, ulong packets)
        {
            if (!initialized || now < previousTime || bytes < previousBytes || packets < previousPackets)
            {
                previousTime = now;
                previousBytes = bytes;
                previousPackets = packets;
                initialized = true;
                HasRate = false;
                BytesPerSecond = 0d;
                PacketsPerSecond = 0d;
                return;
            }
            ulong elapsed = now - previousTime;
            if (elapsed < 500000)
            {
                return;
            }
            double seconds = elapsed / 1000000d;
            BytesPerSecond = (bytes - previousBytes) / seconds;
            PacketsPerSecond = (packets - previousPackets) / seconds;
            HasRate = true;
            previousTime = now;
            previousBytes = bytes;
            previousPackets = packets;
        }
    }
}

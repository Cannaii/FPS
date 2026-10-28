using System;

namespace AFPS.NetCode.SnapshotInterpolation
{
    /// <summary>只读网络质量驱动的表现时钟；不修改权威Tick、输入或模拟状态。</summary>
    public sealed class AdaptiveInterpolationClock
    {
        private const double TickPeriod = 4294967296d;
        private readonly double tickMilliseconds;
        private ulong lastUpdateMicroseconds;
        private bool hasUpdate;
        private bool hasRenderTime;
        private double lastEstimate;
        private double unwrappedEstimate;
        private double lastRenderTime;

        public double MinimumDelayTicks { get; }
        public double MaximumDelayTicks { get; }
        public double CurrentDelayTicks { get; private set; }
        public double TargetDelayTicks { get; private set; }

        /// <param name="tickRate">服务器Tick/秒，正整数。</param>
        /// <param name="snapshotRate">快照/秒，正整数；不高于Tick率。</param>
        /// <param name="configuredMinimumTicks">已有插值配置下限，单位Tick，有限且非负。</param>
        public AdaptiveInterpolationClock(int tickRate, int snapshotRate, double configuredMinimumTicks)
        {
            if (tickRate <= 0 || snapshotRate <= 0 || !IsFiniteNonnegative(configuredMinimumTicks))
            {
                throw new ArgumentOutOfRangeException(nameof(configuredMinimumTicks));
            }
            tickMilliseconds = 1000d / tickRate;
            // 覆盖最大发送间隔；50Hz/20Hz至少3 Tick，而非沿用原2 Tick导致周期性外推。
            MinimumDelayTicks = System.Math.Max(configuredMinimumTicks,
                System.Math.Ceiling(tickRate / (double)System.Math.Min(snapshotRate, tickRate)));
            MaximumDelayTicks = System.Math.Max(MinimumDelayTicks, 12d);
            Reset();
        }

        public void Update(ulong nowMicroseconds, bool hasFreshQuality, double rttMilliseconds, double jitterMilliseconds)
        {
            if (hasFreshQuality && (!IsFiniteNonnegative(rttMilliseconds) || !IsFiniteNonnegative(jitterMilliseconds)))
            {
                throw new ArgumentOutOfRangeException(nameof(rttMilliseconds));
            }
            // RTT一半仅作对称链路下的下行估算；2倍RTT变化均值作余量，不声称直接测到了下行抖动。
            TargetDelayTicks = hasFreshQuality
                ? System.Math.Min(MaximumDelayTicks, MinimumDelayTicks
                    + (rttMilliseconds * 0.5d + jitterMilliseconds * 2d) / tickMilliseconds)
                : MinimumDelayTicks;
            if (!hasUpdate)
            {
                lastUpdateMicroseconds = nowMicroseconds;
                hasUpdate = true;
                return;
            }
            if (nowMicroseconds <= lastUpdateMicroseconds)
            {
                return;
            }
            // 增加最多4 Tick/秒，回退1 Tick/秒；每次最多消耗250ms，暂停/卡顿不造成延迟突跳。
            double seconds = System.Math.Min((nowMicroseconds - lastUpdateMicroseconds) / 1000000d, 0.25d);
            lastUpdateMicroseconds = nowMicroseconds;
            double change = TargetDelayTicks - CurrentDelayTicks;
            CurrentDelayTicks += change >= 0d
                ? System.Math.Min(change, seconds * 4d)
                : System.Math.Max(change, -seconds);
        }

        public bool TryGetRenderTime(double estimatedServerTick, out double continuousEstimate, out double renderTime)
        {
            continuousEstimate = default;
            renderTime = default;
            if (!IsFiniteNonnegative(estimatedServerTick))
            {
                return false;
            }
            if (!hasRenderTime)
            {
                unwrappedEstimate = estimatedServerTick;
            }
            else
            {
                // TimeSync给连续Tick，过期回退可能给原始uint。选择最近回绕世代，避免切换时冻结约2^32 Tick。
                // 连续观察间隔必须小于半个uint周期；断线重连通过Reset建立新历史。
                unwrappedEstimate += System.Math.IEEERemainder(estimatedServerTick - lastEstimate, TickPeriod);
            }
            lastEstimate = estimatedServerTick;
            continuousEstimate = unwrappedEstimate;
            double target = unwrappedEstimate - CurrentDelayTicks;
            lastRenderTime = hasRenderTime ? System.Math.Max(lastRenderTime, target) : target;
            hasRenderTime = true;
            renderTime = lastRenderTime;
            return true;
        }

        public void Reset()
        {
            CurrentDelayTicks = MinimumDelayTicks;
            TargetDelayTicks = MinimumDelayTicks;
            hasUpdate = false;
            hasRenderTime = false;
            lastUpdateMicroseconds = 0;
            lastEstimate = 0d;
            unwrappedEstimate = 0d;
            lastRenderTime = 0d;
        }

        private static bool IsFiniteNonnegative(double value) =>
            value >= 0d && !double.IsNaN(value) && !double.IsInfinity(value);
    }
}

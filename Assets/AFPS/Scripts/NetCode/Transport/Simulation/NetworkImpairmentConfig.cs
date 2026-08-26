using System;

namespace AFPS.NetCode.Transport.Simulation
{
    /// <summary>
    /// 描述不可靠数据包在测试环境中遭受的延迟、抖动、丢包和乱序参数。
    /// 所有时间单位均为秒，概率范围为 0 到 1。
    /// </summary>
    public readonly struct NetworkImpairmentConfig
    {
        /// <summary>
        /// 每个不可靠数据包必定增加的单向固定延迟，单位为秒。
        /// </summary>
        public readonly double BaseLatencySeconds;

        /// <summary>
        /// 在正负范围内随机添加到固定延迟的抖动，单位为秒。
        /// 最终单包延迟不会小于零。
        /// </summary>
        public readonly double JitterSeconds;

        /// <summary>
        /// 不可靠数据包在交给真实传输层前被丢弃的概率。
        /// 模拟丢包仍向上层返回发送成功，因为发送方无法立即知道网络中发生了丢包。
        /// </summary>
        public readonly double PacketLossProbability;

        /// <summary>
        /// 新数据包与仍在队列中的前一数据包交换到达顺序的概率。
        /// </summary>
        public readonly double ReorderProbability;

        /// <summary>
        /// 发生乱序时额外施加给前一数据包的延迟，单位为秒。
        /// </summary>
        public readonly double ReorderExtraDelaySeconds;

        /// <summary>
        /// 决定抖动、丢包和乱序序列的固定随机种子。
        /// 相同配置、种子和发送顺序会得到相同结果。
        /// </summary>
        public readonly uint RandomSeed;

        /// <summary>
        /// 等待释放的数据包数量上限，防止极端配置导致测试进程无界占用内存。
        /// </summary>
        public readonly int MaxQueuedPackets;

        public NetworkImpairmentConfig(double baseLatencySeconds, double jitterSeconds, double packetLossProbability, double reorderProbability, double reorderExtraDelaySeconds, uint randomSeed, int maxQueuedPackets)
        {
            if (baseLatencySeconds < 0d || double.IsNaN(baseLatencySeconds) || double.IsInfinity(baseLatencySeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(baseLatencySeconds));
            }

            if (jitterSeconds < 0d || double.IsNaN(jitterSeconds) || double.IsInfinity(jitterSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(jitterSeconds));
            }

            if (packetLossProbability < 0d || packetLossProbability > 1d || double.IsNaN(packetLossProbability))
            {
                throw new ArgumentOutOfRangeException(nameof(packetLossProbability));
            }

            if (reorderProbability < 0d || reorderProbability > 1d || double.IsNaN(reorderProbability))
            {
                throw new ArgumentOutOfRangeException(nameof(reorderProbability));
            }

            if (reorderExtraDelaySeconds < 0d || double.IsNaN(reorderExtraDelaySeconds) || double.IsInfinity(reorderExtraDelaySeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(reorderExtraDelaySeconds));
            }

            if (reorderProbability > 0d && reorderExtraDelaySeconds <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(reorderExtraDelaySeconds), "启用乱序时必须提供大于零的额外延迟。");
            }

            if (maxQueuedPackets <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxQueuedPackets));
            }

            BaseLatencySeconds = baseLatencySeconds;
            JitterSeconds = jitterSeconds;
            PacketLossProbability = packetLossProbability;
            ReorderProbability = reorderProbability;
            ReorderExtraDelaySeconds = reorderExtraDelaySeconds;
            RandomSeed = randomSeed;
            MaxQueuedPackets = maxQueuedPackets;
        }
    }
}

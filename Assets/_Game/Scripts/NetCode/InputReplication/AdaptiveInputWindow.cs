using System;

namespace AFPS.NetCode.InputReplication
{
    /// <summary>服务器连接级网络策略；只选择等待/替代输入，不改变PlayerSimulation的数学规则。</summary>
    public sealed class AdaptiveInputWindow
    {
        private readonly int baseWait;
        private readonly int baseRepeat;
        private readonly int maximumWait;
        private readonly int maximumRepeat;
        private readonly double tickMilliseconds;
        public int WaitTicks { get; private set; }
        public int RepeatTicks { get; private set; }

        public AdaptiveInputWindow(int tickRate, int configuredWait, int configuredRepeat, int inputCapacity)
        {
            if (tickRate <= 0 || configuredWait < 0 || configuredRepeat < 0 || inputCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(inputCapacity));
            }
            tickMilliseconds = 1000d / tickRate;
            baseWait = configuredWait;
            baseRepeat = configuredRepeat;
            // 默认上限8/6 Tick，亦不主动超出接收窗口。显式较大旧配置保留为下限；0表示禁用该策略。
            maximumWait = configuredWait == 0 ? 0 : System.Math.Max(baseWait, System.Math.Min(8, inputCapacity - 1));
            maximumRepeat = configuredRepeat == 0 ? 0 : System.Math.Max(baseRepeat, System.Math.Min(6, inputCapacity - 1));
            Reset();
        }

        /// <param name="rttMilliseconds">服务器闭环测得的RTT，单位毫秒，有限且非负。</param>
        /// <param name="jitterMilliseconds">相邻RTT变化的平滑均值，单位毫秒，有限且非负。</param>
        public void Observe(double rttMilliseconds, double jitterMilliseconds)
        {
            if (!IsValid(rttMilliseconds) || !IsValid(jitterMilliseconds))
            {
                throw new ArgumentOutOfRangeException(nameof(rttMilliseconds));
            }
            int wait = Target(baseWait, maximumWait, rttMilliseconds * 0.5d + jitterMilliseconds * 2d);
            int repeat = Target(baseRepeat, maximumRepeat, rttMilliseconds * 0.5d + jitterMilliseconds);
            // 升级立即生效以容纳迟到输入；每个新样本最多缩小1 Tick，避免阈值抖动反复制造缺口。
            WaitTicks = wait >= WaitTicks ? wait : System.Math.Max(wait, WaitTicks - 1);
            RepeatTicks = repeat >= RepeatTicks ? repeat : System.Math.Max(repeat, RepeatTicks - 1);
        }

        public void Reset()
        {
            WaitTicks = baseWait;
            RepeatTicks = baseRepeat;
        }

        private int Target(int minimum, int maximum, double milliseconds) =>
            (int)System.Math.Max(minimum, System.Math.Min(maximum, System.Math.Ceiling(milliseconds / tickMilliseconds)));

        private static bool IsValid(double value) => value >= 0d && !double.IsNaN(value) && !double.IsInfinity(value);
    }
}

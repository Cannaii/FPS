using System;
using AFPS.Math;

namespace AFPS.Simulation.Characters
{
    /// <summary>输入轴唯一规范：-127..127 档位映射至 Q16.16，端点精确为 ±1。</summary>
    public static class InputAxisGrid
    {
        public const int GridMax = 127;

        public static Fix ToFix(int grid)
        {
            grid = System.Math.Max(-GridMax, System.Math.Min(GridMax, grid));
            return Fix.FromRaw((int)RoundNearest((long)grid * Fix.Scale, GridMax));
        }

        public static sbyte FromFix(Fix value)
        {
            int raw = System.Math.Max(-(int)Fix.Scale, System.Math.Min((int)Fix.Scale, value.Raw));
            return (sbyte)RoundNearest((long)raw * GridMax, Fix.Scale);
        }

        /// <summary>设备采集边界专用；后续预测/编解码不再对 float 做量化往返。</summary>
        public static sbyte FromFloat(float value)
        {
            if (float.IsNaN(value)) return 0;
            double clamped = System.Math.Max(-1d, System.Math.Min(1d, value));
            return (sbyte)System.Math.Round(clamped * GridMax, MidpointRounding.AwayFromZero);
        }

        internal static long RoundNearest(long numerator, long denominator) => numerator >= 0
            ? (numerator + denominator / 2) / denominator
            : -((-numerator + denominator / 2) / denominator);
    }
}

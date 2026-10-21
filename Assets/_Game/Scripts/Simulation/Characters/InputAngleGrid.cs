using System;
using AFPS.Math;

namespace AFPS.Simulation.Characters
{
    /// <summary>输入观察角以百分度存储。与既有线格式一致，模拟可通过 Fix 度数视图读取。</summary>
    public static class InputAngleGrid
    {
        public const int YawSteps = 36000;
        public const int MaximumPitch = 8990;
        private const int FullTurnRaw = 360 * (int)Fix.Scale;
        public static bool IsValid(ushort yaw, short pitch) => yaw < YawSteps && pitch >= -MaximumPitch && pitch <= MaximumPitch;
        public static Fix YawToFix(ushort centidegrees)
        {
            if (centidegrees >= YawSteps)
            {
                throw new ArgumentOutOfRangeException(nameof(centidegrees));
            }

            return Fix.FromRaw((int)InputAxisGrid.RoundNearest(centidegrees * Fix.Scale, 100));
        }

        public static Fix PitchToFix(short centidegrees)
        {
            if (centidegrees < -MaximumPitch || centidegrees > MaximumPitch)
            {
                throw new ArgumentOutOfRangeException(nameof(centidegrees));
            }

            return Fix.FromRaw((int)InputAxisGrid.RoundNearest(centidegrees * Fix.Scale, 100));
        }

        public static ushort YawFromFix(Fix degrees)
        {
            int raw = degrees.Raw % FullTurnRaw;
            if (raw < 0)
            {
                raw += FullTurnRaw;
            }

            return (ushort)(InputAxisGrid.RoundNearest((long)raw * 100, Fix.Scale) % YawSteps);
        }

        public static short PitchFromFix(Fix degrees)
        {
            long rounded = InputAxisGrid.RoundNearest((long)degrees.Raw * 100, Fix.Scale);
            return (short)System.Math.Max(-MaximumPitch, System.Math.Min(MaximumPitch, rounded));
        }

        /// <summary>设备观察角边界；非有限数映射为中立角，360° 边界回绕到零。</summary>
        public static ushort YawFromFloat(float degrees)
        {
            if (float.IsNaN(degrees) || float.IsInfinity(degrees))
            {
                return 0;
            }

            double normalized = (double)degrees % 360d;
            if (normalized < 0d)
            {
                normalized += 360d;
            }

            return (ushort)((int)System.Math.Round(normalized * 100d, MidpointRounding.AwayFromZero) % YawSteps);
        }

        public static short PitchFromFloat(float degrees)
        {
            if (float.IsNaN(degrees) || float.IsInfinity(degrees))
            {
                return 0;
            }

            double clamped = System.Math.Max(-89.9d, System.Math.Min(89.9d, degrees));
            return (short)System.Math.Round(clamped * 100d, MidpointRounding.AwayFromZero);
        }
    }
}

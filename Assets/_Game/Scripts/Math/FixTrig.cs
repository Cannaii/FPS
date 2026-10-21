namespace AFPS.Math
{
    /// <summary>4096 槽正弦 LUT + Raw 线性插值；角度单位为弧度。</summary>
    public static partial class FixTrig
    {
        public const int TableSize = 4096;
        public static readonly Fix Pi = Fix.FromRaw(205887);
        public static readonly Fix HalfPi = Fix.FromRaw(102944);
        // 沿用方案约定的周期 Raw；四分之一周期落在半个 Raw，查询时保留分子精度。
        public static readonly Fix TwoPi = Fix.FromRaw(411774);
        public static Fix Sin(Fix radians) => Evaluate(radians, 0);
        public static Fix Cos(Fix radians) => Evaluate(radians, TableSize / 4);
        private static Fix Evaluate(Fix radians, int phaseSlots)
        {
            long wrapped = (long)radians.Raw % TwoPi.Raw;
            if (wrapped < 0)
            {
                wrapped += TwoPi.Raw;
            }

            // 先乘后除必须使用 long；Fix 中间值会在 4096 槽映射时溢出。
            long phase = wrapped * TableSize + (long)phaseSlots * TwoPi.Raw;
            int index = (int)(phase / TwoPi.Raw) % TableSize;
            long remainder = phase % TwoPi.Raw;
            int a = SinTableRaw[index];
            int b = SinTableRaw[(index + 1) % TableSize];
            long numerator = (b - a) * remainder;
            long offset = numerator >= 0 ? (numerator + TwoPi.Raw / 2) / TwoPi.Raw : -((-numerator + TwoPi.Raw / 2) / TwoPi.Raw);
            return Fix.FromRaw(a + (int)offset);
        }
    }
}

namespace AFPS.Math
{
    /// <summary>模拟用的整数角度换算，输入度数，输出 LUT 弧度。</summary>
    public static class FixAngles
    {
        public static Fix DegreesToRadians(Fix degrees)
        {
            long numerator = (long)degrees.Raw * FixTrig.TwoPi.Raw;
            long denominator = 360 * Fix.Scale;
            long raw = numerator >= 0 ? (numerator + denominator / 2) / denominator : -((-numerator + denominator / 2) / denominator);
            return Fix.FromRaw(checked((int)raw));
        }
    }
}

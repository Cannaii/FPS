using System;

namespace AFPS.Math
{
    /// <summary>模拟可用的纯整数数学；所有返回值使用 Q16.16。</summary>
    public static class FixMath
    {
        public static Fix Min(Fix a, Fix b) => a < b ? a : b;
        public static Fix Max(Fix a, Fix b) => a > b ? a : b;
        public static Fix Abs(Fix value) => value.Raw < 0 ? -value : value;
        public static Fix Clamp(Fix value, Fix min, Fix max)
        {
            if (min > max) throw new ArgumentException("min must not exceed max.");
            return value < min ? min : value > max ? max : value;
        }
        public static Fix Clamp01(Fix value) => Clamp(value, Fix.Zero, Fix.One);
        public static Fix Lerp(Fix a, Fix b, Fix t)
        {
            // 差值先提升为 long，允许跨越整个 Fix 范围而不产生中间溢出。
            long delta = (long)b.Raw - a.Raw;
            long product = delta * Clamp01(t).Raw;
            long rounded = product / Fix.Scale;
            long remainder = product % Fix.Scale;
            if (remainder >= Fix.HalfScale) rounded++;
            else if (remainder <= -Fix.HalfScale) rounded--;
            return Fix.FromRaw(checked((int)(a.Raw + rounded)));
        }
        public static Fix MoveTowards(Fix current, Fix target, Fix maxDelta)
        {
            if (maxDelta.Raw < 0) throw new ArgumentOutOfRangeException(nameof(maxDelta));
            long delta = (long)target.Raw - current.Raw;
            if (delta >= -maxDelta.Raw && delta <= maxDelta.Raw) return target;
            return Fix.FromRaw(checked(current.Raw + (delta > 0 ? maxDelta.Raw : -maxDelta.Raw)));
        }
        /// <summary>向下取整的整数平方根，满足 r² ≤ x.Raw×Scale &lt; (r+1)²。</summary>
        public static Fix Sqrt(Fix value)
        {
            if (value.Raw < 0) throw new ArgumentOutOfRangeException(nameof(value));
            return Fix.FromRaw(checked((int)IntegerSqrt((ulong)value.Raw * (ulong)Fix.Scale)));
        }
        // 二进制逐位求根，不依赖初始猜测或浮点；向量长度也复用这一实现。
        internal static ulong IntegerSqrt(ulong value)
        {
            ulong result = 0;
            ulong bit = 1UL << 62;
            while (bit > value) bit >>= 2;
            while (bit != 0)
            {
                if (value >= result + bit)
                {
                    value -= result + bit;
                    result = (result >> 1) + bit;
                }
                else result >>= 1;
                bit >>= 2;
            }
            return result;
        }
    }
}

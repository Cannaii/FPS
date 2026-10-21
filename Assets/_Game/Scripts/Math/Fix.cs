using System;
using System.Globalization;

namespace AFPS.Math
{
    /// <summary>Q16.16 有符号定点数。运算溢出始终抛异常，禁止静默回绕污染模拟。</summary>
    [Serializable]
    public readonly struct Fix : IEquatable<Fix>, IComparable<Fix>
    {
        // 低 16 位表示小数；1 Raw = 1/65536，不能把 Raw 当作完整的米或秒。
        public const int FractionalBits = 16;
        public const long Scale = 1L << FractionalBits;
        public const long HalfScale = Scale / 2;
        private readonly int raw;
        public int Raw => raw;

        public static readonly Fix Zero = FromRaw(0);
        public static readonly Fix One = FromRaw((int)Scale);
        public static readonly Fix Half = FromRaw((int)HalfScale);
        public static readonly Fix Epsilon = FromRaw(1);
        public static readonly Fix MinValue = FromRaw(int.MinValue);
        public static readonly Fix MaxValue = FromRaw(int.MaxValue);
        private Fix(int value) => raw = value;
        public static Fix FromRaw(int value) => new Fix(value);
        public static Fix FromInt(int value) => FromRaw(checked((int)(value * Scale)));
        /// <summary>配置/输入边界专用；模拟热路径不得调用浮点转换。</summary>
        public static Fix FromFloat(float value) => FromDouble(value);
        public static Fix FromDouble(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            return FromRaw(checked((int)System.Math.Round(value * Scale, MidpointRounding.AwayFromZero)));
        }

        public static Fix FromRatio(int numerator, int denominator)
        {
            if (denominator == 0)
            {
                throw new DivideByZeroException();
            }

            // 与除法保持一致：整数商向零截断，乘法则使用半格远离零舍入。
            return FromRaw(checked((int)(numerator * Scale / denominator)));
        }

        public float ToFloat() => raw / (float)Scale;
        public double ToDouble() => raw / (double)Scale;
        /// <summary>向零截断；负值不能用算术右移代替。</summary>
        public int ToInt() => (int)(raw / Scale);
        public static Fix operator +(Fix a, Fix b) => FromRaw(checked(a.raw + b.raw));
        public static Fix operator -(Fix a, Fix b) => FromRaw(checked(a.raw - b.raw));
        public static Fix operator -(Fix value) => FromRaw(checked(-value.raw));
        public static Fix operator *(Fix a, Fix b) => FromRaw(RoundProduct((long)a.raw * b.raw));
        public static Fix operator /(Fix a, Fix b)
        {
            if (b.raw == 0)
            {
                throw new DivideByZeroException();
            }

            return FromRaw(checked((int)(a.raw * Scale / b.raw)));
        }

        public static Fix operator %(Fix a, Fix b)
        {
            if (b.raw == 0)
            {
                throw new DivideByZeroException();
            }

            return FromRaw((int)((long)a.raw % b.raw));
        }

        /// <summary>Q32.32 中间乘积还原为 Raw，恰好半格时远离零。</summary>
        internal static int RoundProduct(long product)
        {
            long quotient = product / Scale;
            long remainder = product % Scale;
            if (remainder >= HalfScale)
            {
                quotient++;
            }
            else if (remainder <= -HalfScale)
            {
                quotient--;
            }

            return checked((int)quotient);
        }

        public static bool operator ==(Fix a, Fix b) => a.raw == b.raw;
        public static bool operator !=(Fix a, Fix b) => a.raw != b.raw;
        public static bool operator <(Fix a, Fix b) => a.raw < b.raw;
        public static bool operator>(Fix a, Fix b) => a.raw > b.raw;
        public static bool operator <=(Fix a, Fix b) => a.raw <= b.raw;
        public static bool operator >=(Fix a, Fix b) => a.raw >= b.raw;
        public int CompareTo(Fix other) => raw.CompareTo(other.raw);
        public bool Equals(Fix other) => raw == other.raw;
        public override bool Equals(object obj) => obj is Fix other && Equals(other);
        public override int GetHashCode() => raw;
        public override string ToString() => ToDouble().ToString("F4", CultureInfo.InvariantCulture);
    }
}

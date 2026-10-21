using System;
using BigInteger = System.Numerics.BigInteger;

namespace AFPS.Simulation.World
{
    /// <summary>非负入射时间 (N - sqrt(D)) / Den，比较保持精确，最终距离才向下取整。</summary>
    internal readonly struct CapsuleSweepTime : IComparable<CapsuleSweepTime>
    {
        // t 的单位是 Q16.16 距离的 Raw；保持根式形式直到最终返回，避免近邻命中顺序颠倒。
        private readonly BigInteger numerator;
        private readonly BigInteger discriminant;
        private readonly BigInteger denominator;
        private CapsuleSweepTime(BigInteger numerator, BigInteger discriminant, BigInteger denominator)
        {
            this.numerator = numerator;
            this.discriminant = discriminant;
            this.denominator = denominator;
        }

        public static CapsuleSweepTime Rational(BigInteger numerator, BigInteger denominator)
        {
            if (denominator == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(denominator));
            }

            if (denominator < 0)
            {
                numerator = -numerator;
                denominator = -denominator;
            }

            return new CapsuleSweepTime(numerator, BigInteger.Zero, denominator);
        }

        /// <param name="maximum">本次扫掠的最大距离 Raw，含端点。</param>
        /// <param name="rejectTangent">三角形内边的纯切触不阻挡运动；盒体保持既有接触规则。</param>
        public static bool TryQuadratic(BigInteger a, BigInteger b, BigInteger c, int maximum, out CapsuleSweepTime time, bool rejectTangent = false)
        {
            time = default;
            if (a.Sign <= 0)
            {
                return false;
            }

            BigInteger d = b * b - 4 * a * c;
            if (d.Sign < 0 || (rejectTangent && d.IsZero))
            {
                return false;
            }

            time = new CapsuleSweepTime(-b, d, 2 * a);
            return time.CompareToFraction(0, 1) >= 0 && time.CompareToFraction(maximum, 1) <= 0;
        }

        public int FloorRaw
        {
            get
            {
                BigInteger sqrt = SqrtFloor(discriminant);
                // 非平方数的根略大于 floor(sqrt)，若分子恰好整除，真实商仍应提前一 Raw。
                BigInteger n = numerator - sqrt - (sqrt * sqrt == discriminant ? 0 : 1);
                BigInteger q = BigInteger.DivRem(n, denominator, out BigInteger remainder);
                if (remainder.Sign < 0)
                {
                    q--;
                }

                return checked((int)q);
            }
        }

        public int SignOfLinear(BigInteger constant, BigInteger slope)
        {
            if (slope.IsZero)
            {
                return constant.Sign;
            }

            int order = CompareToFraction(-constant, slope);
            return slope.Sign < 0 ? -order : order;
        }

        public int CompareToFraction(BigInteger n, BigInteger d)
        {
            if (d == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(d));
            }

            if (d < 0)
            {
                n = -n;
                d = -d;
            }

            BigInteger left = numerator * d - denominator * n;
            if (left.Sign < 0)
            {
                return -1;
            }

            return (left * left).CompareTo(discriminant * d * d);
        }

        public int CompareTo(CapsuleSweepTime other)
        {
            // 比较 U + Den*sqrt(other.D) 与 other.Den*sqrt(D)。两次平方只在两侧非负时进行。
            BigInteger u = numerator * other.denominator - other.numerator * denominator;
            if (u.Sign < 0)
            {
                return -other.CompareTo(this);
            }

            BigInteger v = u * u + denominator * denominator * other.discriminant - other.denominator * other.denominator * discriminant;
            BigInteger w = 2 * u * denominator;
            if (v.Sign >= 0)
            {
                return v.IsZero && (w.IsZero || other.discriminant.IsZero) ? 0 : 1;
            }

            return (w * w * other.discriminant).CompareTo(v * v);
        }

        private static BigInteger SqrtFloor(BigInteger value)
        {
            if (value.IsZero)
            {
                return BigInteger.Zero;
            }

            int bits = 0;
            for (BigInteger n = value; n.Sign > 0; n >>= 1)
            {
                bits++;
            }

            BigInteger guess = BigInteger.One << ((bits + 1) / 2);
            while (true)
            {
                BigInteger next = (guess + value / guess) >> 1;
                if (next >= guess)
                {
                    return guess;
                }

                guess = next;
            }
        }
    }
}

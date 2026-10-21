using System;
using BigInteger = System.Numerics.BigInteger;

namespace AFPS.Simulation.World
{
    /// <summary>非负入射时间 (N - sqrt(D)) / Den，比较保持精确，最终距离才向下取整。</summary>
    internal readonly struct CapsuleSweepTime : IComparable<CapsuleSweepTime>
    {
        private readonly BigInteger numerator;
        private readonly BigInteger discriminant;
        private readonly BigInteger denominator;

        private CapsuleSweepTime(BigInteger numerator, BigInteger discriminant, BigInteger denominator)
        {
            this.numerator = numerator;
            this.discriminant = discriminant;
            this.denominator = denominator;
        }

        public static CapsuleSweepTime Rational(long numerator, int denominator)
        {
            if (denominator == 0) throw new ArgumentOutOfRangeException(nameof(denominator));
            if (denominator < 0) { numerator = -numerator; denominator = -denominator; }
            return new CapsuleSweepTime(numerator, BigInteger.Zero, denominator);
        }

        public static bool TryQuadratic(BigInteger a, BigInteger b, BigInteger c, int maximum, out CapsuleSweepTime time)
        {
            time = default;
            if (a.Sign <= 0) return false;
            BigInteger d = b * b - 4 * a * c;
            if (d.Sign < 0) return false;
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
                if (remainder.Sign < 0) q--;
                return checked((int)q);
            }
        }

        public int CompareToFraction(long n, int d)
        {
            if (d == 0) throw new ArgumentOutOfRangeException(nameof(d));
            if (d < 0) { n = -n; d = -d; }
            BigInteger left = numerator * d - denominator * n;
            if (left.Sign < 0) return -1;
            return (left * left).CompareTo(discriminant * d * d);
        }

        public int CompareTo(CapsuleSweepTime other)
        {
            // 比较 U + Den*sqrt(other.D) 与 other.Den*sqrt(D)。两次平方只在两侧非负时进行。
            BigInteger u = numerator * other.denominator - other.numerator * denominator;
            if (u.Sign < 0) return -other.CompareTo(this);
            BigInteger v = u * u + denominator * denominator * other.discriminant - other.denominator * other.denominator * discriminant;
            BigInteger w = 2 * u * denominator;
            if (v.Sign >= 0) return v.IsZero && (w.IsZero || other.discriminant.IsZero) ? 0 : 1;
            return (w * w * other.discriminant).CompareTo(v * v);
        }

        private static BigInteger SqrtFloor(BigInteger value)
        {
            if (value.IsZero) return BigInteger.Zero;
            int bits = 0;
            for (BigInteger n = value; n.Sign > 0; n >>= 1) bits++;
            BigInteger guess = BigInteger.One << ((bits + 1) / 2);
            while (true)
            {
                BigInteger next = (guess + value / guess) >> 1;
                if (next >= guess) return guess;
                guess = next;
            }
        }
    }
}

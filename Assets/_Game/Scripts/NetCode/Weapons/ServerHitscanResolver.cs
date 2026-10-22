using System;
using System.Collections.Generic;
using AFPS.Math;
using BigInteger = System.Numerics.BigInteger;

namespace AFPS.NetCode.Weapons
{
    /// <summary>以两线段最近点判定竖直胶囊命中；保留旧玩法语义，不把最近点改成表面入射点。</summary>
    public static partial class ServerHitscanResolver
    {
        public static bool TryResolveFixed(
            in AuthoritativeShot shot,
            IReadOnlyList<ServerCombatTarget> targets,
            out ServerCombatTarget target,
            out FixVec3 hitPoint)
        {
            target = default;
            hitPoint = PointAtDistance(shot.OriginFixed, shot.DirectionFixed, shot.RangeFixed.Raw);
            if (targets == null || shot.RangeFixed.Raw <= 0 || shot.DirectionFixed == FixVec3.Zero)
            {
                return false;
            }

            bool found = false;
            Fraction nearest = Fraction.One;
            // 坐标先统一放大 Scale：射线端点保留完整乘积，不提前舍入，不要求端点落在 int Raw 域。
            long dx = (long)shot.DirectionFixed.X.Raw * shot.RangeFixed.Raw;
            long dy = (long)shot.DirectionFixed.Y.Raw * shot.RangeFixed.Raw;
            long dz = (long)shot.DirectionFixed.Z.Raw * shot.RangeFixed.Raw;
            BigInteger a = Dot(dx, dy, dz, dx, dy, dz);
            for (int index = 0; index < targets.Count; index++)
            {
                ServerCombatTarget candidate = targets[index];
                if (candidate.EntityId == 0 || candidate.EntityId == shot.EntityId
                    || candidate.HealthFixed.Raw <= 0 || candidate.RadiusFixed.Raw <= 0
                    || (long)candidate.HeightFixed.Raw < 2L * candidate.RadiusFixed.Raw)
                {
                    continue;
                }

                long rx = ((long)shot.OriginFixed.X.Raw - candidate.PositionFixed.X.Raw) * Fix.Scale;
                long ry = ((long)shot.OriginFixed.Y.Raw - candidate.PositionFixed.Y.Raw - candidate.RadiusFixed.Raw) * Fix.Scale;
                long rz = ((long)shot.OriginFixed.Z.Raw - candidate.PositionFixed.Z.Raw) * Fix.Scale;
                long axis = ((long)candidate.HeightFixed.Raw - 2L * candidate.RadiusFixed.Raw) * Fix.Scale;
                BigInteger e = (BigInteger)axis * axis;
                BigInteger b = (BigInteger)dy * axis;
                BigInteger c = Dot(dx, dy, dz, rx, ry, rz);
                BigInteger f = (BigInteger)axis * ry;
                Fraction s;
                Fraction t;
                if (e.IsZero)
                {
                    // 高度恰为直径时退化为球；不能除以零长度胶囊轴。
                    t = Fraction.Zero;
                    s = new Fraction(-c, a).Clamp01();
                }
                else
                {
                    BigInteger denominator = a * e - b * b;
                    s = denominator.IsZero
                        ? Fraction.Zero
                        : new Fraction(b * f - c * e, denominator).Clamp01();
                    t = (s * b + new Fraction(f, 1)) / e;
                    if (t.CompareTo(Fraction.Zero) < 0)
                    {
                        t = Fraction.Zero;
                        s = new Fraction(-c, a).Clamp01();
                    }
                    else if (t.CompareTo(Fraction.One) > 0)
                    {
                        t = Fraction.One;
                        s = new Fraction(b - c, a).Clamp01();
                    }
                }

                Fraction x = new Fraction(rx, 1) + s * dx;
                Fraction y = new Fraction(ry, 1) + s * dy - t * axis;
                Fraction z = new Fraction(rz, 1) + s * dz;
                BigInteger radius = (BigInteger)candidate.RadiusFixed.Raw * Fix.Scale;
                Fraction squared = x * x + y * y + z * z;
                if (squared.CompareTo(new Fraction(radius * radius, 1)) > 0)
                {
                    continue;
                }

                // 比较未舍入的距离；完全同距时选小 EntityId，不依赖字典/输入数组顺序。
                int order = s.CompareTo(nearest);
                if (found && (order > 0 || (order == 0 && candidate.EntityId >= target.EntityId)))
                {
                    continue;
                }
                found = true;
                nearest = s;
                target = candidate;
                hitPoint = new FixVec3(
                    Coordinate(shot.OriginFixed.X.Raw, dx, s),
                    Coordinate(shot.OriginFixed.Y.Raw, dy, s),
                    Coordinate(shot.OriginFixed.Z.Raw, dz, s));
            }
            return found;
        }

        /// <summary>只对返回坐标舍入：半 Raw 远离零，超出表示域的视觉端点饱和到 int32 边界。</summary>
        public static FixVec3 PointAtDistance(in FixVec3 origin, in FixVec3 direction, int distanceRaw) => new FixVec3(
            Coordinate(origin.X.Raw, (long)direction.X.Raw * distanceRaw, Fraction.One),
            Coordinate(origin.Y.Raw, (long)direction.Y.Raw * distanceRaw, Fraction.One),
            Coordinate(origin.Z.Raw, (long)direction.Z.Raw * distanceRaw, Fraction.One));

        private static Fix Coordinate(int origin, long deltaScaled, Fraction fraction)
        {
            Fraction scaled = new Fraction((long)origin * Fix.Scale, 1) + fraction * deltaScaled;
            BigInteger denominator = scaled.Denominator * Fix.Scale;
            BigInteger rounded = BigInteger.DivRem(scaled.Numerator, denominator, out BigInteger remainder);
            if (BigInteger.Abs(remainder) * 2 >= denominator)
            {
                rounded += scaled.Numerator.Sign;
            }
            return Fix.FromRaw((int)BigInteger.Max(int.MinValue, BigInteger.Min(int.MaxValue, rounded)));
        }

        private static BigInteger Dot(long ax, long ay, long az, long bx, long by, long bz) =>
            (BigInteger)ax * bx + (BigInteger)ay * by + (BigInteger)az * bz;

        // 仅用于精确几何中间值。约分控制乘积规模，避免 Q16.16 点积/比例的提前舍入和溢出。
        private readonly struct Fraction : IComparable<Fraction>
        {
            public readonly BigInteger Numerator;
            public readonly BigInteger Denominator;
            public static readonly Fraction Zero = new Fraction(0, 1);
            public static readonly Fraction One = new Fraction(1, 1);

            public Fraction(BigInteger numerator, BigInteger denominator)
            {
                if (denominator.Sign < 0)
                {
                    numerator = -numerator;
                    denominator = -denominator;
                }
                BigInteger gcd = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), denominator);
                Numerator = numerator / gcd;
                Denominator = denominator / gcd;
            }

            public Fraction Clamp01() => Numerator.Sign < 0 ? Zero : Numerator > Denominator ? One : this;
            public int CompareTo(Fraction other) =>
                (Numerator * other.Denominator).CompareTo(other.Numerator * Denominator);
            public static Fraction operator +(Fraction a, Fraction b) => new Fraction(
                a.Numerator * b.Denominator + b.Numerator * a.Denominator, a.Denominator * b.Denominator);
            public static Fraction operator -(Fraction a, Fraction b) => new Fraction(
                a.Numerator * b.Denominator - b.Numerator * a.Denominator, a.Denominator * b.Denominator);
            public static Fraction operator *(Fraction a, Fraction b) =>
                new Fraction(a.Numerator * b.Numerator, a.Denominator * b.Denominator);
            public static Fraction operator *(Fraction a, BigInteger b) => new Fraction(a.Numerator * b, a.Denominator);
            public static Fraction operator /(Fraction a, BigInteger b) => new Fraction(a.Numerator, a.Denominator * b);
        }
    }
}

using System;

namespace AFPS.Simulation.World
{
    /// <summary>单位为距离 Raw 的有理数。分母保持正值，避免近乎平行射线提前截断为相同距离。</summary>
    internal readonly struct RayDistance : IComparable<RayDistance>
    {
        public readonly long Numerator;
        public readonly int Denominator;
        public static readonly RayDistance Zero = new RayDistance(0, 1);

        public RayDistance(long numerator, int denominator)
        {
            Numerator = denominator < 0 ? -numerator : numerator;
            Denominator = denominator < 0 ? -denominator : denominator;
        }

        public long Floor
        {
            get
            {
                long quotient = Numerator / Denominator;
                return Numerator % Denominator < 0 ? quotient - 1 : quotient;
            }
        }

        public int CompareTo(RayDistance other)
        {
            // 直接交叉相乘可能超出 long。先比较整数部分，再交叉比较小于分母的余数。
            long a = Floor, b = other.Floor;
            int result = a.CompareTo(b);
            if (result != 0) return result;
            long remainderA = Numerator % Denominator;
            long remainderB = other.Numerator % other.Denominator;
            if (remainderA < 0) remainderA += Denominator;
            if (remainderB < 0) remainderB += other.Denominator;
            return (remainderA * other.Denominator).CompareTo(remainderB * Denominator);
        }
    }
}

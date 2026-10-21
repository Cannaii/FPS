using System;

namespace AFPS.Math
{
    /// <summary>引擎无关的定点向量。长度使用宽整数平方和，避免 1000m 坐标先平方即溢出。</summary>
    [Serializable]
    public readonly struct FixVec3 : IEquatable<FixVec3>
    {
        public readonly Fix X;
        public readonly Fix Y;
        public readonly Fix Z;
        public FixVec3(Fix x, Fix y, Fix z) { X = x; Y = y; Z = z; }
        public static readonly FixVec3 Zero = new FixVec3(Fix.Zero, Fix.Zero, Fix.Zero);
        public static readonly FixVec3 One = new FixVec3(Fix.One, Fix.One, Fix.One);
        public static readonly FixVec3 Up = new FixVec3(Fix.Zero, Fix.One, Fix.Zero);
        public static readonly FixVec3 Right = new FixVec3(Fix.One, Fix.Zero, Fix.Zero);
        public static readonly FixVec3 Forward = new FixVec3(Fix.Zero, Fix.Zero, Fix.One);
        public static FixVec3 operator +(FixVec3 a, FixVec3 b) => new FixVec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static FixVec3 operator -(FixVec3 a, FixVec3 b) => new FixVec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static FixVec3 operator -(FixVec3 value) => new FixVec3(-value.X, -value.Y, -value.Z);
        public static FixVec3 operator *(FixVec3 value, Fix scale) => new FixVec3(value.X * scale, value.Y * scale, value.Z * scale);
        public static FixVec3 operator *(Fix scale, FixVec3 value) => value * scale;
        public static FixVec3 operator /(FixVec3 value, Fix scale) => new FixVec3(value.X / scale, value.Y / scale, value.Z / scale);
        public static bool operator ==(FixVec3 a, FixVec3 b) => a.Equals(b);
        public static bool operator !=(FixVec3 a, FixVec3 b) => !a.Equals(b);
        public static Fix Dot(FixVec3 a, FixVec3 b)
        {
            long product = checked((long)a.X.Raw * b.X.Raw + (long)a.Y.Raw * b.Y.Raw + (long)a.Z.Raw * b.Z.Raw);
            return Fix.FromRaw(Fix.RoundProduct(product));
        }
        public static FixVec3 Cross(FixVec3 a, FixVec3 b) => new FixVec3(
            Fix.FromRaw(Fix.RoundProduct(checked((long)a.Y.Raw * b.Z.Raw - (long)a.Z.Raw * b.Y.Raw))),
            Fix.FromRaw(Fix.RoundProduct(checked((long)a.Z.Raw * b.X.Raw - (long)a.X.Raw * b.Z.Raw))),
            Fix.FromRaw(Fix.RoundProduct(checked((long)a.X.Raw * b.Y.Raw - (long)a.Y.Raw * b.X.Raw))));
        public Fix SqrMagnitude => Dot(this, this);
        public Fix Magnitude => Fix.FromRaw(checked((int)FixMath.IntegerSqrt(SquaredRaw())));
        private ulong SquaredRaw() => (ulong)((long)X.Raw * X.Raw) + (ulong)((long)Y.Raw * Y.Raw) + (ulong)((long)Z.Raw * Z.Raw);
        public FixVec3 Normalized()
        {
            // 长度可能超出 Fix 表示范围，但归一化后的分量仍在 [-1,1]。
            long magnitude = (long)FixMath.IntegerSqrt(SquaredRaw());
            if (magnitude == 0) return Zero;
            return new FixVec3(Fix.FromRaw((int)(X.Raw * Fix.Scale / magnitude)),
                Fix.FromRaw((int)(Y.Raw * Fix.Scale / magnitude)), Fix.FromRaw((int)(Z.Raw * Fix.Scale / magnitude)));
        }
        public static FixVec3 Normalize(FixVec3 value) => value.Normalized();
        public static FixVec3 MoveTowards(FixVec3 current, FixVec3 target, Fix maxDelta)
        {
            if (maxDelta.Raw < 0) throw new ArgumentOutOfRangeException(nameof(maxDelta));
            FixVec3 delta = target - current;
            ulong magnitude = FixMath.IntegerSqrt(delta.SquaredRaw());
            if (magnitude <= (ulong)maxDelta.Raw) return target;
            return current + ScaleToLength(delta, maxDelta, magnitude);
        }
        private static FixVec3 ScaleToLength(FixVec3 value, Fix length, ulong magnitude) => new FixVec3(
            Fix.FromRaw((int)((long)value.X.Raw * length.Raw / (long)magnitude)),
            Fix.FromRaw((int)((long)value.Y.Raw * length.Raw / (long)magnitude)),
            Fix.FromRaw((int)((long)value.Z.Raw * length.Raw / (long)magnitude)));
        public static FixVec3 ClampMagnitude(FixVec3 value, Fix maxLength)
        {
            if (maxLength.Raw < 0) throw new ArgumentOutOfRangeException(nameof(maxLength));
            ulong magnitude = FixMath.IntegerSqrt(value.SquaredRaw());
            return magnitude > (ulong)maxLength.Raw ? ScaleToLength(value, maxLength, magnitude) : value;
        }
        /// <summary>支持非单位法线；零法线不改变向量。</summary>
        public static FixVec3 ProjectOnPlane(FixVec3 value, FixVec3 normal)
        {
            Fix squared = Dot(normal, normal);
            return squared.Raw == 0 ? value : value - normal * (Dot(value, normal) / squared);
        }
        /// <summary>normal 必须为单位法线，与碰撞查询返回值约定一致。</summary>
        public static FixVec3 Reflect(FixVec3 value, FixVec3 normal) => value - normal * (Fix.FromInt(2) * Dot(value, normal));
        public bool Equals(FixVec3 other) => X == other.X && Y == other.Y && Z == other.Z;
        public override bool Equals(object obj) => obj is FixVec3 other && Equals(other);
        public override int GetHashCode() => unchecked((X.Raw * 397) ^ (Y.Raw * 911) ^ Z.Raw);
        public override string ToString() => $"({X}, {Y}, {Z})";
    }
}

using System;
using AFPS.Math;
using BigInteger = System.Numerics.BigInteger;

namespace AFPS.Simulation.World
{
    internal readonly struct WideVector
    {
        public readonly BigInteger X,
            Y,
            Z;
        public WideVector(BigInteger x, BigInteger y, BigInteger z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public WideVector(FixVec3 v) : this(v.X.Raw, v.Y.Raw, v.Z.Raw)
        {
        }

        public bool IsZero => X.IsZero && Y.IsZero && Z.IsZero;

        public static WideVector operator +(WideVector a, WideVector b) => new WideVector(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static WideVector operator -(WideVector a, WideVector b) => new WideVector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static WideVector operator -(WideVector a) => new WideVector(-a.X, -a.Y, -a.Z);
        public static WideVector operator *(WideVector a, BigInteger b) => new WideVector(a.X * b, a.Y * b, a.Z * b);
        public static BigInteger Dot(WideVector a, WideVector b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        public static WideVector Cross(WideVector a, WideVector b) => new WideVector(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        public WideVector Primitive() => Primitive(out _);
        public WideVector Primitive(out BigInteger scale)
        {
            scale = BigInteger.GreatestCommonDivisor(BigInteger.Abs(X), BigInteger.GreatestCommonDivisor(BigInteger.Abs(Y), BigInteger.Abs(Z)));
            if (scale.IsZero)
            {
                scale = BigInteger.One;
                return this;
            }

            return new WideVector(X / scale, Y / scale, Z / scale);
        }

        public FixVec3 Normalized()
        {
            BigInteger maximum = BigInteger.Max(BigInteger.Abs(X), BigInteger.Max(BigInteger.Abs(Y), BigInteger.Abs(Z)));
            if (maximum.IsZero)
            {
                return FixVec3.Zero;
            }

            return new FixVec3(Fix.FromRaw((int)(X * Fix.Scale / maximum)), Fix.FromRaw((int)(Y * Fix.Scale / maximum)), Fix.FromRaw((int)(Z * Fix.Scale / maximum))).Normalized();
        }

        public FixVec3 ToPoint(BigInteger denominator) =>
            new FixVec3(Fix.FromRaw(checked((int)(X / denominator))), Fix.FromRaw(checked((int)(Y / denominator))), Fix.FromRaw(checked((int)(Z / denominator))));
    }

    /// <summary>不可变、双面三角形。保留世界顶点，不使用包围盒代替表面。</summary>
    public readonly struct FixedWorldTriangle
    {
        public readonly uint ShapeId;
        public readonly int Layer;
        public readonly FixedSurface Surface;
        public readonly FixVec3 A,
            B,
            C;
        public FixedWorldTriangle(uint shapeId, int layer, FixedSurface surface, FixVec3 a, FixVec3 b, FixVec3 c)
        {
            FixedWorldBox.ValidateIdentity(shapeId, layer, surface);
            if (WideVector.Cross(new WideVector(b) - new WideVector(a), new WideVector(c) - new WideVector(a)).IsZero)
            {
                throw new ArgumentException("Triangle vertices must not be collinear.");
            }

            ShapeId = shapeId;
            Layer = layer;
            Surface = surface;
            A = a;
            B = b;
            C = c;
        }
    }
}

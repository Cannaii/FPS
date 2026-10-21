using System;
using AFPS.Math;

namespace AFPS.Simulation.World
{
    [Flags]
    public enum FixedSurface : uint
    {
        None = 0,
        Solid = 1,
        Ground = 2,
        Wall = 4,
        Ladder = 8,
        Grapple = 16,
        All = Solid | Ground | Wall | Ladder | Grapple
    }

    public enum FixedAxis : byte { X, Y, Z }

    /// <summary>以世界坐标 Raw 保存的非退化 AABB；不引用 Transform，也不在查询中烘焙。</summary>
    public readonly struct FixedAabb
    {
        public readonly FixVec3 Min;
        public readonly FixVec3 Max;

        public FixedAabb(FixVec3 min, FixVec3 max)
        {
            if (min.X >= max.X || min.Y >= max.Y || min.Z >= max.Z) throw new ArgumentException("AABB must have positive extent on every axis.");
            Min = min;
            Max = max;
        }

        internal bool IsValid => Min.X < Max.X && Min.Y < Max.Y && Min.Z < Max.Z;
    }

    public readonly struct FixedWorldBox
    {
        public readonly uint ShapeId;
        public readonly int Layer;
        public readonly FixedSurface Surface;
        public readonly FixedAabb Bounds;

        public FixedWorldBox(uint shapeId, int layer, FixedSurface surface, FixedAabb bounds)
        {
            ValidateIdentity(shapeId, layer, surface);
            if (!bounds.IsValid) throw new ArgumentException("Invalid AABB.", nameof(bounds));
            ShapeId = shapeId;
            Layer = layer;
            Surface = surface;
            Bounds = bounds;
        }

        internal static void ValidateIdentity(uint id, int layer, FixedSurface surface)
        {
            if (id == 0) throw new ArgumentOutOfRangeException(nameof(id));
            if (layer < 0 || layer > 31) throw new ArgumentOutOfRangeException(nameof(layer));
            if (surface == FixedSurface.None || (surface & ~FixedSurface.All) != 0) throw new ArgumentOutOfRangeException(nameof(surface));
        }
    }

    /// <summary>轴对齐无限平面。单面仅从法线侧命中；地面使用 Y 轴、正法线。</summary>
    public readonly struct FixedWorldPlane
    {
        public readonly uint ShapeId;
        public readonly int Layer;
        public readonly FixedSurface Surface;
        public readonly FixedAxis Axis;
        public readonly Fix Coordinate;
        public readonly bool PositiveNormal;
        public readonly bool DoubleSided;

        public FixedWorldPlane(uint shapeId, int layer, FixedSurface surface, FixedAxis axis, Fix coordinate, bool positiveNormal = true, bool doubleSided = false)
        {
            FixedWorldBox.ValidateIdentity(shapeId, layer, surface);
            if (axis > FixedAxis.Z) throw new ArgumentOutOfRangeException(nameof(axis));
            ShapeId = shapeId;
            Layer = layer;
            Surface = surface;
            Axis = axis;
            Coordinate = coordinate;
            PositiveNormal = positiveNormal;
            DoubleSided = doubleSided;
        }
    }
}

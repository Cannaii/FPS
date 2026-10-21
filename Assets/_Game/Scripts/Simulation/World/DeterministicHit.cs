using AFPS.Math;

namespace AFPS.Simulation.World
{
    /// <summary>一个定点几何查询的最近命中；default 表示未命中。</summary>
    public readonly struct DeterministicHit
    {
        public readonly bool DidHit;
        public readonly uint ShapeId;
        public readonly int Layer;
        public readonly FixedSurface Surface;
        // 距离向下取整到 Raw；最多提前一个 Raw。最近命中比较仍使用未取整的有理数。
        public readonly Fix Distance;
        public readonly FixVec3 Point;
        public readonly FixVec3 Normal;
        // 射线从箱体严格内部出发时返回距离 0、零法线，由调用者选择穿透/脱离策略。
        public readonly bool StartedInside;

        internal DeterministicHit(uint shapeId, int layer, FixedSurface surface, Fix distance, FixVec3 point, FixVec3 normal, bool startedInside = false)
        {
            DidHit = true;
            ShapeId = shapeId;
            Layer = layer;
            Surface = surface;
            Distance = distance;
            Point = point;
            Normal = normal;
            StartedInside = startedInside;
        }
    }
}

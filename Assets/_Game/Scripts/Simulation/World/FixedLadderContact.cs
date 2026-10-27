using AFPS.Math;

namespace AFPS.Simulation.World
{
    /// <summary>竖直轴对齐梯子体积；脚底/高度和边界均使用世界米Q16.16，零ID表示未命中。</summary>
    public readonly struct FixedLadderContact
    {
        public readonly uint ShapeId;
        public readonly FixedAabb Bounds;
        public readonly FixVec3 Normal; // 水平单位向外法线；较薄XZ轴为梯面轴，同宽时选X。
        public bool HasLadder => ShapeId != 0;

        internal FixedLadderContact(uint shapeId, FixedAabb bounds, FixVec3 normal)
        {
            ShapeId = shapeId;
            Bounds = bounds;
            Normal = normal;
        }
    }
}

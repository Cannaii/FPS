using AFPS.Math;

namespace AFPS.Simulation.World
{
    /// <summary>只读取不可变定点世界；查询不访问场景、Physics 或渲染状态。</summary>
    public interface IWorldQuery
    {
        ulong VersionHash { get; }

        /// <summary>方向无需预归一化；距离单位为米。相同距离按 ShapeId、X/Y/Z 面顺序裁决。</summary>
        DeterministicHit Raycast(in FixVec3 origin, in FixVec3 direction, Fix maxDistance, int layerMask = ~0, FixedSurface surfaceMask = FixedSurface.All);

        /// <summary>从胶囊脚底向下找圆形足迹内的支撑面；这是地面探测，不是胶囊扫掠求解。</summary>
        DeterministicHit GroundCheck(in FixVec3 center, Fix radius, Fix halfHeight, Fix probeDistance, int layerMask = ~0);
    }
}

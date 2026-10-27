using System;
using AFPS.Math;

namespace AFPS.Simulation.World
{
    public sealed partial class FixedWorld
    {
        /// <summary>查询胶囊足迹附近的竖直梯盒；水平含边界、竖直需有正重叠，脚底到达顶面即退出。</summary>
        public FixedLadderContact LadderCheck(in FixVec3 feet, Fix radius, Fix height, Fix reach,
            uint preferredShapeId = 0, int layerMask = ~0)
        {
            if (radius <= Fix.Zero || (long)height.Raw < 2L * radius.Raw || reach < Fix.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(radius));
            }
            long range = (long)radius.Raw + reach.Raw;
            if (range > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(reach));
            }
            FixedLadderContact best = default;
            ulong bestDistance = ulong.MaxValue;
            foreach (var box in ladderBoxes)
            {
                if (!Matches(box.Layer, box.Surface, layerMask, FixedSurface.Ladder)
                    || feet.Y.Raw >= box.Bounds.Max.Y.Raw
                    || (long)feet.Y.Raw + height.Raw <= box.Bounds.Min.Y.Raw)
                {
                    continue;
                }
                long dx = MathDistance(feet.X.Raw, box.Bounds.Min.X.Raw, box.Bounds.Max.X.Raw);
                long dz = MathDistance(feet.Z.Raw, box.Bounds.Min.Z.Raw, box.Bounds.Max.Z.Raw);
                // 先按轴筛选再平方，世界两端相减不能先压回int或Fix。
                if (dx > range || dz > range)
                {
                    continue;
                }
                ulong distance = (ulong)(dx * dx) + (ulong)(dz * dz);
                if (distance > (ulong)(range * range))
                {
                    continue;
                }
                bool xFace = (long)box.Bounds.Max.X.Raw - box.Bounds.Min.X.Raw
                    <= (long)box.Bounds.Max.Z.Raw - box.Bounds.Min.Z.Raw;
                long center = xFace
                    ? ((long)box.Bounds.Min.X.Raw + box.Bounds.Max.X.Raw) / 2
                    : ((long)box.Bounds.Min.Z.Raw + box.Bounds.Max.Z.Raw) / 2;
                int component = xFace ? feet.X.Raw : feet.Z.Raw;
                var normal = (xFace ? FixVec3.Right : FixVec3.Forward) * (component >= center ? Fix.One : -Fix.One);
                var candidate = new FixedLadderContact(box.ShapeId, box.Bounds, normal);
                if (box.ShapeId == preferredShapeId)
                {
                    return candidate;
                }
                if (!best.HasLadder || distance < bestDistance || (distance == bestDistance && box.ShapeId < best.ShapeId))
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }
            return best;
        }

        private static long MathDistance(int value, int minimum, int maximum) =>
            value < minimum ? (long)minimum - value : value > maximum ? (long)value - maximum : 0L;
    }
}

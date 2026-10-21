using System;
using AFPS.Math;
using BigInteger = System.Numerics.BigInteger;

namespace AFPS.Simulation.World
{
    public sealed partial class FixedWorld
    {
        /// <summary>
        /// 竖直胶囊的连续扫掠。圆角使用解析球/边/面求交；只在最终返回时向下取整距离。
        /// 初始重叠仅阻止继续深入，允许沿外侧法线脱离；贴面向外/平移不自命中。
        /// BigInteger 用于宽几何和二次根比较，避免世界极值处的中间乘积溢出。
        /// </summary>
        public DeterministicHit CapsuleCast(
            in FixVec3 feet,
            Fix radius,
            Fix height,
            in FixVec3 direction,
            Fix maxDistance,
            int layerMask = ~0,
            FixedSurface surfaceMask = FixedSurface.Solid) => CapsuleCastCore(feet, radius, height, direction, maxDistance, layerMask, surfaceMask, true);
        // 运动求解器已经归一化方向，保留同一 Raw，防止二次归一化改变距离对应的射线。
        internal DeterministicHit CapsuleCastUnit(in FixVec3 feet, Fix radius, Fix height, in FixVec3 direction, Fix maxDistance, int layerMask) => CapsuleCastCore(feet, radius, height, direction, maxDistance, layerMask, FixedSurface.Solid, false);
        private DeterministicHit CapsuleCastCore(
            in FixVec3 feet,
            Fix radius,
            Fix height,
            in FixVec3 direction,
            Fix maxDistance,
            int layerMask,
            FixedSurface surfaceMask,
            bool normalize)
        {
            if (radius.Raw <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(radius));
            }

            if ((long)height.Raw < 2L * radius.Raw)
            {
                throw new ArgumentOutOfRangeException(nameof(height));
            }

            if (maxDistance.Raw < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxDistance));
            }

            if (direction == FixVec3.Zero || layerMask == 0 || surfaceMask == FixedSurface.None)
            {
                return default;
            }

            FixVec3 unit = normalize ? direction.Normalized() : direction;
            DeterministicHit best = default;
            CapsuleSweepTime bestTime = CapsuleSweepTime.Rational(maxDistance.Raw, 1);
            foreach (FixedWorldBox box in boxes)
            {
                if (!Matches(box.Layer, box.Surface, layerMask, surfaceMask))
                {
                    continue;
                }

                var sweep = new CapsuleBoxSweep(box.Bounds, feet, radius.Raw, height.Raw, unit);
                if (!sweep.Cast(maxDistance.Raw, out CapsuleSweepTime time, out bool inside))
                {
                    continue;
                }

                int order = time.CompareTo(bestTime);
                if (best.DidHit && (order > 0 || (order == 0 && box.ShapeId >= best.ShapeId)))
                {
                    continue;
                }

                int distance = time.FloorRaw;
                bestTime = time;
                best = new DeterministicHit(box.ShapeId, box.Layer, box.Surface, Fix.FromRaw(distance), sweep.ContactPoint(distance), sweep.Normal(distance), inside);
            }

            foreach (FixedWorldPlane plane in planes)
            {
                if (!Matches(plane.Layer, plane.Surface, layerMask, surfaceMask))
                {
                    continue;
                }

                int axis = (int)plane.Axis,
                    component = Component(unit, axis);
                long center = Component(feet, axis) + (axis == 1 ? height.Raw / 2L : 0L);
                bool positive = plane.DoubleSided ? center >= plane.Coordinate.Raw : plane.PositiveNormal;
                int sign = positive ? 1 : -1;
                if ((long)component * sign >= 0)
                {
                    continue;
                }

                long support = Component(feet, axis) + (axis == 1 ? (positive ? 0L : height.Raw) : -sign * (long)radius.Raw);
                long gap = sign * (support - plane.Coordinate.Raw);
                CapsuleSweepTime time = CapsuleSweepTime.Rational(System.Math.Max(0, gap) * Fix.Scale, -component * sign);
                if (time.CompareToFraction(maxDistance.Raw, 1) > 0)
                {
                    continue;
                }

                int order = time.CompareTo(bestTime);
                if (best.DidHit && (order > 0 || (order == 0 && plane.ShapeId >= best.ShapeId)))
                {
                    continue;
                }

                int distance = time.FloorRaw;
                long x = feet.X.Raw + (long)unit.X.Raw * distance / Fix.Scale;
                long y = feet.Y.Raw + height.Raw / 2L + (long)unit.Y.Raw * distance / Fix.Scale;
                long z = feet.Z.Raw + (long)unit.Z.Raw * distance / Fix.Scale;
                if (axis == 0)
                {
                    x = plane.Coordinate.Raw;
                }
                else if (axis == 1)
                {
                    y = plane.Coordinate.Raw;
                }
                else
                {
                    z = plane.Coordinate.Raw;
                }

                bestTime = time;
                best = new DeterministicHit(plane.ShapeId, plane.Layer, plane.Surface, Fix.FromRaw(distance), RawPoint(x, y, z), AxisNormal(axis, positive), gap < 0);
            }

            SweepTriangles(feet, radius, height, unit, maxDistance, layerMask, surfaceMask, ref bestTime, ref best);
            return best;
        }

        private static FixVec3 RawPoint(long x, long y, long z) => new FixVec3(RawCoordinate(x), RawCoordinate(y), RawCoordinate(z));
        private static Fix RawCoordinate(long value) => Fix.FromRaw((int)System.Math.Max(int.MinValue, System.Math.Min(int.MaxValue, value)));
        // 竖直胶囊 = 球 + 竖直线段；等价于球对 Y 方向扩展后的盒体扫掠。
        private readonly struct CapsuleBoxSweep
        {
            private readonly FixedAabb bounds;
            private readonly FixVec3 feet;
            private readonly FixVec3 direction;
            private readonly int radius;
            private readonly int segment;
            public CapsuleBoxSweep(FixedAabb bounds, FixVec3 feet, int radius, int height, FixVec3 direction)
            {
                this.bounds = bounds;
                this.feet = feet;
                this.radius = radius;
                segment = height - 2 * radius;
                this.direction = direction;
            }

            private long Start(int axis) => Component(feet, axis) + (axis == 1 ? (long)radius : 0);
            private long Low(int axis) => Component(bounds.Min, axis) - (axis == 1 ? (long)segment : 0);
            private long High(int axis) => Component(bounds.Max, axis);
            private long AtScaled(int axis, int distance) => Start(axis) * Fix.Scale + (long)Component(direction, axis) * distance;
            private long DeltaScaled(int axis, int distance)
            {
                long coordinate = AtScaled(axis, distance);
                long nearest = System.Math.Max(Low(axis) * Fix.Scale, System.Math.Min(High(axis) * Fix.Scale, coordinate));
                return coordinate - nearest;
            }

            public bool Cast(int maximum, out CapsuleSweepTime time, out bool inside)
            {
                time = default;
                inside = false;
                if (!BroadPhase(maximum))
                {
                    return false;
                }

                long x = DeltaScaled(0, 0),
                    y = DeltaScaled(1, 0),
                    z = DeltaScaled(2, 0);
                BigInteger squared = (BigInteger)x * x + (BigInteger)y * y + (BigInteger)z * z;
                BigInteger r = (BigInteger)radius * Fix.Scale;
                int overlap = squared.CompareTo(r * r);
                if (overlap <= 0)
                {
                    FixVec3 normal = Normal(0);
                    // 使用宽整数而非已舍入的 Fix.Dot，精确区分贴面平移与继续深入。
                    BigInteger approach = squared.IsZero ? (BigInteger)direction.X.Raw * normal.X.Raw + (BigInteger)direction.Y.Raw * normal.Y.Raw + (BigInteger)direction.Z.Raw * normal.Z.Raw : (BigInteger)direction.X.Raw * x + (BigInteger)direction.Y.Raw * y + (BigInteger)direction.Z.Raw * z;
                    if (approach >= 0)
                    {
                        return false;
                    }

                    time = CapsuleSweepTime.Rational(0, 1);
                    inside = overlap < 0;
                    return true;
                }

                bool found = false;
                // 6 个面、12 条边和 8 个角；每个区域各自构造一个二次方程。
                for (int sx = -1; sx <= 1; sx++)
                {
                    for (int sy = -1; sy <= 1; sy++)
                    {
                        for (int sz = -1; sz <= 1; sz++)
                        {
                            if (sx == 0 && sy == 0 && sz == 0)
                            {
                                continue;
                            }

                            BigInteger a = 0,
                                b = 0,
                                c = -r * r;
                            AddAxis(0, sx, ref a, ref b, ref c);
                            AddAxis(1, sy, ref a, ref b, ref c);
                            AddAxis(2, sz, ref a, ref b, ref c);
                            if (!CapsuleSweepTime.TryQuadratic(a, b, c, maximum, out CapsuleSweepTime candidate))
                            {
                                continue;
                            }

                            if (!InRegion(candidate, 0, sx) || !InRegion(candidate, 1, sy) || !InRegion(candidate, 2, sz))
                            {
                                continue;
                            }

                            if (found && candidate.CompareTo(time) >= 0)
                            {
                                continue;
                            }

                            found = true;
                            time = candidate;
                        }
                    }
                }

                return found;
            }

            private void AddAxis(int axis, int side, ref BigInteger a, ref BigInteger b, ref BigInteger c)
            {
                if (side == 0)
                {
                    return;
                }

                BigInteger offset = (BigInteger)(Start(axis) - (side < 0 ? Low(axis) : High(axis))) * Fix.Scale;
                int velocity = Component(direction, axis);
                a += (BigInteger)velocity * velocity;
                b += 2 * offset * velocity;
                c += offset * offset;
            }

            private bool InRegion(CapsuleSweepTime time, int axis, int side)
            {
                int low = CompareCoordinate(time, axis, Low(axis));
                int high = CompareCoordinate(time, axis, High(axis));
                return side < 0 ? low <= 0 : side > 0 ? high >= 0 : low >= 0 && high <= 0;
            }

            private int CompareCoordinate(CapsuleSweepTime time, int axis, long boundary)
            {
                int velocity = Component(direction, axis);
                if (velocity == 0)
                {
                    return Start(axis).CompareTo(boundary);
                }

                int order = time.CompareToFraction((boundary - Start(axis)) * Fix.Scale, velocity);
                return velocity < 0 ? -order : order;
            }

            private bool BroadPhase(int maximum)
            {
                RayDistance enter = RayDistance.Zero,
                    exit = new RayDistance(maximum, 1);
                for (int axis = 0; axis < 3; axis++)
                {
                    long low = Low(axis) - radius,
                        high = High(axis) + radius,
                        start = Start(axis);
                    int velocity = Component(direction, axis);
                    if (velocity == 0)
                    {
                        if (start < low || start > high)
                        {
                            return false;
                        }

                        continue;
                    }

                    long near = velocity > 0 ? low : high,
                        far = velocity > 0 ? high : low;
                    var entry = new RayDistance((near - start) * Fix.Scale, velocity);
                    var leave = new RayDistance((far - start) * Fix.Scale, velocity);
                    if (entry.CompareTo(enter) > 0)
                    {
                        enter = entry;
                    }

                    if (leave.CompareTo(exit) < 0)
                    {
                        exit = leave;
                    }

                    if (enter.CompareTo(exit) > 0)
                    {
                        return false;
                    }
                }

                return true;
            }

            public FixVec3 Normal(int distance)
            {
                long x = DeltaScaled(0, distance),
                    y = DeltaScaled(1, distance),
                    z = DeltaScaled(2, distance);
                long maximum = System.Math.Max(System.Math.Abs(x), System.Math.Max(System.Math.Abs(y), System.Math.Abs(z)));
                if (maximum != 0)
                {
                    return new FixVec3(Fix.FromRaw((int)(x * Fix.Scale / maximum)), Fix.FromRaw((int)(y * Fix.Scale / maximum)), Fix.FromRaw((int)(z * Fix.Scale / maximum))).Normalized();
                }

                // 中心已在扩展盒内部时选择最短脱离轴；相等时固定 X/Y/Z、负/正顺序。
                long best = long.MaxValue;
                FixVec3 normal = FixVec3.Zero;
                for (int axis = 0; axis < 3; axis++)
                {
                    long low = AtScaled(axis, distance) - Low(axis) * Fix.Scale,
                        high = High(axis) * Fix.Scale - AtScaled(axis, distance);
                    if (low < best)
                    {
                        best = low;
                        normal = AxisNormal(axis, false);
                    }

                    if (high < best)
                    {
                        best = high;
                        normal = AxisNormal(axis, true);
                    }
                }

                return normal;
            }

            public FixVec3 ContactPoint(int distance)
            {
                long x = AtScaled(0, distance) / Fix.Scale;
                long y = AtScaled(1, distance) / Fix.Scale;
                long z = AtScaled(2, distance) / Fix.Scale;
                return RawPoint(
                    System.Math.Max(bounds.Min.X.Raw, System.Math.Min(bounds.Max.X.Raw, x)),
                    System.Math.Max(bounds.Min.Y.Raw, System.Math.Min(bounds.Max.Y.Raw, y)),
                    System.Math.Max(bounds.Min.Z.Raw, System.Math.Min(bounds.Max.Z.Raw, z)));
            }
        }
    }
}

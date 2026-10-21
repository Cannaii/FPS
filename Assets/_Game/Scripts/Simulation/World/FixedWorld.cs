using System;
using System.Collections.Generic;
using AFPS.Math;

namespace AFPS.Simulation.World
{
    /// <summary>不可变的平面/AABB/三角形世界；稳定 ID、规范快照与整数查询，不访问引擎状态。</summary>
    public sealed partial class FixedWorld : IWorldQuery
    {
        private readonly FixedWorldBox[] boxes;
        private readonly FixedWorldPlane[] planes;
        private readonly FixedWorldTriangle[] triangles;
        private readonly TriangleTree triangleTree;
        private readonly byte[] snapshot;
        public ulong VersionHash { get; }
        public int BoxCount => boxes.Length;
        public int PlaneCount => planes.Length;
        public int TriangleCount => triangles.Length;

        public FixedWorldTriangle GetTriangle(int index) => triangles[index];
        public FixedWorldBox GetBox(int index) => boxes[index];
        public FixedWorldPlane GetPlane(int index) => planes[index];
        public FixedWorld(FixedWorldBox[] boxes, FixedWorldPlane[] planes = null, FixedWorldTriangle[] triangles = null)
        {
            int boxCount = boxes?.Length ?? 0,
                planeCount = planes?.Length ?? 0;
            if ((long)boxCount + planeCount > FixedWorldCodec.MaxShapeCount)
            {
                throw new ArgumentException("World shape limit exceeded.");
            }

            this.boxes = boxes == null ? Array.Empty<FixedWorldBox>() : (FixedWorldBox[])boxes.Clone();
            this.planes = planes == null ? Array.Empty<FixedWorldPlane>() : (FixedWorldPlane[])planes.Clone();
            if ((triangles?.Length ?? 0) > FixedWorldCodec.MaxTriangleCount)
            {
                throw new ArgumentException("World triangle limit exceeded.");
            }

            this.triangles = triangles == null ? Array.Empty<FixedWorldTriangle>() : (FixedWorldTriangle[])triangles.Clone();
            var ids = new HashSet<uint>();
            foreach (FixedWorldBox box in this.boxes)
            {
                FixedWorldBox.ValidateIdentity(box.ShapeId, box.Layer, box.Surface);
                if (!box.Bounds.IsValid || !ids.Add(box.ShapeId))
                {
                    throw new ArgumentException("Invalid bounds or duplicate shape ID.");
                }
            }

            foreach (FixedWorldPlane plane in this.planes)
            {
                FixedWorldBox.ValidateIdentity(plane.ShapeId, plane.Layer, plane.Surface);
                if (plane.Axis > FixedAxis.Z || !ids.Add(plane.ShapeId))
                {
                    throw new ArgumentException("Invalid axis or duplicate shape ID.");
                }
            }

            foreach (FixedWorldTriangle triangle in this.triangles)
            {
                _ = new FixedWorldTriangle(triangle.ShapeId, triangle.Layer, triangle.Surface, triangle.A, triangle.B, triangle.C);
                if (!ids.Add(triangle.ShapeId))
                {
                    throw new ArgumentException("Duplicate shape ID.");
                }
            }

            // 快照顺序由稳定 ID 唯一确定，构造输入顺序不应影响版本哈希或重放结果。
            Array.Sort(this.triangles, (a, b) => a.ShapeId.CompareTo(b.ShapeId));
            triangleTree = new TriangleTree(this.triangles);
            Array.Sort(this.boxes, (a, b) => a.ShapeId.CompareTo(b.ShapeId));
            Array.Sort(this.planes, (a, b) => a.ShapeId.CompareTo(b.ShapeId));
            snapshot = FixedWorldCodec.WriteCanonical(this);
            VersionHash = FixedWorldCodec.ComputeHash(snapshot);
        }

        /// <summary>返回快照副本，调用方的修改不会污染当前世界或版本哈希。</summary>
        public byte[] CreateSnapshot() => (byte[])snapshot.Clone();
        public DeterministicHit Raycast(
            in FixVec3 origin,
            in FixVec3 direction,
            Fix maxDistance,
            int layerMask = ~0,
            FixedSurface surfaceMask = FixedSurface.All)
        {
            if (maxDistance.Raw < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxDistance));
            }

            if (layerMask == 0 || surfaceMask == FixedSurface.None || direction == FixVec3.Zero)
            {
                return default;
            }

            FixVec3 unit = direction.Normalized();
            DeterministicHit best = default;
            RayDistance bestDistance = new RayDistance(maxDistance.Raw, 1);
            foreach (FixedWorldBox box in boxes)
            {
                if (!Matches(box.Layer, box.Surface, layerMask, surfaceMask)
                    || !CastBox(box.Bounds, origin, unit, maxDistance, out RayDistance distance, out FixVec3 normal, out bool inside))
                {
                    continue;
                }

                Consider(box.ShapeId, box.Layer, box.Surface, origin, unit, distance, normal, inside, ref bestDistance, ref best);
            }

            foreach (FixedWorldPlane plane in planes)
            {
                if (!Matches(plane.Layer, plane.Surface, layerMask, surfaceMask))
                {
                    continue;
                }

                int axis = (int)plane.Axis,
                    component = Component(unit, axis);
                if (component == 0 || (!plane.DoubleSided && ((component > 0) == plane.PositiveNormal)))
                {
                    continue;
                }

                var distance = new RayDistance(((long)plane.Coordinate.Raw - Component(origin, axis)) * Fix.Scale, component);
                if (distance.CompareTo(RayDistance.Zero) < 0 || distance.CompareTo(new RayDistance(maxDistance.Raw, 1)) > 0)
                {
                    continue;
                }

                FixVec3 normal = AxisNormal(axis, plane.DoubleSided ? component < 0 : plane.PositiveNormal);
                Consider(plane.ShapeId, plane.Layer, plane.Surface, origin, unit, distance, normal, false, ref bestDistance, ref best);
            }

            RaycastTriangles(origin, unit, maxDistance, layerMask, surfaceMask, bestDistance, ref best);
            return best;
        }

        public DeterministicHit GroundCheck(in FixVec3 center, Fix radius, Fix halfHeight, Fix probeDistance, int layerMask = ~0)
        {
            if (radius.Raw < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(radius));
            }

            if (halfHeight < radius)
            {
                throw new ArgumentOutOfRangeException(nameof(halfHeight));
            }

            if (probeDistance.Raw < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(probeDistance));
            }

            long feetY = (long)center.Y.Raw - halfHeight.Raw;
            DeterministicHit best = default;
            foreach (FixedWorldBox box in boxes)
            {
                if (!Matches(box.Layer, box.Surface, layerMask, FixedSurface.Ground))
                {
                    continue;
                }

                int x = ClampRaw(center.X.Raw, box.Bounds.Min.X.Raw, box.Bounds.Max.X.Raw);
                int z = ClampRaw(center.Z.Raw, box.Bounds.Min.Z.Raw, box.Bounds.Max.Z.Raw);
                long dx = (long)center.X.Raw - x,
                    dz = (long)center.Z.Raw - z;
                // 先限制单轴差，避免世界两端的差值平方超出 long。
                if (dx < -radius.Raw || dx > radius.Raw || dz < -radius.Raw || dz > radius.Raw)
                {
                    continue;
                }

                if ((ulong)(dx * dx) + (ulong)(dz * dz) > (ulong)((long)radius.Raw * radius.Raw))
                {
                    continue;
                }

                ConsiderGround(box.ShapeId, box.Layer, box.Surface, x, box.Bounds.Max.Y.Raw, z, feetY, probeDistance, ref best);
            }

            foreach (FixedWorldPlane plane in planes)
            {
                if (plane.Axis != FixedAxis.Y || (!plane.PositiveNormal && !plane.DoubleSided) || !Matches(plane.Layer, plane.Surface, layerMask, FixedSurface.Ground))
                {
                    continue;
                }

                ConsiderGround(plane.ShapeId, plane.Layer, plane.Surface, center.X.Raw, plane.Coordinate.Raw, center.Z.Raw, feetY, probeDistance, ref best);
            }

            if (triangles.Length > 0)
            {
                if (feetY < int.MinValue || feetY > int.MaxValue)
                {
                    throw new ArgumentOutOfRangeException(nameof(center));
                }

                if (radius.Raw > int.MaxValue / 2)
                {
                    throw new ArgumentOutOfRangeException(nameof(radius));
                }

                FixVec3 feet = new FixVec3(center.X, Fix.FromRaw((int)feetY), center.Z);
                if (radius.Raw == 0)
                {
                    RayDistance time = new RayDistance(best.DidHit ? best.Distance.Raw : probeDistance.Raw, 1);
                    RaycastTriangles(feet, -FixVec3.Up, probeDistance, layerMask, FixedSurface.Ground, time, ref best);
                }
                else
                {
                    CapsuleSweepTime time = CapsuleSweepTime.Rational(best.DidHit ? best.Distance.Raw : probeDistance.Raw, 1);
                    SweepTriangles(feet, radius, Fix.FromRaw(radius.Raw * 2), -FixVec3.Up, probeDistance, layerMask, FixedSurface.Ground, ref time, ref best);
                }
            }

            return best;
        }

        private static void ConsiderGround(
            uint id,
            int layer,
            FixedSurface surface,
            int x,
            int y,
            int z,
            long feetY,
            Fix probeDistance,
            ref DeterministicHit best)
        {
            long distance = feetY - y;
            if (distance < 0
                || distance > probeDistance.Raw
                || (best.DidHit
                && (distance > best.Distance.Raw
                || (distance == best.Distance.Raw
                && id >= best.ShapeId))))
            {
                return;
            }

            best = new DeterministicHit(id, layer, surface, Fix.FromRaw((int)distance), new FixVec3(Fix.FromRaw(x), Fix.FromRaw(y), Fix.FromRaw(z)), FixVec3.Up);
        }

        private static bool Matches(int layer, FixedSurface surface, int layerMask, FixedSurface surfaceMask) => (layerMask & (1 << layer)) != 0 && (surface & surfaceMask) != 0;
        private static int ClampRaw(int value, int min, int max) => value < min ? min : value > max ? max : value;
        internal static int Component(FixVec3 value, int axis) => axis == 0 ? value.X.Raw : axis == 1 ? value.Y.Raw : value.Z.Raw;
        private static FixVec3 AxisNormal(int axis, bool positive)
        {
            Fix sign = positive ? Fix.One : -Fix.One;
            return new FixVec3(axis == 0 ? sign : Fix.Zero, axis == 1 ? sign : Fix.Zero, axis == 2 ? sign : Fix.Zero);
        }

        private static bool CastBox(
            in FixedAabb bounds,
            in FixVec3 origin,
            in FixVec3 direction,
            Fix maximum,
            out RayDistance enter,
            out FixVec3 normal,
            out bool inside)
        {
            enter = new RayDistance(long.MinValue, 1);
            var exit = new RayDistance(maximum.Raw, 1);
            normal = FixVec3.Zero;
            inside = true;
            for (int axis = 0; axis < 3; axis++)
            {
                int low = Component(bounds.Min, axis),
                    high = Component(bounds.Max, axis);
                int start = Component(origin, axis),
                    directionRaw = Component(direction, axis);
                inside &= start > low && start < high;
                if (directionRaw == 0)
                {
                    if (start < low || start > high)
                    {
                        return false;
                    }

                    continue;
                }

                int near = directionRaw > 0 ? low : high,
                    far = directionRaw > 0 ? high : low;
                var nearDistance = new RayDistance(((long)near - start) * Fix.Scale, directionRaw);
                var farDistance = new RayDistance(((long)far - start) * Fix.Scale, directionRaw);
                if (nearDistance.CompareTo(enter) > 0)
                {
                    enter = nearDistance;
                    normal = AxisNormal(axis, directionRaw < 0);
                }

                if (farDistance.CompareTo(exit) < 0)
                {
                    exit = farDistance;
                }

                if (enter.CompareTo(exit) > 0)
                {
                    return false;
                }
            }

            if (exit.CompareTo(RayDistance.Zero) < 0)
            {
                return false;
            }

            if (enter.CompareTo(RayDistance.Zero) < 0)
            {
                // 在表面向外或贴面平移不产生自命中；严格内部起点显式报告穿透。
                if (!inside)
                {
                    return false;
                }

                enter = RayDistance.Zero;
                normal = FixVec3.Zero;
            }

            return true;
        }

        private static void Consider(
            uint id,
            int layer,
            FixedSurface surface,
            in FixVec3 origin,
            in FixVec3 direction,
            RayDistance distance,
            FixVec3 normal,
            bool inside,
            ref RayDistance bestDistance,
            ref DeterministicHit best)
        {
            int order = distance.CompareTo(bestDistance);
            if (best.DidHit && (order > 0 || (order == 0 && id >= best.ShapeId)))
            {
                return;
            }

            int rawDistance = checked((int)distance.Floor);
            if (!TryPoint(origin, direction, rawDistance, out FixVec3 point))
            {
                return;
            }

            bestDistance = distance;
            best = new DeterministicHit(id, layer, surface, Fix.FromRaw(rawDistance), point, normal, inside);
        }

        private static bool TryPoint(in FixVec3 origin, in FixVec3 direction, int rawDistance, out FixVec3 point)
        {
            long x = (long)origin.X.Raw + (long)direction.X.Raw * rawDistance / Fix.Scale;
            long y = (long)origin.Y.Raw + (long)direction.Y.Raw * rawDistance / Fix.Scale;
            long z = (long)origin.Z.Raw + (long)direction.Z.Raw * rawDistance / Fix.Scale;
            point = default;
            if (x < int.MinValue || x > int.MaxValue || y < int.MinValue || y > int.MaxValue || z < int.MinValue || z > int.MaxValue)
            {
                return false;
            }

            point = new FixVec3(Fix.FromRaw((int)x), Fix.FromRaw((int)y), Fix.FromRaw((int)z));
            return true;
        }
    }
}

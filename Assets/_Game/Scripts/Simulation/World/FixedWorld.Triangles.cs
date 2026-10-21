using System;
using System.Collections.Generic;
using AFPS.Math;
using BigInteger = System.Numerics.BigInteger;

namespace AFPS.Simulation.World
{
    public sealed partial class FixedWorld
    {
        // BVH 不进入快照：由规范三角形按整数质心/稳定 ID 重建，无可变查询缓存。
        private sealed class TriangleTree
        {
            internal readonly int[] Order;
            internal readonly Node[] Nodes;
            internal readonly struct Node
            {
                internal readonly Bounds Bounds;
                internal readonly int Left,
                    Right,
                    Start,
                    Count;
                internal Node(Bounds bounds, int left, int right, int start, int count)
                {
                    Bounds = bounds;
                    Left = left;
                    Right = right;
                    Start = start;
                    Count = count;
                }
            }

            internal TriangleTree(FixedWorldTriangle[] triangles)
            {
                Order = new int[triangles.Length];
                for (int i = 0; i < Order.Length; i++)
                {
                    Order[i] = i;
                }

                if (Order.Length == 0)
                {
                    Nodes = Array.Empty<Node>();
                    return;
                }

                var nodes = new List<Node>();
                Build(triangles, Order, nodes, 0, Order.Length);
                Nodes = nodes.ToArray();
            }

            private static int Build(FixedWorldTriangle[] triangles, int[] order, List<Node> nodes, int start, int count)
            {
                Bounds bounds = new Bounds(triangles[order[start]]);
                for (int i = 1; i < count; i++)
                {
                    bounds = Bounds.Union(bounds, new Bounds(triangles[order[start + i]]));
                }

                int index = nodes.Count;
                nodes.Add(default);
                // 每个叶节点最多八个三角形；只影响查询加速，不参与快照及命中裁决。
                if (count <= 8)
                {
                    nodes[index] = new Node(bounds, -1, -1, start, count);
                    return index;
                }

                int axis = bounds.LongestAxis;
                Array.Sort(
                    order,
                    start,
                    count,
                    Comparer<int>.Create((x, y) =>
                {
                    long a = Centroid(triangles[x], axis),
                        b = Centroid(triangles[y], axis);
                    // 相同质心仍用稳定 ShapeId 打破平局，避免依赖 Array.Sort 的稳定性。
                    int comparison = a.CompareTo(b);
                    return comparison != 0 ? comparison : triangles[x].ShapeId.CompareTo(triangles[y].ShapeId);
                }));
                int half = count / 2,
                    left = Build(triangles, order, nodes, start, half),
                    right = Build(triangles, order, nodes, start + half, count - half);
                nodes[index] = new Node(bounds, left, right, 0, 0);
                return index;
            }

            private static long Centroid(FixedWorldTriangle t, int axis) => (long)Component(t.A, axis) + Component(t.B, axis) + Component(t.C, axis);
        }

        private readonly struct Bounds
        {
            private readonly long x0,
                y0,
                z0,
                x1,
                y1,
                z1;
            internal Bounds(FixedWorldTriangle t) : this(
                System.Math.Min(t.A.X.Raw, System.Math.Min(t.B.X.Raw, t.C.X.Raw)),
                System.Math.Min(t.A.Y.Raw, System.Math.Min(t.B.Y.Raw, t.C.Y.Raw)),
                System.Math.Min(t.A.Z.Raw, System.Math.Min(t.B.Z.Raw, t.C.Z.Raw)),
                System.Math.Max(t.A.X.Raw, System.Math.Max(t.B.X.Raw, t.C.X.Raw)),
                System.Math.Max(t.A.Y.Raw, System.Math.Max(t.B.Y.Raw, t.C.Y.Raw)),
                System.Math.Max(t.A.Z.Raw, System.Math.Max(t.B.Z.Raw, t.C.Z.Raw)))
            {
            }

            private Bounds(long x0, long y0, long z0, long x1, long y1, long z1)
            {
                this.x0 = x0;
                this.y0 = y0;
                this.z0 = z0;
                this.x1 = x1;
                this.y1 = y1;
                this.z1 = z1;
            }

            internal static Bounds Union(Bounds a, Bounds b) =>
                new Bounds(
                    System.Math.Min(a.x0, b.x0),
                    System.Math.Min(a.y0, b.y0),
                    System.Math.Min(a.z0, b.z0),
                    System.Math.Max(a.x1, b.x1),
                    System.Math.Max(a.y1, b.y1),
                    System.Math.Max(a.z1, b.z1));
            internal int LongestAxis => x1 - x0 >= y1 - y0 && x1 - x0 >= z1 - z0 ? 0 : y1 - y0 >= z1 - z0 ? 1 : 2;

            internal bool Overlaps(Bounds b) => x0 <= b.x1 && x1 >= b.x0 && y0 <= b.y1 && y1 >= b.y0 && z0 <= b.z1 && z1 >= b.z0;
            // 半径、高度及包围盒坐标均为 Raw；向外扩一 Raw 覆盖分量截断，防止宽阶段漏检。
            internal static Bounds Sweep(FixVec3 feet, FixVec3 unit, Fix maximum, int radius, int height)
            {
                long x = feet.X.Raw + (long)unit.X.Raw * maximum.Raw / Fix.Scale,
                    y = feet.Y.Raw + (long)unit.Y.Raw * maximum.Raw / Fix.Scale,
                    z = feet.Z.Raw + (long)unit.Z.Raw * maximum.Raw / Fix.Scale;
                return new Bounds(
                    System.Math.Min(feet.X.Raw, x) - radius - 1,
                    System.Math.Min(feet.Y.Raw, y) - 1,
                    System.Math.Min(feet.Z.Raw, z) - radius - 1,
                    System.Math.Max(feet.X.Raw, x) + radius + 1,
                    System.Math.Max(feet.Y.Raw, y) + height + 1,
                    System.Math.Max(feet.Z.Raw, z) + radius + 1);
            }
        }

        private void SweepTriangles(
            FixVec3 feet,
            Fix radius,
            Fix height,
            FixVec3 unit,
            Fix maximum,
            int mask,
            FixedSurface surfaces,
            ref CapsuleSweepTime bestTime,
            ref DeterministicHit best)
        {
            if (triangleTree.Nodes.Length == 0)
            {
                return;
            }

            Bounds query = Bounds.Sweep(feet, unit, maximum, radius.Raw, height.Raw);
            SweepTriangleNode(0, query, feet, radius, height, unit, maximum, mask, surfaces, ref bestTime, ref best);
        }

        private void SweepTriangleNode(
            int index,
            Bounds query,
            FixVec3 feet,
            Fix radius,
            Fix height,
            FixVec3 unit,
            Fix maximum,
            int mask,
            FixedSurface surfaces,
            ref CapsuleSweepTime bestTime,
            ref DeterministicHit best)
        {
            TriangleTree.Node node = triangleTree.Nodes[index];
            if (!node.Bounds.Overlaps(query))
            {
                return;
            }

            if (node.Count == 0)
            {
                SweepTriangleNode(node.Left, query, feet, radius, height, unit, maximum, mask, surfaces, ref bestTime, ref best);
                SweepTriangleNode(node.Right, query, feet, radius, height, unit, maximum, mask, surfaces, ref bestTime, ref best);
                return;
            }

            for (int i = 0; i < node.Count; i++)
            {
                FixedWorldTriangle triangle = triangles[triangleTree.Order[node.Start + i]];
                if (!Matches(triangle.Layer, triangle.Surface, mask, surfaces) || !new Bounds(triangle).Overlaps(query))
                {
                    continue;
                }

                var sweep = new TriangleCapsuleSweep(triangle, feet, radius, height, unit);
                if (!sweep.Cast(maximum.Raw, out CapsuleSweepTime time, out bool inside, out FixVec3 normal, out FixVec3 point))
                {
                    continue;
                }

                int order = time.CompareTo(bestTime);
                if (best.DidHit && (order > 0 || (order == 0 && triangle.ShapeId >= best.ShapeId)))
                {
                    continue;
                }

                bestTime = time;
                best = new DeterministicHit(triangle.ShapeId, triangle.Layer, triangle.Surface, Fix.FromRaw(time.FloorRaw), point, normal, inside);
            }
        }

        private void RaycastTriangles(
            FixVec3 origin,
            FixVec3 unit,
            Fix maximum,
            int mask,
            FixedSurface surfaces,
            RayDistance currentTime,
            ref DeterministicHit best)
        {
            if (triangleTree.Nodes.Length == 0)
            {
                return;
            }

            BigInteger bestN = currentTime.Numerator,
                bestD = currentTime.Denominator;
            RayTriangleNode(0, Bounds.Sweep(origin, unit, maximum, 0, 0), origin, unit, maximum, mask, surfaces, ref bestN, ref bestD, ref best);
        }

        private void RayTriangleNode(
            int index,
            Bounds query,
            FixVec3 origin,
            FixVec3 unit,
            Fix maximum,
            int mask,
            FixedSurface surfaces,
            ref BigInteger bestN,
            ref BigInteger bestD,
            ref DeterministicHit best)
        {
            TriangleTree.Node node = triangleTree.Nodes[index];
            if (!node.Bounds.Overlaps(query))
            {
                return;
            }

            if (node.Count == 0)
            {
                RayTriangleNode(node.Left, query, origin, unit, maximum, mask, surfaces, ref bestN, ref bestD, ref best);
                RayTriangleNode(node.Right, query, origin, unit, maximum, mask, surfaces, ref bestN, ref bestD, ref best);
                return;
            }

            for (int i = 0; i < node.Count; i++)
            {
                FixedWorldTriangle triangle = triangles[triangleTree.Order[node.Start + i]];
                if (!Matches(triangle.Layer, triangle.Surface, mask, surfaces) || !new Bounds(triangle).Overlaps(query))
                {
                    continue;
                }

                WideVector a = new WideVector(triangle.A),
                    b = new WideVector(triangle.B),
                    c = new WideVector(triangle.C),
                    o = new WideVector(origin),
                    v = new WideVector(unit);
                WideVector normal = WideVector.Cross(b - a, c - a);
                BigInteger denominator = WideVector.Dot(normal, v),
                    numerator = WideVector.Dot(normal, a - o) * Fix.Scale;
                if (denominator.IsZero)
                {
                    continue;
                }

                if (denominator.Sign < 0)
                {
                    denominator = -denominator;
                    numerator = -numerator;
                }

                if (numerator.Sign < 0 || numerator > maximum.Raw * denominator)
                {
                    continue;
                }

                int order = (numerator * bestD).CompareTo(bestN * denominator);
                if (best.DidHit && (order > 0 || (order == 0 && triangle.ShapeId >= best.ShapeId)))
                {
                    continue;
                }

                BigInteger pointDenominator = denominator * Fix.Scale;
                WideVector point = o * pointDenominator + v * numerator;
                if (WideVector.Dot(WideVector.Cross(b - a, point - a * pointDenominator), normal).Sign < 0
                    || WideVector.Dot(WideVector.Cross(c - b, point - b * pointDenominator), normal).Sign < 0
                    || WideVector.Dot(WideVector.Cross(a - c, point - c * pointDenominator), normal).Sign < 0)
                {
                    continue;
                }

                if (WideVector.Dot(normal, v).Sign > 0)
                {
                    normal = -normal;
                }

                bestN = numerator;
                bestD = denominator;
                best = new DeterministicHit(
                    triangle.ShapeId,
                    triangle.Layer,
                    triangle.Surface,
                    Fix.FromRaw((int)(numerator / denominator)),
                    point.ToPoint(pointDenominator),
                    normal.Normalized());
            }
        }
    }
}

using System;
using AFPS.Math;
using AFPS.Simulation.World;
using AFPS.Simulation.Characters.Collision;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class TriangleWorldTests
    {
        private static Fix F(int x) => Fix.FromInt(x);
        private static FixVec3 V(int x, int y, int z) => new FixVec3(F(x), F(y), F(z));
        private static FixedWorldTriangle Floor(uint id = 1) => new FixedWorldTriangle(id, 0, FixedSurface.Solid | FixedSurface.Ground, V(-10, 0, -10), V(0, 0, 10), V(10, 0, -10));
        private static FixedWorld World(FixedWorldTriangle triangle) => new FixedWorld(null, null, new[] { triangle });
        [Test]
        public void Raycast_UsesActualTriangleAndBothFaces()
        {
            var world = World(Floor());
            var hit = world.Raycast(V(0, 3, 0), -FixVec3.Up, F(5));
            Assert.That(hit.DidHit, Is.True);
            Assert.That(hit.Distance, Is.EqualTo(F(3)));
            Assert.That(hit.Normal, Is.EqualTo(FixVec3.Up));
            Assert.That(world.Raycast(V(0, -3, 0), FixVec3.Up, F(5)).Normal, Is.EqualTo(-FixVec3.Up));
            Assert.That(world.Raycast(V(9, 3, 9), -FixVec3.Up, F(5)).DidHit, Is.False);
        }

        [Test]
        public void Capsule_FloorAndUndersideRespectFullHeight()
        {
            var world = World(Floor());
            var hit = world.CapsuleCast(V(0, 1, 0), Fix.Half, F(2), -FixVec3.Up, F(5));
            Assert.That(hit.Distance, Is.EqualTo(Fix.One));
            Assert.That(hit.Normal, Is.EqualTo(FixVec3.Up));
            Assert.That(hit.Point.Y, Is.EqualTo(Fix.Zero));
            hit = world.CapsuleCast(V(0, -3, 0), Fix.Half, F(2), FixVec3.Up, F(5));
            Assert.That(hit.Distance, Is.EqualTo(Fix.One));
            Assert.That(hit.Normal, Is.EqualTo(-FixVec3.Up));
        }

        [Test]
        public void Capsule_VerticalTriangleUsesExtrudedEdges()
        {
            var triangle = new FixedWorldTriangle(1, 0, FixedSurface.Solid, V(2, 0, -3), V(2, 5, 0), V(2, 0, 3));
            var hit = World(triangle).CapsuleCast(FixVec3.Zero, Fix.Half, F(2), FixVec3.Right, F(4));
            Assert.That(hit.DidHit, Is.True);
            Assert.That(hit.Distance.Raw, Is.EqualTo(98304));
            Assert.That(hit.Normal, Is.EqualTo(-FixVec3.Right));
            Assert.That(hit.Point.X, Is.EqualTo(F(2)));
            Assert.That(hit.Point.Y.Raw, Is.InRange(0, F(2).Raw));
        }

        [Test]
        public void Capsule_SphereHeightAndTinyRadiusRemainValid()
        {
            var world = World(Floor());
            Assert.That(world.CapsuleCast(V(0, 1, 0), Fix.Half, Fix.One, -FixVec3.Up, F(4)).Distance, Is.EqualTo(Fix.One));
            Assert.That(world.CapsuleCast(V(0, 1, 0), Fix.Epsilon, Fix.FromRaw(2), -FixVec3.Up, F(4)).Distance, Is.EqualTo(Fix.One));
        }

        [Test]
        public void Capsule_InitialTouchAndOverlapAllowEscape()
        {
            var world = World(Floor());
            Assert.That(world.CapsuleCast(FixVec3.Zero, Fix.Half, F(2), FixVec3.Up, Fix.One).DidHit, Is.False);
            Assert.That(world.CapsuleCast(FixVec3.Zero, Fix.Half, F(2), FixVec3.Right, Fix.One).DidHit, Is.False);
            var hit = world.CapsuleCast(new FixVec3(Fix.Zero, Fix.FromRatio(-1, 10), Fix.Zero), Fix.Half, F(2), -FixVec3.Up, Fix.One);
            Assert.That(hit.DidHit, Is.True);
            Assert.That(hit.StartedInside, Is.True);
            Assert.That(hit.Normal, Is.EqualTo(FixVec3.Up));
        }

        [Test]
        public void Capsule_SlopeDistanceAndNormalMatchAnalyticPlane()
        {
            var triangle = new FixedWorldTriangle(1, 0, FixedSurface.Solid, V(-10, -5, -10), V(0, 0, 10), V(10, 5, -10));
            var hit = World(triangle).CapsuleCast(V(0, 2, 0), Fix.Half, F(2), -FixVec3.Up, F(4));
            double expected = 2.5 - 0.5 * System.Math.Sqrt(1.25);
            Assert.That(hit.DidHit, Is.True);
            Assert.That(System.Math.Abs(hit.Distance.Raw - expected * 65536), Is.LessThan(1));
            Assert.That(hit.Normal.X.Raw, Is.InRange(-29312, -29306));
            Assert.That(hit.Normal.Y.Raw, Is.InRange(58615, 58620));
        }

        [Test]
        public void Motor_ClimbsRealSlopeWithoutZeroDistanceStall()
        {
            var triangle = new FixedWorldTriangle(1, 0, FixedSurface.Solid, V(-10, -5, -10), V(0, 0, 10), V(10, 5, -10));
            var world = new FixedCharacterCollisionWorld(World(triangle));
            var start = new FixVec3(Fix.Zero, Fix.FromRaw(3866), Fix.Zero);
            KinematicCharacterMotor.Move(start, FixVec3.Right, Fix.One, CharacterCollisionConfig.Default, world, out var position, out _, out var grounded);
            Assert.That(position.X.Raw, Is.InRange(52300, 52500));
            Assert.That(position.Y.Raw, Is.InRange(29900, 30200));
            Assert.That(grounded, Is.True);
        }

        [Test]
        public void MasksIdentityAndWinding_DoNotChangeEarliestContact()
        {
            var a = Floor(20);
            var b = new FixedWorldTriangle(2, 3, FixedSurface.Wall, a.C, a.B, a.A);
            var world = new FixedWorld(null, null, new[] { a, b });
            Assert.That(world.Raycast(V(0, 2, 0), -FixVec3.Up, F(4)).ShapeId, Is.EqualTo(2));
            Assert.That(world.CapsuleCast(V(0, 2, 0), Fix.Half, F(2), -FixVec3.Up, F(4), ~0, FixedSurface.All).ShapeId, Is.EqualTo(2));
            Assert.That(world.CapsuleCast(V(0, 2, 0), Fix.Half, F(2), -FixVec3.Up, F(4)).ShapeId, Is.EqualTo(20));
        }

        [Test]
        public void SnapshotV2_RoundTripsCanonicalTrianglesWithSegmentOffset()
        {
            var world = new FixedWorld(null, null, new[] { Floor(20), Floor(2) });
            byte[] bytes = world.CreateSnapshot();
            Assert.That(bytes[4], Is.EqualTo(2));
            Assert.That(bytes.Length, Is.EqualTo(116));
            byte[] storage = new byte[bytes.Length + 7];
            Array.Copy(bytes, 0, storage, 3, bytes.Length);
            Assert.That(FixedWorldCodec.TryDeserialize(new ArraySegment<byte>(storage, 3, bytes.Length), out var restored, out _), Is.True);
            Assert.That(restored.TriangleCount, Is.EqualTo(2));
            Assert.That(restored.GetTriangle(0).ShapeId, Is.EqualTo(2));
            Assert.That(restored.VersionHash, Is.EqualTo(world.VersionHash));
            CollectionAssert.AreEqual(bytes, restored.CreateSnapshot());
        }

        [Test]
        public void SnapshotV2_RejectsTruncationInvalidCountsAndDegeneracy()
        {
            byte[] bytes = World(Floor()).CreateSnapshot();
            for (int i = 0; i < bytes.Length; i++)
            {
                Assert.That(FixedWorldCodec.TryDeserialize(new ArraySegment<byte>(bytes, 0, i), out _, out _), Is.False);
            }

            byte[] copy = (byte[])bytes.Clone();
            copy[16] = 0;
            Assert.That(FixedWorldCodec.TryDeserialize(new ArraySegment<byte>(copy), out _, out _), Is.False);
            copy = (byte[])bytes.Clone();
            Array.Copy(copy, 32, copy, 44, 12);
            Array.Copy(copy, 32, copy, 56, 12);
            Assert.That(FixedWorldCodec.TryDeserialize(new ArraySegment<byte>(copy), out _, out _), Is.False);
        }

        [Test]
        public void Constructor_RejectsCollinearityAndCrossTypeIdentityCollisions()
        {
            Assert.Throws<ArgumentException>(() => new FixedWorldTriangle(1, 0, FixedSurface.Solid, FixVec3.Zero, FixVec3.Right, V(2, 0, 0)));
            Assert.Throws<ArgumentException>(() => new FixedWorld(null, new[] { new FixedWorldPlane(1, 0, FixedSurface.Solid, FixedAxis.Y, Fix.Zero) }, new[] { Floor() }));
        }

        [Test]
        public void CoplanarTileEdges_DoNotConsumeSlideIterations()
        {
            var triangles = new FixedWorldTriangle[40];
            int at = 0;
            for (int x = 0; x < 20; x++)
            {
                triangles[at] = new FixedWorldTriangle((uint)at + 1, 0, FixedSurface.Solid, V(x, 0, -2), V(x, 0, 2), V(x + 1, 0, 2));
                at++;
                triangles[at] = new FixedWorldTriangle((uint)at + 1, 0, FixedSurface.Solid, V(x, 0, -2), V(x + 1, 0, 2), V(x + 1, 0, -2));
                at++;
            }

            var backend = new FixedCharacterCollisionWorld(new FixedWorld(null, null, triangles));
            KinematicCharacterMotor.Move(V(0, 0, 0), V(19, 0, 0), Fix.One, CharacterCollisionConfig.Default, backend, out var position, out _, out var grounded);
            Assert.That(position.X.Raw, Is.InRange(F(19).Raw - 2, F(19).Raw + 2));
            Assert.That(grounded, Is.True);
        }

        [TestCase(0)]
        [TestCase(32768)]
        public void GroundCheck_FindsTriangleSupport(int radiusRaw)
        {
            var hit = World(Floor()).GroundCheck(V(0, 2, 0), Fix.FromRaw(radiusRaw), Fix.One, F(2));
            Assert.That(hit.DidHit, Is.True);
            Assert.That(hit.Distance, Is.EqualTo(Fix.One));
            Assert.That(hit.Normal, Is.EqualTo(FixVec3.Up));
        }

        private readonly struct D
        {
            public readonly double X,
                Y,
                Z;
            public D(double x, double y, double z)
            {
                X = x;
                Y = y;
                Z = z;
            }

            public D(FixVec3 v) : this(v.X.ToDouble(), v.Y.ToDouble(), v.Z.ToDouble())
            {
            }

            public static D operator +(D a, D b) => new D(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
            public static D operator -(D a, D b) => new D(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
            public static D operator *(D a, double b) => new D(a.X * b, a.Y * b, a.Z * b);
            public static double Dot(D a, D b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
            public static D Cross(D a, D b) => new D(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        }

        private static double PointTriangle(D p, D a, D b, D c)
        {
            D ab = b - a,
                ac = c - a,
                ap = p - a;
            double d1 = D.Dot(ab, ap),
                d2 = D.Dot(ac, ap);
            if (d1 <= 0 && d2 <= 0)
            {
                return D.Dot(ap, ap);
            }

            D bp = p - b;
            double d3 = D.Dot(ab, bp),
                d4 = D.Dot(ac, bp);
            if (d3 >= 0 && d4 <= d3)
            {
                return D.Dot(bp, bp);
            }

            double vc = d1 * d4 - d3 * d2;
            if (vc <= 0 && d1 >= 0 && d3 <= 0)
            {
                D delta = p - (a + ab * (d1 / (d1 - d3)));
                return D.Dot(delta, delta);
            }

            D cp = p - c;
            double d5 = D.Dot(ab, cp),
                d6 = D.Dot(ac, cp);
            if (d6 >= 0 && d5 <= d6)
            {
                return D.Dot(cp, cp);
            }

            double vb = d5 * d2 - d1 * d6;
            if (vb <= 0 && d2 >= 0 && d6 <= 0)
            {
                D delta = p - (a + ac * (d2 / (d2 - d6)));
                return D.Dot(delta, delta);
            }

            double va = d3 * d6 - d5 * d4;
            if (va <= 0 && d4 - d3 >= 0 && d5 - d6 >= 0)
            {
                D delta = p - (b + (c - b) * ((d4 - d3) / (d4 - d3 + d5 - d6)));
                return D.Dot(delta, delta);
            }

            double den = 1 / (va + vb + vc);
            D q = p - (a + ab * (vb * den) + ac * (vc * den));
            return D.Dot(q, q);
        }

        private static double SegmentSegment(D p, D q, D a, D b)
        {
            D u = q - p,
                v = b - a,
                w = p - a;
            double aa = D.Dot(u, u),
                bb = D.Dot(v, v),
                uv = D.Dot(u, v),
                uw = D.Dot(u, w),
                vw = D.Dot(v, w);
            double s = aa < 1e-24 ? 0 : aa * bb - uv * uv > 1e-24 ? System.Math.Max(0, System.Math.Min(1, (uv * vw - bb * uw) / (aa * bb - uv * uv))) : 0;
            double t = bb < 1e-24 ? 0 : (uv * s + vw) / bb;
            if (t < 0)
            {
                t = 0;
                s = aa < 1e-24 ? 0 : System.Math.Max(0, System.Math.Min(1, -uw / aa));
            }
            else if (t > 1)
            {
                t = 1;
                s = aa < 1e-24 ? 0 : System.Math.Max(0, System.Math.Min(1, (uv - uw) / aa));
            }

            D delta = p + u * s - a - v * t;
            return D.Dot(delta, delta);
        }

        private static double SegmentTriangle(D p, D q, D a, D b, D c)
        {
            D n = D.Cross(b - a, c - a);
            double den = D.Dot(n, q - p);
            if (System.Math.Abs(den) > 1e-24)
            {
                double t = D.Dot(n, a - p) / den;
                if (t >= 0 && t <= 1 && PointTriangle(p + (q - p) * t, a, b, c) < 1e-20)
                {
                    return 0;
                }
            }

            return System.Math.Min(System.Math.Min(PointTriangle(p, a, b, c), PointTriangle(q, a, b, c)), System.Math.Min(SegmentSegment(p, q, a, b), System.Math.Min(SegmentSegment(p, q, b, c), SegmentSegment(p, q, c, a))));
        }

        [Test]
        public void RandomCapsules_MatchIndependentSegmentTriangleDistanceOracle()
        {
            var shapes = new[]
            {
                Floor(),
                new FixedWorldTriangle(1, 0, FixedSurface.Solid, V(-4, -2, -4), V(0, 0, 4), V(4, 2, -4)),
                new FixedWorldTriangle(1, 0, FixedSurface.Solid, V(0, -2, -4), V(0, 4, 0), V(0, -2, 4))
            };
            var random = new FixRandom(84016);
            int contacts = 0;
            for (int i = 0; i < 500; i++)
            {
                var triangle = shapes[i % 3];
                D a = new D(triangle.A),
                    b = new D(triangle.B),
                    c = new D(triangle.C);
                var feet = new FixVec3(Fix.FromRatio(random.NextInt(-40, 41), 10), Fix.FromRatio(random.NextInt(-30, 51), 10), Fix.FromRatio(random.NextInt(-40, 41), 10));
                Fix radius = Fix.FromRatio(random.NextInt(1, 11), 10),
                    height = radius * F(2) + Fix.FromRatio(random.NextInt(0, 21), 10);
                FixVec3 direction = i % 2 == 0 ? -(feet + FixVec3.Up * (height * Fix.Half)) : V(random.NextInt(-8, 9), random.NextInt(-8, 9), random.NextInt(-8, 9));
                if (direction == FixVec3.Zero)
                {
                    continue;
                }

                D unit = new D(direction.Normalized()),
                    origin = new D(feet) + new D(0, radius.ToDouble(), 0),
                    offset = new D(0, height.ToDouble() - 2 * radius.ToDouble(), 0);
                double r2 = radius.ToDouble() * radius.ToDouble();
                double Distance(double t) => SegmentTriangle(origin + unit * t, origin + offset + unit * t, a, b, c);
                if (Distance(0) <= r2)
                {
                    continue;
                }

                double low = 0,
                    high = 8;
                for (int iteration = 0; iteration < 90; iteration++)
                {
                    double left = (2 * low + high) / 3,
                        right = (low + 2 * high) / 3;
                    if (Distance(left) <= Distance(right))
                    {
                        high = right;
                    }
                    else
                    {
                        low = left;
                    }
                }

                double minimum = (low + high) / 2;
                bool expected = Distance(minimum) <= r2;
                var hit = World(triangle).CapsuleCast(feet, radius, height, direction, F(8));
                Assert.That(hit.DidHit, Is.EqualTo(expected), "sample=" + i);
                if (!expected)
                {
                    continue;
                }

                contacts++;
                low = 0;
                high = minimum;
                for (int iteration = 0; iteration < 80; iteration++)
                {
                    double mid = (low + high) / 2;
                    if (Distance(mid) > r2)
                    {
                        low = mid;
                    }
                    else
                    {
                        high = mid;
                    }
                }

                Assert.That(System.Math.Abs(hit.Distance.Raw - (low + high) / 2 * 65536), Is.LessThan(1.05), "sample=" + i);
                Assert.That(PointTriangle(new D(hit.Point), a, b, c), Is.LessThan(2e-9), "point sample=" + i);
            }

            Assert.That(contacts, Is.GreaterThan(40));
        }

        [Test]
        public void BvhMatchesPerTriangleTraversalAcrossRandomRaysAndCapsules()
        {
            var triangles = new FixedWorldTriangle[40];
            var singles = new FixedWorld[40];
            for (int i = 0; i < 40; i++)
            {
                var shift = V(i % 8 * 3, 0, i / 8 * 3);
                triangles[i] = new FixedWorldTriangle((uint)i + 1, 0, FixedSurface.Solid, shift, shift + V(0, 1, 2), shift + V(2, 0, 0));
                singles[i] = World(triangles[i]);
            }

            var world = new FixedWorld(null, null, triangles);
            var random = new FixRandom(7001);
            for (int sample = 0; sample < 200; sample++)
            {
                var origin = V(random.NextInt(-2, 26), random.NextInt(2, 6), random.NextInt(-2, 17));
                var direction = new FixVec3(Fix.FromRatio(random.NextInt(-10, 11), 10), -Fix.One, Fix.FromRatio(random.NextInt(-10, 11), 10));
                var ray = world.Raycast(origin, direction, F(10));
                var capsule = world.CapsuleCast(origin, Fix.Half, F(2), direction, F(10));
                int minRay = int.MaxValue,
                    minCapsule = int.MaxValue;
                foreach (var single in singles)
                {
                    var r = single.Raycast(origin, direction, F(10));
                    var c = single.CapsuleCast(origin, Fix.Half, F(2), direction, F(10));
                    if (r.DidHit)
                    {
                        minRay = System.Math.Min(minRay, r.Distance.Raw);
                    }

                    if (c.DidHit)
                    {
                        minCapsule = System.Math.Min(minCapsule, c.Distance.Raw);
                    }
                }

                Assert.That(ray.DidHit, Is.EqualTo(minRay != int.MaxValue));
                Assert.That(capsule.DidHit, Is.EqualTo(minCapsule != int.MaxValue));
                if (ray.DidHit)
                {
                    Assert.That(ray.Distance.Raw, Is.EqualTo(minRay));
                }

                if (capsule.DidHit)
                {
                    Assert.That(capsule.Distance.Raw, Is.EqualTo(minCapsule));
                }
            }
        }
    }
}

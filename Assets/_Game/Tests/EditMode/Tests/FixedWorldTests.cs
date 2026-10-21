using System;
using AFPS.Math;
using AFPS.Simulation.World;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class FixedWorldTests
    {
        private static Fix F(int value) => Fix.FromInt(value);
        private static FixVec3 V(int x, int y, int z) => new FixVec3(F(x), F(y), F(z));
        private static FixedWorldBox Box(uint id, int layer = 0, FixedSurface surface = FixedSurface.All) => new FixedWorldBox(id, layer, surface, new FixedAabb(V(2, -1, -1), V(4, 1, 1)));
        private static FixedWorldPlane Ground(uint id, int height = 0) => new FixedWorldPlane(id, 0, FixedSurface.Solid | FixedSurface.Ground, FixedAxis.Y, F(height));
        private static FixedWorld Load(byte[] bytes)
        {
            Assert.That(FixedWorldCodec.TryDeserialize(new ArraySegment<byte>(bytes), out FixedWorld world, out FixedWorldReadError error), Is.True, error.ToString());
            return world;
        }

        [Test]
        public void Ray_AxisAlignedHitHasGoldenDistancePointAndNormal()
        {
            IWorldQuery world = new FixedWorld(new[] { Box(10) });
            DeterministicHit hit = world.Raycast(FixVec3.Zero, V(7, 0, 0), F(10));
            Assert.That(hit.DidHit, Is.True);
            Assert.That(hit.ShapeId, Is.EqualTo(10));
            Assert.That(hit.Distance.Raw, Is.EqualTo(131072));
            Assert.That(hit.Point, Is.EqualTo(V(2, 0, 0)));
            Assert.That(hit.Normal, Is.EqualTo(-FixVec3.Right));
            Assert.That(hit.StartedInside, Is.False);
            hit = world.Raycast(V(6, 0, 0), -FixVec3.Right, F(10));
            Assert.That(hit.Distance.Raw, Is.EqualTo(131072));
            Assert.That(hit.Normal, Is.EqualTo(FixVec3.Right));
        }

        [TestCase(0, 1, 0)]
        [TestCase(0, 0, 1)]
        [TestCase(0, 0, 0)]
        [TestCase(-1, 0, 0)]
        public void Ray_ParallelOutsideZeroAndBehindDoNotHit(int x, int y, int z)
        {
            var world = new FixedWorld(new[] { Box(1) });
            Assert.That(world.Raycast(FixVec3.Zero, V(x, y, z), F(10)).DidHit, Is.False);
        }

        [Test]
        public void Ray_DistanceLimitIncludesEndpointAndRejectsNegativeRange()
        {
            var world = new FixedWorld(new[] { Box(1) });
            Assert.That(world.Raycast(FixVec3.Zero, FixVec3.Right, F(2)).DidHit, Is.True);
            Assert.That(world.Raycast(FixVec3.Zero, FixVec3.Right, Fix.FromRaw(131071)).DidHit, Is.False);
            Assert.That(world.Raycast(FixVec3.Zero, FixVec3.Right, Fix.Zero).DidHit, Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => world.Raycast(FixVec3.Zero, FixVec3.Right, -Fix.One));
        }

        [Test]
        public void Ray_LayerAndSemanticMasksFilterNearestHit()
        {
            var world = new FixedWorld(new[] { Box(10, 0, FixedSurface.Wall), Box(20, 31, FixedSurface.Ladder) });
            Assert.That(world.Raycast(FixVec3.Zero, FixVec3.Right, F(10), 0).DidHit, Is.False);
            Assert.That(world.Raycast(FixVec3.Zero, FixVec3.Right, F(10), ~0, FixedSurface.None).DidHit, Is.False);
            Assert.That(world.Raycast(FixVec3.Zero, FixVec3.Right, F(10), 1 << 31).ShapeId, Is.EqualTo(20));
            Assert.That(world.Raycast(FixVec3.Zero, FixVec3.Right, F(10), ~0, FixedSurface.Ladder).ShapeId, Is.EqualTo(20));
            Assert.That(world.Raycast(FixVec3.Zero, FixVec3.Right, F(10), ~0, FixedSurface.Grapple).DidHit, Is.False);
        }

        [Test]
        public void Ray_InsideAndSurfaceOriginsHaveExplicitSemantics()
        {
            var world = new FixedWorld(new[] { Box(1) });
            DeterministicHit inside = world.Raycast(V(3, 0, 0), FixVec3.Right, Fix.Zero);
            Assert.That(inside.DidHit && inside.StartedInside, Is.True);
            Assert.That(inside.Distance, Is.EqualTo(Fix.Zero));
            Assert.That(inside.Normal, Is.EqualTo(FixVec3.Zero));
            Assert.That(world.Raycast(V(2, 0, 0), -FixVec3.Right, F(1)).DidHit, Is.False);
            Assert.That(world.Raycast(V(2, 0, 0), FixVec3.Up, F(1)).DidHit, Is.False);
            DeterministicHit inward = world.Raycast(V(2, 0, 0), FixVec3.Right, Fix.Zero);
            Assert.That(inward.DidHit && !inward.StartedInside, Is.True);
            Assert.That(inward.Normal, Is.EqualTo(-FixVec3.Right));
        }

        [Test]
        public void Ray_EqualDistanceUsesStableIdAndCornerUsesXAxis()
        {
            var world = new FixedWorld(new[] { Box(20), Box(10) }, new[] { new FixedWorldPlane(5, 0, FixedSurface.Wall, FixedAxis.X, F(2), false) });
            Assert.That(world.Raycast(FixVec3.Zero, FixVec3.Right, F(10)).ShapeId, Is.EqualTo(5));
            var corner = new FixedWorld(new[] { new FixedWorldBox(1, 0, FixedSurface.Wall, new FixedAabb(V(1, 1, -1), V(2, 2, 1))) });
            Assert.That(corner.Raycast(FixVec3.Zero, V(1, 1, 0), F(4)).Normal, Is.EqualTo(-FixVec3.Right));
        }

        [Test]
        public void Ray_OneRawThinSlabAndNearParallelDirectionAreRetained()
        {
            FixVec3 min = new FixVec3(F(1), Fix.FromRaw(-10), -Fix.One);
            FixVec3 max = new FixVec3(Fix.FromRaw(65537), Fix.FromRaw(10), Fix.One);
            var world = new FixedWorld(new[] { new FixedWorldBox(1, 0, FixedSurface.Wall, new FixedAabb(min, max)) });
            FixVec3 direction = new FixVec3(Fix.One, Fix.Epsilon, Fix.Zero);
            Assert.That(world.Raycast(FixVec3.Zero, direction, F(2)).Distance, Is.EqualTo(Fix.One));
            // 大范围近乎平行：远端 slab 距离大于 int，但应直接判 miss，不溢出。
            Assert.That(world.Raycast(V(0, 2, 0), direction, Fix.MaxValue).DidHit, Is.False);
        }

        [Test]
        public void Ray_SubRawOrderingDoesNotCollapseToIdTie()
        {
            var farther = new FixedWorldPlane(1, 0, FixedSurface.Wall, FixedAxis.Y, Fix.FromRaw(1), false);
            var nearer = new FixedWorldPlane(2, 0, FixedSurface.Wall, FixedAxis.X, Fix.FromRaw(1), false);
            var world = new FixedWorld(null, new[] { farther, nearer });
            // 方向 (3,2)；X 距离 1/dx 小于 Y 距离 1/dy，但二者向下取整后都是 1 Raw。
            Assert.That(world.Raycast(FixVec3.Zero, V(3, 2, 0), F(1)).ShapeId, Is.EqualTo(2));
        }

        [Test]
        public void Ray_ExtremeCoordinatesAndUnrepresentablePlanePointDoNotOverflow()
        {
            var bounds = new FixedAabb(new FixVec3(Fix.MinValue, -Fix.One, -Fix.One), new FixVec3(Fix.FromRaw(int.MinValue + 65536), Fix.One, Fix.One));
            var world = new FixedWorld(new[] { new FixedWorldBox(1, 0, FixedSurface.Wall, bounds) });
            Assert.That(world.Raycast(new FixVec3(Fix.MaxValue, Fix.Zero, Fix.Zero), -FixVec3.Right, Fix.MaxValue).DidHit, Is.False);
            DeterministicHit hit = world.Raycast(new FixVec3(Fix.FromRaw(int.MinValue + 131072), Fix.Zero, Fix.Zero), -FixVec3.Right, F(3));
            Assert.That(hit.Distance, Is.EqualTo(Fix.One));
            var plane = new FixedWorld(null, new[] { Ground(2) });
            Assert.That(plane.Raycast(new FixVec3(Fix.MaxValue, Fix.One, Fix.Zero), V(1, -1, 0), F(3)).DidHit, Is.False);
        }

        [Test]
        public void Plane_OneSidedAndDoubleSidedNormalsFollowApproach()
        {
            var oneSided = new FixedWorld(null, new[] { Ground(1) });
            Assert.That(oneSided.Raycast(V(0, 2, 0), -FixVec3.Up, F(3)).Normal, Is.EqualTo(FixVec3.Up));
            Assert.That(oneSided.Raycast(V(0, -2, 0), FixVec3.Up, F(3)).DidHit, Is.False);
            var twoSided = new FixedWorld(null, new[] { new FixedWorldPlane(2, 0, FixedSurface.Ground, FixedAxis.Y, Fix.Zero, true, true) });
            Assert.That(twoSided.Raycast(V(0, -2, 0), FixVec3.Up, F(3)).Normal, Is.EqualTo(-FixVec3.Up));
            Assert.That(twoSided.Raycast(FixVec3.Zero, FixVec3.Right, F(3)).DidHit, Is.False);
        }

        [Test]
        public void GroundCheck_UsesFeetProbeRangeAndNearestSupport()
        {
            var platform = new FixedWorldBox(2, 0, FixedSurface.Ground, new FixedAabb(V(-2, -1, -2), V(2, 1, 2)));
            var world = new FixedWorld(new[] { platform }, new[] { Ground(1) });
            DeterministicHit hit = world.GroundCheck(V(0, 3, 0), Fix.Half, F(1), F(2));
            Assert.That(hit.ShapeId, Is.EqualTo(2));
            Assert.That(hit.Distance, Is.EqualTo(Fix.One));
            Assert.That(hit.Point, Is.EqualTo(V(0, 1, 0)));
            Assert.That(hit.Normal, Is.EqualTo(FixVec3.Up));
            Assert.That(world.GroundCheck(V(0, 3, 0), Fix.Half, F(1), Fix.Half).DidHit, Is.False);
            Assert.That(world.GroundCheck(V(0, 3, 0), Fix.Half, F(1), F(2), 0).DidHit, Is.False);
        }

        [Test]
        public void GroundCheck_CircularFootprintRejectsSquareCornerFalseSupport()
        {
            var box = new FixedWorldBox(1, 0, FixedSurface.Ground, new FixedAabb(V(0, -1, 0), V(1, 0, 1)));
            var world = new FixedWorld(new[] { box });
            FixVec3 center = new FixVec3(Fix.FromRatio(8, 5), Fix.One, Fix.FromRatio(8, 5));
            Assert.That(world.GroundCheck(center, Fix.FromRatio(4, 5), Fix.One, Fix.One).DidHit, Is.False);
            DeterministicHit edge = world.GroundCheck(new FixVec3(Fix.FromRatio(3, 2), Fix.One, Fix.Half), Fix.Half, Fix.One, Fix.Zero);
            Assert.That(edge.DidHit, Is.True);
            Assert.That(edge.Point, Is.EqualTo(new FixVec3(Fix.One, Fix.Zero, Fix.Half)));
        }

        [Test]
        public void GroundCheck_IgnoresAboveFeetAndNonGroundSurfaces()
        {
            var world = new FixedWorld(new[] { Box(1, 0, FixedSurface.Wall) }, new[] { Ground(2, 2) });
            Assert.That(world.GroundCheck(V(3, 1, 0), Fix.Half, Fix.One, F(10)).DidHit, Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => world.GroundCheck(FixVec3.Zero, -Fix.One, Fix.One, Fix.One));
            Assert.Throws<ArgumentOutOfRangeException>(() => world.GroundCheck(FixVec3.Zero, Fix.One, Fix.Half, Fix.One));
            Assert.Throws<ArgumentOutOfRangeException>(() => world.GroundCheck(FixVec3.Zero, Fix.Zero, Fix.Zero, -Fix.One));
        }

        [Test]
        public void Construction_RejectsInvalidGeometryAndDuplicateIds()
        {
            Assert.Throws<ArgumentException>(() => new FixedAabb(FixVec3.Zero, FixVec3.Zero));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FixedWorld(new FixedWorldBox[1]));
            Assert.Throws<ArgumentException>(() => new FixedWorld(new[] { Box(1), Box(1) }));
            Assert.Throws<ArgumentException>(() => new FixedWorld(new[] { Box(1) }, new[] { Ground(1) }));
            Assert.Throws<ArgumentException>(() => new FixedWorld(new FixedWorldBox[FixedWorldCodec.MaxShapeCount + 1]));
            Assert.Throws<ArgumentOutOfRangeException>(() => Box(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => Box(1, 32));
            Assert.Throws<ArgumentOutOfRangeException>(() => Box(1, 0, (FixedSurface)0x80000000u));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FixedWorldPlane(1, 0, FixedSurface.Wall, (FixedAxis)3, Fix.Zero));
        }

        [Test]
        public void World_OwnsGeometryAndSnapshotAndCanonicalizesInputOrder()
        {
            var input = new[] { Box(20), Box(10) };
            var world = new FixedWorld(input, new[] { Ground(5) });
            byte[] snapshot = world.CreateSnapshot();
            input[1] = Box(99);
            snapshot[0] ^= 255;
            Assert.That(world.GetBox(0).ShapeId, Is.EqualTo(10));
            Assert.That(world.CreateSnapshot()[0], Is.EqualTo((byte)'A'));
            var reordered = new FixedWorld(new[] { Box(10), Box(20) }, new[] { Ground(5) });
            Assert.That(world.VersionHash, Is.EqualTo(reordered.VersionHash));
            CollectionAssert.AreEqual(world.CreateSnapshot(), reordered.CreateSnapshot());
        }

        [Test]
        public void Snapshot_RoundTripPreservesEveryRawAndQuery()
        {
            var box = new FixedWorldBox(0xf0000001u, 31, FixedSurface.Wall | FixedSurface.Ladder, new FixedAabb(new FixVec3(Fix.FromRaw(-131073), -Fix.One, -Fix.One), V(4, 1, 1)));
            var plane = new FixedWorldPlane(20, 2, FixedSurface.Ground, FixedAxis.Y, Fix.FromRaw(-1), false, true);
            var original = new FixedWorld(new[] { box }, new[] { plane });
            byte[] bytes = original.CreateSnapshot();
            var restored = Load(bytes);
            Assert.That(restored.VersionHash, Is.EqualTo(original.VersionHash));
            CollectionAssert.AreEqual(bytes, restored.CreateSnapshot());
            Assert.That(restored.GetBox(0).Bounds.Min.X.Raw, Is.EqualTo(-131073));
            Assert.That(restored.GetPlane(0).Coordinate.Raw, Is.EqualTo(-1));
            Assert.That(restored.GetPlane(0).DoubleSided, Is.True);
            Assert.That(restored.GetPlane(0).PositiveNormal, Is.False);
            Assert.That(restored.Raycast(V(8, 0, 0), -FixVec3.Right, F(10)).Point, Is.EqualTo(original.Raycast(V(8, 0, 0), -FixVec3.Right, F(10)).Point));
        }

        [Test]
        public void Snapshot_ArraySegmentOffsetAndEmptyWorldAreSupported()
        {
            var empty = new FixedWorld(null);
            Assert.That(Load(empty.CreateSnapshot()).BoxCount, Is.Zero);
            Assert.That(empty.Raycast(FixVec3.Zero, FixVec3.Right, F(10)).DidHit, Is.False);
            byte[] snapshot = new FixedWorld(new[] { Box(1) }).CreateSnapshot();
            byte[] offset = new byte[snapshot.Length + 6];
            Array.Copy(snapshot, 0, offset, 3, snapshot.Length);
            Assert.That(FixedWorldCodec.TryDeserialize(new ArraySegment<byte>(offset, 3, snapshot.Length), out FixedWorld world, out _), Is.True);
            Assert.That(world.GetBox(0).ShapeId, Is.EqualTo(1));
        }

        [Test]
        public void Snapshot_LayoutAndContentHashHaveGoldenValues()
        {
            var world = new FixedWorld(new[] { Box(1) }, new[] { Ground(2) });
            byte[] golden = {
                0x41, 0x46, 0x57, 0x31, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0,
                1, 0, 0, 0, 0, 0, 0, 0, 31, 0, 0, 0,
                0, 0, 2, 0, 0, 0, 255, 255, 0, 0, 255, 255,
                0, 0, 4, 0, 0, 0, 1, 0, 0, 0, 1, 0,
                2, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0, 0
            };
            CollectionAssert.AreEqual(golden, world.CreateSnapshot());
            Assert.That(world.VersionHash, Is.EqualTo(393673060225373904UL));
            Assert.That(Load(golden).VersionHash, Is.EqualTo(world.VersionHash));
        }

        [TestCase(0, 0, FixedWorldReadError.InvalidMagic)]
        [TestCase(4, 3, FixedWorldReadError.UnsupportedVersion)]
        [TestCase(6, 1, FixedWorldReadError.ReservedBits)]
        [TestCase(11, 128, FixedWorldReadError.InvalidCount)]
        [TestCase(16, 0, FixedWorldReadError.InvalidShape)]
        [TestCase(20, 32, FixedWorldReadError.InvalidShape)]
        [TestCase(24, 128, FixedWorldReadError.InvalidShape)]
        public void Snapshot_InvalidFieldsAreRejected(int offset, int value, FixedWorldReadError expected)
        {
            byte[] bytes = new FixedWorld(new[] { Box(1) }).CreateSnapshot();
            bytes[offset] = (byte)value;
            Assert.That(FixedWorldCodec.TryDeserialize(new ArraySegment<byte>(bytes), out FixedWorld world, out FixedWorldReadError error), Is.False);
            Assert.That(world, Is.Null);
            Assert.That(error, Is.EqualTo(expected));
        }

        [Test]
        public void Snapshot_TruncationTrailingDataReservedPlaneFlagsAndDuplicateIdsAreRejected()
        {
            byte[] bytes = new FixedWorld(new[] { Box(1) }, new[] { Ground(2) }).CreateSnapshot();
            Assert.That(FixedWorldCodec.TryDeserialize(default, out _, out _), Is.False);
            for (int length = 0; length < bytes.Length; length++) Assert.That(FixedWorldCodec.TryDeserialize(new ArraySegment<byte>(bytes, 0, length), out _, out _), Is.False);
            byte[] extra = new byte[bytes.Length + 1];
            Array.Copy(bytes, extra, bytes.Length);
            Assert.That(FixedWorldCodec.TryDeserialize(new ArraySegment<byte>(extra), out _, out _), Is.False);
            bytes[FixedWorldCodec.HeaderSize + FixedWorldCodec.BoxSize + 13] |= 4;
            Assert.That(FixedWorldCodec.TryDeserialize(new ArraySegment<byte>(bytes), out _, out FixedWorldReadError error), Is.False);
            Assert.That(error, Is.EqualTo(FixedWorldReadError.ReservedBits));
            bytes = new FixedWorld(new[] { Box(1) }, new[] { Ground(2) }).CreateSnapshot();
            bytes[FixedWorldCodec.HeaderSize + FixedWorldCodec.BoxSize] = 1;
            Assert.That(FixedWorldCodec.TryDeserialize(new ArraySegment<byte>(bytes), out _, out error), Is.False);
            Assert.That(error, Is.EqualTo(FixedWorldReadError.InvalidShape));
        }

        [Test]
        public void Snapshot_NonCanonicalRecordOrderIsRejected()
        {
            byte[] bytes = new FixedWorld(new[] { Box(1), Box(2) }).CreateSnapshot();
            bytes[FixedWorldCodec.HeaderSize] = 3;
            Assert.That(FixedWorldCodec.TryDeserialize(new ArraySegment<byte>(bytes), out _, out FixedWorldReadError error), Is.False);
            Assert.That(error, Is.EqualTo(FixedWorldReadError.NonCanonicalOrder));
        }

        [Test]
        public void VersionHash_ChangesForGeometryLayerTagsAndIdentity()
        {
            ulong hash = new FixedWorld(new[] { Box(1) }).VersionHash;
            Assert.That(new FixedWorld(new[] { Box(2) }).VersionHash, Is.Not.EqualTo(hash));
            Assert.That(new FixedWorld(new[] { Box(1, 1) }).VersionHash, Is.Not.EqualTo(hash));
            Assert.That(new FixedWorld(new[] { Box(1, 0, FixedSurface.Solid) }).VersionHash, Is.Not.EqualTo(hash));
            var shifted = new FixedWorldBox(1, 0, FixedSurface.All, new FixedAabb(V(2, -1, -1), V(5, 1, 1)));
            Assert.That(new FixedWorld(new[] { shifted }).VersionHash, Is.Not.EqualTo(hash));
        }

        [Test]
        public void Snapshot_RandomCorruptionIsRejectedOrProducesCanonicalData()
        {
            byte[] original = new FixedWorld(new[] { Box(1) }, new[] { Ground(2) }).CreateSnapshot();
            var random = new FixRandom(9876);
            for (int i = 0; i < 1000; i++)
            {
                byte[] damaged = (byte[])original.Clone();
                damaged[random.NextInt(0, damaged.Length)] = (byte)random.NextInt(0, 256);
                if (FixedWorldCodec.TryDeserialize(new ArraySegment<byte>(damaged), out FixedWorld world, out FixedWorldReadError error))
                    CollectionAssert.AreEqual(damaged, world.CreateSnapshot());
                else
                {
                    Assert.That(error, Is.Not.EqualTo(FixedWorldReadError.None));
                    Assert.That(world, Is.Null);
                }
            }
        }

        [Test]
        public void RandomRays_MatchIndependentDecimalSlabReference()
        {
            var random = new FixRandom(0x12345678);
            int hits = 0;
            for (int i = 0; i < 2000; i++)
            {
                FixVec3 min = V(random.NextInt(-20, 20), random.NextInt(-20, 20), random.NextInt(-20, 20));
                FixVec3 max = min + V(random.NextInt(1, 8), random.NextInt(1, 8), random.NextInt(1, 8));
                FixVec3 origin = V(random.NextInt(-30, 30), random.NextInt(-30, 30), random.NextInt(-30, 30));
                FixVec3 direction = V(random.NextInt(-10, 11), random.NextInt(-10, 11), random.NextInt(-10, 11));
                // 一半随机方向，一半瞄准箱体中心，避免参考比较退化为几乎全是 miss。
                if ((i & 1) == 0) direction = (min + max) * Fix.Half - origin;
                var bounds = new FixedAabb(min, max);
                var world = new FixedWorld(new[] { new FixedWorldBox(1, 0, FixedSurface.Solid, bounds) });
                bool expected = ReferenceRay(bounds, origin, direction.Normalized(), out int distance);
                DeterministicHit hit = world.Raycast(origin, direction, F(100));
                Assert.That(hit.DidHit, Is.EqualTo(expected), "ray " + i);
                if (expected)
                {
                    hits++;
                    Assert.That(hit.Distance.Raw, Is.EqualTo(distance), "distance " + i);
                }
            }
            Assert.That(hits, Is.GreaterThan(500));
        }

        private static bool ReferenceRay(FixedAabb box, FixVec3 origin, FixVec3 direction, out int distance)
        {
            distance = 0;
            if (direction == FixVec3.Zero) return false;
            decimal enter = decimal.MinValue, exit = 100m * 65536m;
            bool inside = true;
            int[] low = { box.Min.X.Raw, box.Min.Y.Raw, box.Min.Z.Raw }, high = { box.Max.X.Raw, box.Max.Y.Raw, box.Max.Z.Raw };
            int[] start = { origin.X.Raw, origin.Y.Raw, origin.Z.Raw }, unit = { direction.X.Raw, direction.Y.Raw, direction.Z.Raw };
            for (int axis = 0; axis < 3; axis++)
            {
                inside &= start[axis] > low[axis] && start[axis] < high[axis];
                if (unit[axis] == 0) { if (start[axis] < low[axis] || start[axis] > high[axis]) return false; continue; }
                decimal a = ((decimal)low[axis] - start[axis]) * 65536m / unit[axis];
                decimal b = ((decimal)high[axis] - start[axis]) * 65536m / unit[axis];
                enter = System.Math.Max(enter, System.Math.Min(a, b));
                exit = System.Math.Min(exit, System.Math.Max(a, b));
                if (enter > exit) return false;
            }
            if (exit < 0 || (enter < 0 && !inside)) return false;
            distance = (int)decimal.Floor(System.Math.Max(0m, enter));
            return true;
        }
    }
}

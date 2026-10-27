using AFPS.Math;
using AFPS.Simulation.Characters.Collision;
using AFPS.Simulation.World;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class TraversalQueryBoundaryTests
    {
        private static FixedWorld Floor() => new FixedWorld(null, new[]
        {
            new FixedWorldPlane(1, 8, FixedSurface.Ground | FixedSurface.Solid, FixedAxis.Y, Fix.Zero)
        });

        private static DeterministicHit Query(IWorldQuery world, int kind, int mask = ~0)
        {
            if (kind == 0)
            {
                return world.Raycast(FixVec3.Up * Fix.FromInt(3), -FixVec3.Up, Fix.FromInt(3), mask);
            }
            if (kind == 1)
            {
                return world.CapsuleCast(FixVec3.Up * Fix.FromInt(3), Fix.Half,
                    Fix.FromInt(2), -FixVec3.Up, Fix.FromInt(3), mask);
            }
            return world.GroundCheck(FixVec3.Up, Fix.Half, Fix.One, Fix.Half, mask);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void Query_PreservesCanonicalRawHitAndWorldIdentity(int kind)
        {
            var world = Floor();
            IWorldQuery query = new FixedCharacterCollisionWorld(world, 1 << 8);
            var expected = Query(world, kind);
            var actual = Query(query, kind);
            Assert.That(query.VersionHash, Is.EqualTo(world.VersionHash));
            Assert.That(actual.DidHit, Is.True);
            Assert.That(actual.ShapeId, Is.EqualTo(expected.ShapeId));
            Assert.That(actual.Distance, Is.EqualTo(expected.Distance));
            Assert.That(actual.Point, Is.EqualTo(expected.Point));
            Assert.That(actual.Normal, Is.EqualTo(expected.Normal));
            Assert.That(actual.Surface, Is.EqualTo(expected.Surface));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void Query_IntersectsCallerAndSessionMasks(int kind)
        {
            IWorldQuery excluded = new FixedCharacterCollisionWorld(Floor(), 1);
            Assert.That(Query(excluded, kind).DidHit, Is.False);
            Assert.That(Query(excluded, kind, 1 << 8).DidHit, Is.False);
            IWorldQuery included = new FixedCharacterCollisionWorld(Floor(), 1 << 8);
            Assert.That(Query(included, kind, 1).DidHit, Is.False);
            Assert.That(Query(included, kind, 0).DidHit, Is.False);
            Assert.That(Query(included, kind, 1 << 8).DidHit, Is.True);
        }

        [Test]
        public void TaggedQueries_KeepSurfaceFilteringAndStableNearestShape()
        {
            var wall = new FixedWorldBox(2, 8, FixedSurface.Solid | FixedSurface.Wall,
                new FixedAabb(new FixVec3(Fix.FromInt(2), Fix.Zero, Fix.FromInt(-2)),
                    new FixVec3(Fix.FromInt(3), Fix.FromInt(4), Fix.FromInt(2))));
            var ladder = new FixedWorldBox(3, 8, FixedSurface.Ladder,
                new FixedAabb(new FixVec3(Fix.FromInt(4), Fix.Zero, Fix.FromInt(-2)),
                    new FixVec3(Fix.FromInt(5), Fix.FromInt(4), Fix.FromInt(2))));
            IWorldQuery query = new FixedCharacterCollisionWorld(new FixedWorld(new[] { ladder, wall }), 1 << 8);
            var origin = FixVec3.Up;
            Assert.That(query.Raycast(origin, FixVec3.Right, Fix.FromInt(10),
                surfaceMask: FixedSurface.Wall).ShapeId, Is.EqualTo(2));
            Assert.That(query.Raycast(origin, FixVec3.Right, Fix.FromInt(10),
                surfaceMask: FixedSurface.Ladder).ShapeId, Is.EqualTo(3));
            Assert.That(query.Raycast(origin, FixVec3.Right, Fix.FromInt(10),
                surfaceMask: FixedSurface.Grapple).DidHit, Is.False);
            var swept = query.CapsuleCast(FixVec3.Zero, Fix.Half, Fix.FromInt(2),
                FixVec3.Right, Fix.FromInt(10), surfaceMask: FixedSurface.Ladder);
            Assert.That(swept.ShapeId, Is.EqualTo(3));
            Assert.That(swept.Distance, Is.EqualTo(Fix.FromRatio(7, 2)));
        }
    }
}

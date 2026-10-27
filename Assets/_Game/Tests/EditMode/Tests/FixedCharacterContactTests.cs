using AFPS.Math;
using AFPS.Simulation.Characters.Collision;
using AFPS.Simulation.World;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class FixedCharacterContactTests
    {
        private static FixVec3 V(int x, int y, int z) =>
            new FixVec3(Fix.FromInt(x), Fix.FromInt(y), Fix.FromInt(z));

        private static FixedCharacterCollisionWorld Walls(bool corner = false)
        {
            var xWall = new FixedWorldBox(1, 0, FixedSurface.Solid, new FixedAabb(V(2, 0, -10), V(3, 10, 10)));
            var zWall = new FixedWorldBox(2, 0, FixedSurface.Solid, new FixedAabb(V(-10, 0, 2), V(10, 10, 3)));
            return new FixedCharacterCollisionWorld(new FixedWorld(corner ? new[] { zWall, xWall } : new[] { xWall }));
        }

        [TestCase(0, true)]
        [TestCase(655, true)]
        [TestCase(656, false)]
        public void StationaryWall_UsesInclusiveSkinDistance(int gapRaw, bool expected)
        {
            var position = new FixVec3(Fix.FromRatio(3, 2) - Fix.FromRaw(gapRaw), Fix.One, Fix.Zero);
            var contact = KinematicCharacterMotor.CollectContacts(position, CharacterCollisionConfig.Default, Walls());
            Assert.That(contact.HasWall, Is.EqualTo(expected));
            if (expected)
            {
                Assert.That(contact.WallNormal, Is.EqualTo(-FixVec3.Right));
                Assert.That(contact.WallDistance.Raw, Is.EqualTo(gapRaw));
            }
        }

        [Test]
        public void Corner_UsesStableNormalTieBreak()
        {
            var contact = KinematicCharacterMotor.CollectContacts(
                new FixVec3(Fix.FromRatio(3, 2), Fix.One, Fix.FromRatio(3, 2)),
                CharacterCollisionConfig.Default, Walls(true));
            Assert.That(contact.HasWall, Is.True);
            Assert.That(contact.WallNormal, Is.EqualTo(-FixVec3.Right));
        }

        [Test]
        public void MoveAndContactOverloads_HaveIdenticalMotionAndClearOnDeparture()
        {
            var world = Walls();
            var position = new FixVec3(Fix.FromRatio(3, 2), Fix.One, Fix.Zero);
            KinematicCharacterMotor.Move(position, -FixVec3.Right, Fix.FromRatio(1, 10),
                CharacterCollisionConfig.Default, world, out var actual, out var velocity, out bool grounded, out var contact);
            KinematicCharacterMotor.Move(position, -FixVec3.Right, Fix.FromRatio(1, 10),
                CharacterCollisionConfig.Default, world, out var expected, out var expectedVelocity, out bool expectedGrounded);
            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(velocity, Is.EqualTo(expectedVelocity));
            Assert.That(grounded, Is.EqualTo(expectedGrounded));
            Assert.That(contact.HasWall, Is.False);
        }

        [Test]
        public void ContactReplay_MatchesGoldenHash()
        {
            Assert.That(DeterministicContactScenario.Run(), Is.EqualTo(DeterministicContactScenario.ExpectedHash));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void GroundAndCeiling_AreNotWalls(bool ceiling)
        {
            var boxes = ceiling ? new[]
            {
                new FixedWorldBox(2, 0, FixedSurface.Solid, new FixedAabb(V(-10, 2, -10), V(10, 3, 10)))
            } : null;
            var world = new FixedCharacterCollisionWorld(new FixedWorld(boxes, new[]
            {
                new FixedWorldPlane(1, 0, FixedSurface.Solid | FixedSurface.Ground, FixedAxis.Y, Fix.Zero)
            }));
            var contact = KinematicCharacterMotor.CollectContacts(FixVec3.Zero, CharacterCollisionConfig.Default, world);
            Assert.That(contact.HasWall, Is.False);
            Assert.That(contact.WallNormal, Is.EqualTo(FixVec3.Zero));
        }

        [Test]
        public void SuccessfulStep_DoesNotReportOldObstacle()
        {
            var box = new FixedWorldBox(2, 0, FixedSurface.Solid | FixedSurface.Ground,
                new FixedAabb(V(1, 0, -2), new FixVec3(Fix.FromInt(3), Fix.FromRatio(1, 5), Fix.FromInt(2))));
            var floor = new FixedWorldPlane(1, 0, FixedSurface.Solid | FixedSurface.Ground, FixedAxis.Y, Fix.Zero);
            var world = new FixedCharacterCollisionWorld(new FixedWorld(new[] { box }, new[] { floor }));
            KinematicCharacterMotor.Move(FixVec3.Zero, V(2, 0, 0), Fix.One,
                CharacterCollisionConfig.Default, world, out _, out _, out bool grounded, out var contact);
            Assert.That(grounded, Is.True);
            Assert.That(contact.HasWall, Is.False);
        }
    }
}

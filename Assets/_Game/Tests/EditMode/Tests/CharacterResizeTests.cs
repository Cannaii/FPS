using System;
using AFPS.Math;
using AFPS.Simulation.Characters.Collision;
using AFPS.Simulation.World;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class CharacterResizeTests
    {
        private static readonly CharacterCollisionConfig Crouched =
            CharacterCollisionConfig.Default.WithHeight(Fix.One);

        private static FixVec3 V(int x, int y, int z) =>
            new FixVec3(Fix.FromInt(x), Fix.FromInt(y), Fix.FromInt(z));

        private static IFixedCharacterCollisionWorld Ceiling(Fix y, int layer = 0, int mask = ~0)
        {
            var box = new FixedWorldBox(1, layer, FixedSurface.Solid,
                new FixedAabb(new FixVec3(Fix.FromInt(-5), y, Fix.FromInt(-5)), V(5, 4, 5)));
            return new FixedCharacterCollisionWorld(new FixedWorld(new[] { box }), mask);
        }

        [TestCase(-1, false)]
        [TestCase(0, false)]
        [TestCase(1, true)]
        public void StandUp_RequiresSkinClearanceAndTreatsEndpointAsBlocked(int offsetRaw, bool allowed)
        {
            Fix ceilingY = Fix.FromInt(2) + Crouched.SkinWidthFixed + Fix.FromRaw(offsetRaw);
            Assert.That(KinematicCharacterMotor.CanResizeAtFeet(FixVec3.Zero,
                Crouched, Fix.FromInt(2), Ceiling(ceilingY)), Is.EqualTo(allowed));
        }

        [Test]
        public void LowCeiling_BlocksStandingButAllowsShrinkingAndUnchangedHeight()
        {
            var world = Ceiling(Fix.FromRatio(3, 2));
            Assert.That(KinematicCharacterMotor.CanResizeAtFeet(FixVec3.Zero,
                Crouched, Fix.FromInt(2), world), Is.False);
            Assert.That(KinematicCharacterMotor.CanResizeAtFeet(FixVec3.Zero,
                CharacterCollisionConfig.Default, Fix.One, world), Is.True);
            Assert.That(KinematicCharacterMotor.CanResizeAtFeet(FixVec3.Zero,
                Crouched, Fix.One, world), Is.True);
        }

        [Test]
        public void GroundAndTangentWall_DoNotBlockStanding()
        {
            var wall = new FixedWorldBox(1, 0, FixedSurface.Solid,
                new FixedAabb(new FixVec3(Fix.Half, Fix.Zero, Fix.FromInt(-5)), V(2, 5, 5)));
            var ground = new FixedWorldPlane(2, 0, FixedSurface.Ground | FixedSurface.Solid, FixedAxis.Y, Fix.Zero);
            var world = new FixedCharacterCollisionWorld(new FixedWorld(new[] { wall }, new[] { ground }));
            Assert.That(KinematicCharacterMotor.CanResizeAtFeet(FixVec3.Zero,
                Crouched, Fix.FromInt(2), world), Is.True);
        }

        [Test]
        public void OffCenterOverhang_IsDetectedByCapsuleWidth()
        {
            var box = new FixedWorldBox(1, 0, FixedSurface.Solid,
                new FixedAabb(new FixVec3(Fix.FromRatio(1, 4), Fix.FromRatio(3, 2), Fix.FromInt(-2)), V(3, 4, 2)));
            var world = new FixedCharacterCollisionWorld(new FixedWorld(new[] { box }));
            Assert.That(KinematicCharacterMotor.CanResizeAtFeet(FixVec3.Zero,
                Crouched, Fix.FromInt(2), world), Is.False);
        }

        [Test]
        public void NonSolidOrExcludedLayers_DoNotBlockStanding()
        {
            Assert.That(KinematicCharacterMotor.CanResizeAtFeet(FixVec3.Zero,
                Crouched, Fix.FromInt(2), Ceiling(Fix.FromRatio(3, 2), 8, 1)), Is.True);
            var trigger = new FixedWorldBox(1, 0, FixedSurface.Ladder,
                new FixedAabb(V(-5, 1, -5), V(5, 4, 5)));
            var world = new FixedCharacterCollisionWorld(new FixedWorld(new[] { trigger }));
            Assert.That(KinematicCharacterMotor.CanResizeAtFeet(FixVec3.Zero,
                Crouched, Fix.FromInt(2), world), Is.True);
        }

        [Test]
        public void SlopedTriangle_AndOneSidedCeilingBlockGrowth()
        {
            var triangle = new FixedWorldTriangle(1, 0, FixedSurface.Solid,
                V(-5, 1, -5), V(5, 3, -5), V(0, 2, 5));
            var world = new FixedCharacterCollisionWorld(new FixedWorld(null, null, new[] { triangle }));
            Assert.That(KinematicCharacterMotor.CanResizeAtFeet(FixVec3.Zero,
                Crouched, Fix.FromInt(2), world), Is.False);
            var ceiling = new FixedWorldPlane(2, 0, FixedSurface.Solid,
                FixedAxis.Y, Fix.FromRatio(3, 2), positiveNormal: false);
            world = new FixedCharacterCollisionWorld(new FixedWorld(null, new[] { ceiling }));
            Assert.That(KinematicCharacterMotor.CanResizeAtFeet(FixVec3.Zero,
                Crouched, Fix.FromInt(2), world), Is.False);
        }

        [Test]
        public void CrouchedMotor_CanExitLowPassageBeforeStanding()
        {
            var world = Ceiling(Fix.FromRatio(3, 2));
            KinematicCharacterMotor.Move(FixVec3.Zero, FixVec3.Right * Fix.FromInt(8), Fix.One,
                Crouched, world, out var position, out _, out _);
            Assert.That(position.X, Is.GreaterThan(Fix.FromInt(5)));
            Assert.That(KinematicCharacterMotor.CanResizeAtFeet(position,
                Crouched, Fix.FromInt(2), world), Is.True);
        }

        [Test]
        public void WithHeight_PreservesOtherRulesAndValidatesDiameter()
        {
            var source = CharacterCollisionConfig.Default;
            Assert.That(Crouched.HeightFixed, Is.EqualTo(Fix.One));
            Assert.That(Crouched.RadiusFixed, Is.EqualTo(source.RadiusFixed));
            Assert.That(Crouched.SkinWidthFixed, Is.EqualTo(source.SkinWidthFixed));
            Assert.That(Crouched.GroundProbeDistanceFixed, Is.EqualTo(source.GroundProbeDistanceFixed));
            Assert.That(Crouched.StepHeightFixed, Is.EqualTo(source.StepHeightFixed));
            Assert.That(Crouched.MinimumGroundNormalYFixed, Is.EqualTo(source.MinimumGroundNormalYFixed));
            Assert.That(Crouched.MaxSlideIterations, Is.EqualTo(source.MaxSlideIterations));
            Assert.Throws<ArgumentOutOfRangeException>(() => source.WithHeight(Fix.One - Fix.Epsilon));
            Assert.Throws<ArgumentOutOfRangeException>(() => KinematicCharacterMotor.CanResizeAtFeet(
                FixVec3.Zero, Crouched, Fix.One - Fix.Epsilon, FixedFlatGroundCollisionWorld.Instance));
            Assert.Throws<ArgumentNullException>(() => KinematicCharacterMotor.CanResizeAtFeet(
                FixVec3.Zero, Crouched, Fix.One, null));
        }
    }
}

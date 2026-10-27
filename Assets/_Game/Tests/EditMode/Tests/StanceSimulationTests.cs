using System;
using AFPS.Core.Collections;
using AFPS.Math;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Prediction;
using AFPS.NetCode.Weapons;
using AFPS.Simulation.Characters;
using AFPS.Simulation.Characters.Collision;
using AFPS.Simulation.Weapons;
using AFPS.Simulation.World;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class StanceSimulationTests
    {
        private static readonly PlayerSimulationConfig Config =
            new PlayerSimulationConfig(Fix.FromInt(6), Fix.FromInt(30), Fix.FromInt(20), Fix.FromInt(8));
        private static readonly PlayerState Grounded = new PlayerState { IsGrounded = true };

        private static PlayerState Step(in PlayerState state, bool crouch = true, bool jump = false,
            bool dash = false, IFixedCharacterCollisionWorld world = null)
        {
            var input = new PlayerInputCommand
            {
                Tick = unchecked(state.Tick + 1), CrouchHeld = crouch, JumpPressed = jump, DashPressed = dash
            };
            input.SetFixedControls(Fix.One, Fix.Zero, Fix.Zero, Fix.Zero);
            return PlayerSimulation.Simulate(state, input, Config, SimulationStep.Step50Hz,
                world ?? FixedFlatGroundCollisionWorld.Instance);
        }

        [Test]
        public void Crouch_UsesShortCapsuleAndHalfWalkSpeed()
        {
            PlayerState state = Grounded;
            for (int i = 0; i < 100; i++)
            {
                state = Step(state);
            }
            Assert.That(state.IsCrouched, Is.True);
            Assert.That(state.IsSprinting, Is.False);
            Assert.That(state.VelocityFixed.X, Is.EqualTo(Fix.FromInt(3)));
            Assert.That(PlayerStanceSimulation.Collision(state, Config).HeightFixed, Is.EqualTo(Config.Stance.Height));
            Assert.That(state.PositionFixed.Y, Is.EqualTo(Fix.Zero));
            Assert.That(Step(state, crouch: false).IsCrouched, Is.False);
        }

        [Test]
        public void ReleasedCrouch_RemainsUnderRoofUntilVolumeIsClear()
        {
            var roof = new FixedWorldBox(1, 0, FixedSurface.Solid, new FixedAabb(
                new FixVec3(Fix.FromInt(-2), Fix.FromRatio(3, 2), Fix.FromInt(-3)),
                new FixVec3(Fix.FromInt(2), Fix.FromInt(4), Fix.FromInt(3))));
            var ground = new FixedWorldPlane(2, 0, FixedSurface.Solid | FixedSurface.Ground, FixedAxis.Y, Fix.Zero);
            var world = new FixedCharacterCollisionWorld(new FixedWorld(new[] { roof }, new[] { ground }));
            PlayerState state = Grounded;
            state.IsCrouched = true;
            state = Step(state, crouch: false, world: world);
            Assert.That(state.IsCrouched, Is.True);
            for (int i = 0; i < 100; i++)
            {
                state = Step(state, crouch: false, world: world);
            }
            Assert.That(state.PositionFixed.X, Is.GreaterThan(Fix.FromRatio(5, 2)));
            Assert.That(state.IsCrouched, Is.False);
        }

        [TestCase(-1, false)]
        [TestCase(0, true)]
        [TestCase(1, true)]
        public void Slide_RequiresExactSpeedThreshold(int offsetRaw, bool starts)
        {
            PlayerState state = Grounded;
            state.VelocityFixed = FixVec3.Right * (Config.Stance.SlideMinimumSpeed + Fix.FromRaw(offsetRaw));
            state = Step(state);
            Assert.That(state.SlideRemainingFixed > Fix.Zero, Is.EqualTo(starts));
            if (starts)
            {
                Assert.That(state.VelocityFixed.X, Is.GreaterThan(Fix.FromInt(9)));
                Assert.That(state.SlideVelocityFixed, Is.EqualTo(state.VelocityFixed));
            }
        }

        [Test]
        public void HeldSlide_ExpiresWithoutRetriggeringAndReleaseCancels()
        {
            PlayerState initial = Grounded;
            initial.VelocityFixed = FixVec3.Right * Fix.FromInt(8);
            PlayerState state = Step(initial);
            Fix first = state.SlideRemainingFixed;
            for (int i = 0; i < 60; i++)
            {
                state = Step(state);
            }
            Assert.That(first, Is.GreaterThan(Fix.Zero));
            Assert.That(state.SlideRemainingFixed, Is.EqualTo(Fix.Zero));
            Assert.That(state.SlideVelocityFixed, Is.EqualTo(FixVec3.Zero));
            state = Step(Step(initial), crouch: false);
            Assert.That(state.IsCrouched, Is.False);
            Assert.That(state.SlideRemainingFixed, Is.EqualTo(Fix.Zero));
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        public void JumpOrDash_CancelsSlide(bool jump, bool dash)
        {
            PlayerState state = Grounded;
            state.VelocityFixed = FixVec3.Right * Fix.FromInt(8);
            state = Step(state);
            state = Step(state, jump: jump, dash: dash);
            Assert.That(state.SlideRemainingFixed, Is.EqualTo(Fix.Zero));
            Assert.That(state.SlideVelocityFixed, Is.EqualTo(FixVec3.Zero));
            Assert.That(jump ? state.VelocityFixed.Y > Fix.Zero : state.DashRemainingFixed > Fix.Zero, Is.True);
        }

        [Test]
        public void AirborneCrouch_DoesNotStartSlide()
        {
            var state = Grounded;
            state.IsGrounded = false;
            state.PositionFixed = FixVec3.Up * Fix.FromInt(3);
            state.VelocityFixed = FixVec3.Right * Fix.FromInt(9);
            Assert.That(Step(state).SlideRemainingFixed, Is.EqualTo(Fix.Zero));
        }

        [Test]
        public void SlideSteering_PreservesMomentumAndCannotTurnInstantly()
        {
            var state = Grounded;
            state.VelocityFixed = FixVec3.Right * Fix.FromInt(8);
            state = Step(state);
            Fix previousSpeed = state.VelocityFixed.Magnitude;
            var input = new PlayerInputCommand { Tick = 2, MoveY = 1f, CrouchHeld = true };
            state = PlayerSimulation.Simulate(state, input, Config,
                SimulationStep.Step50Hz, FixedFlatGroundCollisionWorld.Instance);
            Assert.That(state.VelocityFixed.X, Is.GreaterThan(Fix.FromInt(9)));
            Assert.That(state.VelocityFixed.Z, Is.GreaterThan(Fix.Zero));
            Assert.That(state.VelocityFixed.Magnitude, Is.LessThan(previousSpeed));
        }

        [Test]
        public void SlidingIntoWall_StopsAndClearsSlideWithoutPenetration()
        {
            var wall = new FixedWorldBox(1, 0, FixedSurface.Solid, new FixedAabb(
                new FixVec3(Fix.FromInt(2), Fix.Zero, Fix.FromInt(-4)),
                new FixVec3(Fix.FromInt(3), Fix.FromInt(4), Fix.FromInt(4))));
            var ground = new FixedWorldPlane(2, 0, FixedSurface.Solid | FixedSurface.Ground, FixedAxis.Y, Fix.Zero);
            var world = new FixedCharacterCollisionWorld(new FixedWorld(new[] { wall }, new[] { ground }));
            var state = Grounded;
            state.PositionFixed = FixVec3.Right * Fix.FromRatio(3, 2);
            state.VelocityFixed = FixVec3.Right * Fix.FromInt(8);
            state = Step(state, world: world);
            Assert.That(state.PositionFixed.X, Is.EqualTo(Fix.FromRatio(3, 2)));
            Assert.That(state.VelocityFixed, Is.EqualTo(FixVec3.Zero));
            Assert.That(state.SlideRemainingFixed, Is.EqualTo(Fix.Zero));
            Assert.That(state.SlideVelocityFixed, Is.EqualTo(FixVec3.Zero));
        }

        [Test]
        public void Input_RoundtripsCrouchAndOnlyShortFallbackKeepsIt()
        {
            var input = new PlayerInputCommand { Tick = uint.MaxValue, CrouchHeld = true };
            var bytes = new byte[InputCommandBatchCodec.GetPacketSize(1)];
            Assert.That(InputCommandBatchCodec.TrySerialize(new InputCommandBatch(
                new ArraySegment<PlayerInputCommand>(new[] { input })), 1, new ArraySegment<byte>(bytes), out _), Is.True);
            var output = new PlayerInputCommand[1];
            Assert.That(InputCommandBatchCodec.TryDeserialize(new ArraySegment<byte>(bytes), output, 0, out _, out _), Is.True);
            Assert.That(output[0].CrouchHeld, Is.True);
            Assert.That(output[0].CreateFallback(0, true).CrouchHeld, Is.True);
            Assert.That(output[0].CreateFallback(0, false).CrouchHeld, Is.False);
        }

        [Test]
        public void Authority_RoundtripsSlideAndRejectsContradictions()
        {
            PlayerState state = Grounded;
            state.VelocityFixed = FixVec3.Right * Fix.FromInt(8);
            state = Step(state);
            var bytes = new byte[AuthoritativePlayerStateCodec.PacketSize];
            Assert.That(AuthoritativePlayerStateCodec.TrySerialize(new AuthoritativePlayerState(10, state.Tick, state),
                1, new ArraySegment<byte>(bytes), out _), Is.True);
            Assert.That(AuthoritativePlayerStateCodec.TryDeserialize(new ArraySegment<byte>(bytes), out _, out var decoded), Is.True);
            Assert.That(decoded.State.RawEquals(state), Is.True);
            state.IsCrouched = false;
            Assert.That(AuthoritativePlayerStateCodec.TrySerialize(new AuthoritativePlayerState(10, state.Tick, state),
                1, new ArraySegment<byte>(bytes), out _), Is.False);
            state.IsCrouched = true;
            state.SlideVelocityFixed += FixVec3.Up;
            Assert.That(AuthoritativePlayerStateCodec.TrySerialize(new AuthoritativePlayerState(10, state.Tick, state),
                1, new ArraySegment<byte>(bytes), out _), Is.False);
            bytes[110] = 128;
            Assert.That(AuthoritativePlayerStateCodec.TryDeserialize(new ArraySegment<byte>(bytes), out _, out _), Is.False);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void StanceOnlyDrift_TriggersRollbackAcrossWrap(int field)
        {
            var authority = Grounded;
            authority.Tick = uint.MaxValue;
            var predicted = authority;
            if (field == 0)
            {
                predicted.IsCrouched = true;
            }
            else if (field == 1)
            {
                predicted.SlideRemainingFixed = Fix.Epsilon;
            }
            else
            {
                predicted.SlideVelocityFixed = FixVec3.Right;
            }
            Assert.That(PlayerStateHash.Compute(predicted), Is.Not.EqualTo(PlayerStateHash.Compute(authority)));
            var states = new TickBuffer<PlayerState>(8);
            var inputs = new TickBuffer<PlayerInputCommand>(8);
            states.Store(authority.Tick, predicted);
            var input = new PlayerInputCommand { Tick = 0, CrouchHeld = true };
            inputs.Store(0, input);
            var current = PlayerSimulation.Simulate(predicted, input, Config,
                SimulationStep.Step50Hz, FixedFlatGroundCollisionWorld.Instance);
            var expected = PlayerSimulation.Simulate(authority, input, Config,
                SimulationStep.Step50Hz, FixedFlatGroundCollisionWorld.Instance);
            var result = ClientPredictionReconciler.Reconcile(new AuthoritativePlayerState(10, authority.Tick, authority),
                0, current, inputs, states, Config, SimulationStep.Step50Hz, FixedFlatGroundCollisionWorld.Instance);
            Assert.That(result.Status, Is.EqualTo(ReconciliationStatus.Corrected));
            Assert.That(result.State.RawEquals(expected), Is.True);
        }

        [Test]
        public void CrouchedFire_UsesInjectedEyeHeight()
        {
            var weapon = WeaponSimulationConfig.DefaultRifle;
            var controller = new ServerWeaponFireController(weapon, Fix.One);
            var state = Grounded;
            state.IsCrouched = true;
            var fire = new PlayerInputCommand { FirePressed = true, ShotSequence = 1 };
            var result = controller.Process(1, 1, fire, state);
            Assert.That(result.Accepted, Is.True);
            Assert.That(result.Shot.OriginFixed.Y, Is.EqualTo(Fix.One));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        public void EveryStanceRule_ChangesWorldFingerprint(int field)
        {
            var stance = Config.Stance;
            var changed = new PlayerStanceConfig(
                stance.Height + (field == 0 ? Fix.Epsilon : Fix.Zero),
                stance.SpeedMultiplier + (field == 1 ? Fix.Epsilon : Fix.Zero),
                stance.SlideSpeed + (field == 2 ? Fix.Epsilon : Fix.Zero),
                stance.SlideMinimumSpeed + (field == 3 ? Fix.Epsilon : Fix.Zero),
                stance.SlideDuration + (field == 4 ? Fix.Epsilon : Fix.Zero),
                stance.SlideDeceleration + (field == 5 ? Fix.Epsilon : Fix.Zero),
                stance.SlideSteer + (field == 6 ? Fix.Epsilon : Fix.Zero));
            var config = new PlayerSimulationConfig(Fix.FromInt(6), Fix.FromInt(30), Fix.FromInt(20), Fix.FromInt(8), stance: changed);
            var world = new FixedWorld(null);
            Assert.That(new SimulationWorldContext(world, config, SimulationStep.Step50Hz).Identity.ConfigurationHash,
                Is.Not.EqualTo(new SimulationWorldContext(world, Config, SimulationStep.Step50Hz).Identity.ConfigurationHash));
        }

        [Test]
        public void InvalidOrDefaultStance_AndOutOfCapsuleHeightAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerSimulationConfig(
                Fix.FromInt(6), Fix.FromInt(30), Fix.FromInt(20), Fix.FromInt(8), stance: default(PlayerStanceConfig)));
            var stance = PlayerStanceConfig.Default;
            var tooLow = new PlayerStanceConfig(Fix.Half, stance.SpeedMultiplier, stance.SlideSpeed,
                stance.SlideMinimumSpeed, stance.SlideDuration, stance.SlideDeceleration, stance.SlideSteer);
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerSimulationConfig(
                Fix.FromInt(6), Fix.FromInt(30), Fix.FromInt(20), Fix.FromInt(8), stance: tooLow));
            var tooTall = new PlayerStanceConfig(Fix.FromInt(3), stance.SpeedMultiplier, stance.SlideSpeed,
                stance.SlideMinimumSpeed, stance.SlideDuration, stance.SlideDeceleration, stance.SlideSteer);
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerSimulationConfig(
                Fix.FromInt(6), Fix.FromInt(30), Fix.FromInt(20), Fix.FromInt(8), stance: tooTall));
        }

        [Test]
        public void StanceReplay_MatchesGolden()
        {
            Assert.That(DeterministicStanceScenario.Run(), Is.EqualTo(DeterministicStanceScenario.ExpectedHash));
        }
    }
}

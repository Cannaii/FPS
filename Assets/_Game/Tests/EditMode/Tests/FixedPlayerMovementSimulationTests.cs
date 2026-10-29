using System;
using AFPS.Math;
using AFPS.Core.Collections;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Prediction;
using AFPS.NetCode.Protocol;
using AFPS.Simulation.Characters;
using AFPS.Simulation.Characters.Collision;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class FixedPlayerMovementSimulationTests
    {
        private static PlayerMovementConfig Config => new PlayerMovementConfig(Fix.FromInt(6), Fix.FromInt(20), Fix.FromInt(20), Fix.FromInt(8));
        private static PlayerMovementState Grounded => new PlayerMovementState
        {
            IsGrounded = true
        };

        private static PlayerMovementState Step(PlayerMovementState state, PlayerInputCommand command) => PlayerMovementSimulation.Simulate(state, command, Config, SimulationStep.Step50Hz, FixedFlatGroundCollisionWorld.Instance);
        [Test]
        public void Forward100Ticks_MatchesIndependentIntegerGolden()
        {
            PlayerMovementState state = Grounded;
            for (uint tick = 1; tick <= 100; tick++)
            {
                var input = new PlayerInputCommand
                {
                    Tick = tick
                };
                input.SetFixedControls(Fix.Zero, Fix.One, Fix.Zero, Fix.Zero);
                state = Step(state, input);
            }

            Assert.That(state.PositionFixed.Z.Raw, Is.EqualTo(731552));
            Assert.That(state.VelocityFixed.Z.Raw, Is.EqualTo(393216));
            Assert.That(state.PositionFixed.Y.Raw, Is.Zero);
            Assert.That(state.IsGrounded, Is.True);
            Assert.That(PlayerMovementStateHash.Compute(state), Is.EqualTo(4219067840041231712UL));
        }

        [Test]
        public void Jump_UsesIntegerGravityAndReturnsToGround()
        {
            PlayerMovementState state = Step(Grounded, new PlayerInputCommand { Tick = 1, JumpPressed = true });
            Assert.That(state.VelocityFixed.Y.Raw, Is.EqualTo(498068));
            Assert.That(state.PositionFixed.Y.Raw, Is.EqualTo(9963));
            Assert.That(state.IsGrounded, Is.False);
            for (uint tick = 2; tick <= 100; tick++)
            {
                state = Step(state, new PlayerInputCommand { Tick = tick });
            }

            Assert.That(state.PositionFixed.Y.Raw, Is.Zero);
            Assert.That(state.VelocityFixed.Y.Raw, Is.Zero);
            Assert.That(state.IsGrounded, Is.True);
        }

        [Test]
        public void QuarterTurn_UsesLutToRotateForward()
        {
            var input = new PlayerInputCommand
            {
                Tick = 1
            };
            input.SetFixedControls(Fix.Zero, Fix.One, Fix.FromInt(90), Fix.Zero);
            PlayerMovementState state = Step(Grounded, input);
            Assert.That(state.VelocityFixed.X.Raw, Is.EqualTo(26220));
            Assert.That(System.Math.Abs(state.VelocityFixed.Z.Raw), Is.LessThanOrEqualTo(2));
        }

        [Test]
        public void DiagonalMovement_IsClampedBeforeAcceleration()
        {
            PlayerMovementState state = Grounded;
            for (uint tick = 1; tick <= 100; tick++)
            {
                var input = new PlayerInputCommand
                {
                    Tick = tick
                };
                input.SetFixedControls(Fix.One, Fix.One, Fix.Zero, Fix.Zero);
                state = Step(state, input);
            }

            Assert.That(state.VelocityFixed.Magnitude.Raw, Is.InRange(393200, 393230));
        }

        private sealed class SlopeWorld : IFixedCharacterCollisionWorld
        {
            private static readonly FixVec3 Normal = new FixVec3(Fix.FromRaw(-29309), Fix.FromRaw(58617), Fix.Zero);
            public bool CapsuleCast(
                FixVec3 position,
                FixVec3 direction,
                Fix distance,
                in CharacterCollisionConfig config,
                out FixedCharacterCollisionHit hit)
            {
                // 使用精确宽整数点积，捕获投影舍入留下的向内残差。
                long approach = (long)direction.X.Raw * Normal.X.Raw + (long)direction.Y.Raw * Normal.Y.Raw;
                hit = new FixedCharacterCollisionHit(Fix.Zero, Normal);
                return approach < 0;
            }
        }

        [Test]
        public void SlopeProjection_DoesNotGetStuckInZeroDistanceContacts()
        {
            KinematicCharacterMotor.Move(FixVec3.Zero, FixVec3.Right, Fix.One, CharacterCollisionConfig.Default, new SlopeWorld(), out var position, out _, out var grounded);
            Assert.That(position.X.Raw, Is.InRange(52420, 52440));
            Assert.That(position.Y.Raw, Is.InRange(26210, 26230));
            Assert.That(grounded, Is.True);
        }

        [Test]
        public void Core_RequiresPositiveStepAndExplicitWorld()
        {
            Assert.Throws<ArgumentNullException>(() => PlayerMovementSimulation.Simulate(Grounded, default, Config, SimulationStep.Step50Hz, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => PlayerMovementSimulation.Simulate(Grounded, default, Config, Fix.Zero, FixedFlatGroundCollisionWorld.Instance));
        }

        [Test]
        public void AuthorityRaw_RoundTripsExtremeRawCoordinatesWithoutLoss()
        {
            PlayerMovementState state = new PlayerMovementState
            {
                Tick = uint.MaxValue,
                PositionFixed = new FixVec3(Fix.FromRaw(int.MinValue), Fix.FromRaw(int.MaxValue), Fix.FromRaw(-1)),
                VelocityFixed = new FixVec3(Fix.FromInt(400), Fix.FromRaw(1), Fix.FromRaw(-1000000)),
                YawFixedDegrees = InputAngleGrid.YawToFix(35999),
                PitchFixedDegrees = InputAngleGrid.PitchToFix(-8990),
                IsGrounded = true
            };
            byte[] packet = new byte[AuthoritativeMovementStateCodec.PacketSize];
            Assert.That(AuthoritativeMovementStateCodec.PacketSize, Is.EqualTo(152));
            Assert.That(PacketHeader.CurrentProtocolVersion, Is.EqualTo(28));
            Assert.That(AuthoritativeMovementStateCodec.TrySerialize(new AuthoritativeMovementState(0, state.Tick, state), 1, new ArraySegment<byte>(packet), out _), Is.True);
            Assert.That(AuthoritativeMovementStateCodec.TryDeserialize(new ArraySegment<byte>(packet), out _, out AuthoritativeMovementState decoded), Is.True);
            Assert.That(decoded.State.RawEquals(state), Is.True);
            packet[4] = 2;
            Assert.That(AuthoritativeMovementStateCodec.TryDeserialize(new ArraySegment<byte>(packet), out _, out _), Is.False);
        }

        [Test]
        public void AuthorityRaw_RejectsNonCanonicalAnglesAndFlags()
        {
            var state = new PlayerMovementState
            {
                Tick = 1,
                YawFixedDegrees = Fix.Epsilon
            };
            byte[] packet = new byte[AuthoritativeMovementStateCodec.PacketSize];
            Assert.That(AuthoritativeMovementStateCodec.TrySerialize(new AuthoritativeMovementState(2, 1, state), 1, new ArraySegment<byte>(packet), out _), Is.False);
            state.YawFixedDegrees = Fix.Zero;
            Assert.That(AuthoritativeMovementStateCodec.TrySerialize(new AuthoritativeMovementState(2, 1, state), 1, new ArraySegment<byte>(packet), out _), Is.True);
            packet[48] = 128;
            Assert.That(AuthoritativeMovementStateCodec.TryDeserialize(new ArraySegment<byte>(packet), out _, out _), Is.False);
        }

        [Test]
        public void OneRawDifference_AlwaysTriggersCorrection()
        {
            var predicted = Grounded;
            predicted.Tick = 5;
            var authority = predicted;
            authority.PositionFixed = new FixVec3(Fix.Epsilon, Fix.Zero, Fix.Zero);
            var states = new TickBuffer<PlayerMovementState>(8);
            states.Store(5, predicted);
            var result = MovementPredictionReconciler.Reconcile(
                new AuthoritativeMovementState(7, 5, authority),
                5,
                predicted,
                new TickBuffer<PlayerInputCommand>(8),
                states,
                Config,
                SimulationStep.Step50Hz,
                FixedFlatGroundCollisionWorld.Instance);
            Assert.That(result.Status, Is.EqualTo(MovementReconciliationStatus.Corrected));
            Assert.That(result.State.RawEquals(authority), Is.True);
            Assert.That(result.Error.RawMismatch, Is.True);
        }

        [Test]
        public void MissingInput_DoesNotPartiallyRewriteHistory()
        {
            var predicted = Grounded;
            predicted.Tick = 5;
            var authority = predicted;
            authority.PositionFixed = new FixVec3(Fix.One, Fix.Zero, Fix.Zero);
            var states = new TickBuffer<PlayerMovementState>(8);
            states.Store(5, predicted);
            var current = predicted;
            current.Tick = 7;
            var inputs = new TickBuffer<PlayerInputCommand>(8);
            inputs.Store(6, new PlayerInputCommand { Tick = 6 });
            var result = MovementPredictionReconciler.Reconcile(new AuthoritativeMovementState(7, 5, authority), 7, current, inputs, states, Config, SimulationStep.Step50Hz, FixedFlatGroundCollisionWorld.Instance);
            Assert.That(result.Status, Is.EqualTo(MovementReconciliationStatus.MissingInputHistory));
            states.TryGet(5, out var unchanged);
            Assert.That(unchanged.RawEquals(predicted), Is.True);
        }

        [Test]
        public void RollbackReplay_CrossesTickWrapExactly()
        {
            var baseline = Grounded;
            baseline.Tick = uint.MaxValue - 1;
            var predicted = baseline;
            predicted.PositionFixed = new FixVec3(Fix.One, Fix.Zero, Fix.Zero);
            var states = new TickBuffer<PlayerMovementState>(8);
            states.Store(baseline.Tick, predicted);
            var inputs = new TickBuffer<PlayerInputCommand>(8);
            var expected = baseline;
            foreach (uint tick in new[]
            {
                uint.MaxValue,
                0u,
                1u
            }

            )
            {
                var input = new PlayerInputCommand
                {
                    Tick = tick
                };
                input.SetFixedControls(Fix.Zero, Fix.One, Fix.Zero, Fix.Zero);
                inputs.Store(tick, input);
                expected = Step(expected, input);
                predicted = Step(predicted, input);
                states.Store(tick, predicted);
            }

            var result = MovementPredictionReconciler.Reconcile(
                new AuthoritativeMovementState(9, baseline.Tick, baseline),
                1,
                predicted,
                inputs,
                states,
                Config,
                SimulationStep.Step50Hz,
                FixedFlatGroundCollisionWorld.Instance);
            Assert.That(result.Status, Is.EqualTo(MovementReconciliationStatus.Corrected));
            Assert.That(result.ReplayedTickCount, Is.EqualTo(3));
            Assert.That(result.State.RawEquals(expected), Is.True);
        }

        [Test]
        public void MatchingRawState_DoesNotReplay()
        {
            var state = Grounded;
            state.Tick = 3;
            var states = new TickBuffer<PlayerMovementState>(8);
            states.Store(3, state);
            var result = MovementPredictionReconciler.Reconcile(
                new AuthoritativeMovementState(5, 3, state),
                3,
                state,
                new TickBuffer<PlayerInputCommand>(8),
                states,
                Config,
                SimulationStep.Step50Hz,
                FixedFlatGroundCollisionWorld.Instance);
            Assert.That(result.Status, Is.EqualTo(MovementReconciliationStatus.NoCorrection));
            Assert.That(result.Error.RawMismatch, Is.False);
        }
    }
}

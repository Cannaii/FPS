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
    public sealed class FixedPlayerSimulationTests
    {
        private static PlayerSimulationConfig Config => new PlayerSimulationConfig(Fix.FromInt(6), Fix.FromInt(20), Fix.FromInt(20), Fix.FromInt(8));
        private static PlayerState Grounded => new PlayerState
        {
            IsGrounded = true
        };

        private static PlayerState Step(PlayerState state, PlayerInputCommand command) => PlayerSimulation.Simulate(state, command, Config, SimulationStep.Step50Hz, FixedFlatGroundCollisionWorld.Instance);
        [Test]
        public void Forward100Ticks_MatchesIndependentIntegerGolden()
        {
            PlayerState state = Grounded;
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
            Assert.That(PlayerStateHash.Compute(state), Is.EqualTo(13392039858859600634UL));
        }

        [Test]
        public void Jump_UsesIntegerGravityAndReturnsToGround()
        {
            PlayerState state = Step(Grounded, new PlayerInputCommand { Tick = 1, JumpPressed = true });
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
            PlayerState state = Step(Grounded, input);
            Assert.That(state.VelocityFixed.X.Raw, Is.EqualTo(26220));
            Assert.That(System.Math.Abs(state.VelocityFixed.Z.Raw), Is.LessThanOrEqualTo(2));
        }

        [Test]
        public void DiagonalMovement_IsClampedBeforeAcceleration()
        {
            PlayerState state = Grounded;
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
            Assert.Throws<ArgumentNullException>(() => PlayerSimulation.Simulate(Grounded, default, Config, SimulationStep.Step50Hz, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => PlayerSimulation.Simulate(Grounded, default, Config, Fix.Zero, FixedFlatGroundCollisionWorld.Instance));
        }

        [Test]
        public void AuthorityRaw_RoundTripsExtremeRawCoordinatesWithoutLoss()
        {
            PlayerState state = new PlayerState
            {
                Tick = uint.MaxValue,
                PositionFixed = new FixVec3(Fix.FromRaw(int.MinValue), Fix.FromRaw(int.MaxValue), Fix.FromRaw(-1)),
                VelocityFixed = new FixVec3(Fix.FromInt(400), Fix.FromRaw(1), Fix.FromRaw(-1000000)),
                YawFixedDegrees = InputAngleGrid.YawToFix(35999),
                PitchFixedDegrees = InputAngleGrid.PitchToFix(-8990),
                IsGrounded = true
            };
            byte[] packet = new byte[AuthoritativePlayerStateCodec.PacketSize];
            Assert.That(AuthoritativePlayerStateCodec.PacketSize, Is.EqualTo(70));
            Assert.That(PacketHeader.CurrentProtocolVersion, Is.EqualTo(14));
            Assert.That(AuthoritativePlayerStateCodec.TrySerialize(new AuthoritativePlayerState(0, state.Tick, state), 1, new ArraySegment<byte>(packet), out _), Is.True);
            Assert.That(AuthoritativePlayerStateCodec.TryDeserialize(new ArraySegment<byte>(packet), out _, out AuthoritativePlayerState decoded), Is.True);
            Assert.That(decoded.State.RawEquals(state), Is.True);
            packet[4] = 2;
            Assert.That(AuthoritativePlayerStateCodec.TryDeserialize(new ArraySegment<byte>(packet), out _, out _), Is.False);
        }

        [Test]
        public void AuthorityRaw_RejectsNonCanonicalAnglesAndFlags()
        {
            var state = new PlayerState
            {
                Tick = 1,
                YawFixedDegrees = Fix.Epsilon
            };
            byte[] packet = new byte[AuthoritativePlayerStateCodec.PacketSize];
            Assert.That(AuthoritativePlayerStateCodec.TrySerialize(new AuthoritativePlayerState(2, 1, state), 1, new ArraySegment<byte>(packet), out _), Is.False);
            state.YawFixedDegrees = Fix.Zero;
            Assert.That(AuthoritativePlayerStateCodec.TrySerialize(new AuthoritativePlayerState(2, 1, state), 1, new ArraySegment<byte>(packet), out _), Is.True);
            packet[48] = 8;
            Assert.That(AuthoritativePlayerStateCodec.TryDeserialize(new ArraySegment<byte>(packet), out _, out _), Is.False);
        }

        [Test]
        public void OneRawDifference_AlwaysTriggersCorrection()
        {
            var predicted = Grounded;
            predicted.Tick = 5;
            var authority = predicted;
            authority.PositionFixed = new FixVec3(Fix.Epsilon, Fix.Zero, Fix.Zero);
            var states = new TickBuffer<PlayerState>(8);
            states.Store(5, predicted);
            var result = ClientPredictionReconciler.Reconcile(
                new AuthoritativePlayerState(7, 5, authority),
                5,
                predicted,
                new TickBuffer<PlayerInputCommand>(8),
                states,
                Config,
                SimulationStep.Step50Hz,
                FixedFlatGroundCollisionWorld.Instance);
            Assert.That(result.Status, Is.EqualTo(ReconciliationStatus.Corrected));
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
            var states = new TickBuffer<PlayerState>(8);
            states.Store(5, predicted);
            var current = predicted;
            current.Tick = 7;
            var inputs = new TickBuffer<PlayerInputCommand>(8);
            inputs.Store(6, new PlayerInputCommand { Tick = 6 });
            var result = ClientPredictionReconciler.Reconcile(new AuthoritativePlayerState(7, 5, authority), 7, current, inputs, states, Config, SimulationStep.Step50Hz, FixedFlatGroundCollisionWorld.Instance);
            Assert.That(result.Status, Is.EqualTo(ReconciliationStatus.MissingInputHistory));
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
            var states = new TickBuffer<PlayerState>(8);
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

            var result = ClientPredictionReconciler.Reconcile(
                new AuthoritativePlayerState(9, baseline.Tick, baseline),
                1,
                predicted,
                inputs,
                states,
                Config,
                SimulationStep.Step50Hz,
                FixedFlatGroundCollisionWorld.Instance);
            Assert.That(result.Status, Is.EqualTo(ReconciliationStatus.Corrected));
            Assert.That(result.ReplayedTickCount, Is.EqualTo(3));
            Assert.That(result.State.RawEquals(expected), Is.True);
        }

        [Test]
        public void MatchingRawState_DoesNotReplay()
        {
            var state = Grounded;
            state.Tick = 3;
            var states = new TickBuffer<PlayerState>(8);
            states.Store(3, state);
            var result = ClientPredictionReconciler.Reconcile(
                new AuthoritativePlayerState(5, 3, state),
                3,
                state,
                new TickBuffer<PlayerInputCommand>(8),
                states,
                Config,
                SimulationStep.Step50Hz,
                FixedFlatGroundCollisionWorld.Instance);
            Assert.That(result.Status, Is.EqualTo(ReconciliationStatus.NoCorrection));
            Assert.That(result.Error.RawMismatch, Is.False);
        }
    }
}

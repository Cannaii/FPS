using System;
using AFPS.Core.Collections;
using AFPS.Math;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Prediction;
using AFPS.Simulation.Characters;
using AFPS.Simulation.Characters.Collision;
using AFPS.Simulation.World;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class SprintSimulationTests
    {
        [TestCase(true)]
        [TestCase(false)]
        public void LargeStaminaChanges_SaturateBeforeIntegerConversion(bool sprinting)
        {
            var previous = new PlayerState { IsGrounded = true, StaminaSpentFixed = Fix.Half };
            var next = previous;
            next.PositionFixed = FixVec3.Forward;
            var config = new PlayerSprintConfig(Fix.One, Fix.MaxValue, Fix.MaxValue, Fix.Half);
            next = PlayerSprintSimulation.Complete(next, previous, Input(1, sprinting), config, Fix.One, sprinting);
            Assert.That(next.StaminaSpentFixed, Is.EqualTo(sprinting ? Fix.One : Fix.Zero));
        }

        [Test]
        public void CapabilityReplay_MatchesGoldenHash()
        {
            Assert.That(DeterministicSprintScenario.Run(), Is.EqualTo(DeterministicSprintScenario.ExpectedHash));
        }

        [Test]
        public void BlockedMovement_DoesNotConsumeStamina()
        {
            var previous = new PlayerState { IsGrounded = true, StaminaSpentFixed = Fix.Half };
            var next = PlayerSprintSimulation.Complete(previous, previous, Input(1),
                PlayerSprintConfig.Default, SimulationStep.Step50Hz, true);
            Assert.That(next.IsSprinting, Is.False);
            Assert.That(next.StaminaSpentFixed.Raw, Is.EqualTo(Fix.Half.Raw - 262));
        }

        [Test]
        public void ReleaseAtExactRecoveryThreshold_ClearsExhaustion()
        {
            var state = new PlayerState
            {
                IsGrounded = true, SprintExhausted = true,
                StaminaSpentFixed = Fix.One - PlayerSprintConfig.Default.RestartStamina + Fix.FromRaw(262)
            };
            state = Step(state, Input(1, false));
            Assert.That(state.SprintExhausted, Is.False);
            Assert.That(Fix.One - state.StaminaSpentFixed, Is.EqualTo(PlayerSprintConfig.Default.RestartStamina));
        }

        private static PlayerSimulationConfig Config => new PlayerSimulationConfig(
            Fix.FromInt(6), Fix.FromInt(20), Fix.FromInt(20), Fix.FromInt(8));

        private static PlayerInputCommand Input(uint tick, bool held = true)
        {
            var input = new PlayerInputCommand { Tick = tick, SprintHeld = held };
            input.SetFixedControls(Fix.Zero, Fix.One, Fix.Zero, Fix.Zero);
            return input;
        }

        private static PlayerState Step(PlayerState state, PlayerInputCommand input) =>
            PlayerSimulation.Simulate(state, input, Config, SimulationStep.Step50Hz,
                FixedFlatGroundCollisionWorld.Instance);

        [Test]
        public void Sprint_UsesSharedSpeedAndConsumesExactRawStamina()
        {
            var state = new PlayerState { IsGrounded = true };
            for (uint tick = 1; tick <= 50; tick++)
            {
                state = Step(state, Input(tick));
            }
            Assert.That(state.VelocityFixed.Z, Is.EqualTo(Fix.FromInt(9)));
            Assert.That(state.StaminaSpentFixed.Raw, Is.EqualTo(50 * 328));
            Assert.That(state.IsSprinting, Is.True);
        }

        [Test]
        public void Exhaustion_RequiresReleaseAndRecoveryBeforeRestart()
        {
            var state = new PlayerState { IsGrounded = true, StaminaSpentFixed = Fix.One - Fix.Epsilon };
            state = Step(state, Input(1));
            Assert.That(state.StaminaSpentFixed, Is.EqualTo(Fix.One));
            Assert.That(state.SprintExhausted, Is.True);
            for (uint tick = 2; tick <= 300; tick++)
            {
                state = Step(state, Input(tick));
            }
            Assert.That(state.StaminaSpentFixed, Is.EqualTo(Fix.Zero));
            Assert.That(state.IsSprinting, Is.False);
            Assert.That(state.SprintExhausted, Is.True, "一直按住不能在体力恢复后自动反复启动。");
            state = Step(state, Input(301, false));
            Assert.That(state.SprintExhausted, Is.False);
            state = Step(state, Input(302));
            Assert.That(state.IsSprinting, Is.True);
        }

        [Test]
        public void Exhaustion_DoesNotClearBeforeRestartThreshold()
        {
            var state = new PlayerState
            {
                IsGrounded = true, StaminaSpentFixed = Fix.One, SprintExhausted = true
            };
            state = Step(state, Input(1, false));
            Assert.That(state.SprintExhausted, Is.True);
            state = Step(state, Input(2));
            Assert.That(state.IsSprinting, Is.False);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void StandingBackwardsAndAirborne_DoNotDrain(int mode)
        {
            var state = new PlayerState { IsGrounded = mode != 2, StaminaSpentFixed = Fix.FromRatio(1, 2) };
            if (mode == 2)
            {
                state.PositionFixed = new FixVec3(Fix.Zero, Fix.FromInt(10), Fix.Zero);
            }
            var input = Input(1);
            input.SetFixedControls(Fix.Zero, mode == 0 ? Fix.Zero : -Fix.One, Fix.Zero, Fix.Zero);
            state = Step(state, input);
            Assert.That(state.IsSprinting, Is.False);
            Assert.That(state.StaminaSpentFixed.Raw, Is.EqualTo(32768 - 262));
        }

        [Test]
        public void HeldSprint_RespectsFallbackWindowAndInputRoundtrip()
        {
            var input = Input(uint.MaxValue);
            var bytes = new byte[InputCommandBatchCodec.GetPacketSize(1)];
            Assert.That(InputCommandBatchCodec.TrySerialize(new InputCommandBatch(
                new ArraySegment<PlayerInputCommand>(new[] { input })), 1,
                new ArraySegment<byte>(bytes), out _), Is.True);
            var decoded = new PlayerInputCommand[1];
            Assert.That(InputCommandBatchCodec.TryDeserialize(new ArraySegment<byte>(bytes), decoded,
                0, out _, out _), Is.True);
            Assert.That(decoded[0].SprintHeld, Is.True);
            Assert.That(decoded[0].CreateFallback(0, true).SprintHeld, Is.True);
            Assert.That(decoded[0].CreateFallback(0, false).SprintHeld, Is.False);
        }

        [Test]
        public void Authority_TransportsStaminaAndRejectsInvalidStates()
        {
            var state = new PlayerState { Tick = 1, StaminaSpentFixed = Fix.FromRaw(12345), SprintExhausted = true };
            var bytes = new byte[AuthoritativePlayerStateCodec.PacketSize];
            Assert.That(AuthoritativePlayerStateCodec.TrySerialize(new AuthoritativePlayerState(2, 1, state),
                1, new ArraySegment<byte>(bytes), out int count), Is.True);
            Assert.That(count, Is.EqualTo(115));
            Assert.That(AuthoritativePlayerStateCodec.TryDeserialize(new ArraySegment<byte>(bytes),
                out _, out var decoded), Is.True);
            Assert.That(decoded.State.RawEquals(state), Is.True);
            state.IsSprinting = true;
            Assert.That(AuthoritativePlayerStateCodec.TrySerialize(new AuthoritativePlayerState(2, 1, state),
                1, new ArraySegment<byte>(bytes), out _), Is.False);
            bytes[48] = 6;
            Assert.That(AuthoritativePlayerStateCodec.TryDeserialize(new ArraySegment<byte>(bytes), out _, out _), Is.False);
            bytes[48] = 0;
            bytes[52] = 1;
            Assert.That(AuthoritativePlayerStateCodec.TryDeserialize(new ArraySegment<byte>(bytes), out _, out _), Is.False);
        }

        [Test]
        public void StaminaOnlyError_TriggersRollbackAcrossTickWrap()
        {
            var authority = new PlayerState { Tick = uint.MaxValue, IsGrounded = true };
            var predicted = authority;
            predicted.StaminaSpentFixed = Fix.Epsilon;
            Assert.That(PlayerStateHash.Compute(authority), Is.Not.EqualTo(PlayerStateHash.Compute(predicted)));
            var states = new TickBuffer<PlayerState>(8);
            states.Store(authority.Tick, predicted);
            var inputs = new TickBuffer<PlayerInputCommand>(8);
            inputs.Store(0, Input(0));
            inputs.Store(1, Input(1));
            PlayerState current = Step(Step(predicted, Input(0)), Input(1));
            var result = ClientPredictionReconciler.Reconcile(new AuthoritativePlayerState(100, authority.Tick, authority),
                1, current, inputs, states, Config, SimulationStep.Step50Hz, FixedFlatGroundCollisionWorld.Instance);
            Assert.That(result.Status, Is.EqualTo(ReconciliationStatus.Corrected));
            Assert.That(result.State.RawEquals(Step(Step(authority, Input(0)), Input(1))), Is.True);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void SprintRuleChanges_ChangeWorldFingerprint(int field)
        {
            var original = PlayerSprintConfig.Default;
            var changed = new PlayerSprintConfig(
                original.SpeedMultiplier + (field == 0 ? Fix.Epsilon : Fix.Zero),
                original.DrainPerSecond + (field == 1 ? Fix.Epsilon : Fix.Zero),
                original.RecoveryPerSecond + (field == 2 ? Fix.Epsilon : Fix.Zero),
                original.RestartStamina + (field == 3 ? Fix.Epsilon : Fix.Zero));
            var config = new PlayerSimulationConfig(Fix.FromInt(6), Fix.FromInt(20),
                Fix.FromInt(20), Fix.FromInt(8), sprint: changed);
            var world = new FixedWorld(null);
            Assert.That(new SimulationWorldContext(world, config, SimulationStep.Step50Hz).Identity.ConfigurationHash,
                Is.Not.EqualTo(new SimulationWorldContext(world, Config, SimulationStep.Step50Hz).Identity.ConfigurationHash));
        }
    }
}

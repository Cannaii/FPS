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
    public sealed class DashSimulationTests
    {
        private static PlayerMovementConfig Config => new PlayerMovementConfig(
            Fix.FromInt(6), Fix.FromInt(20), Fix.FromInt(20), Fix.FromInt(8));

        private static PlayerInputCommand Input(uint tick, bool dash = false, int yaw = 0)
        {
            var input = new PlayerInputCommand { Tick = tick, DashPressed = dash };
            input.SetFixedControls(Fix.Zero, Fix.One, Fix.FromInt(yaw), Fix.Zero);
            return input;
        }

        private static PlayerMovementState Step(PlayerMovementState state, PlayerInputCommand input) =>
            PlayerMovementSimulation.Simulate(state, input, Config, SimulationStep.Step50Hz,
                FixedFlatGroundCollisionWorld.Instance);

        [TestCase(false)]
        [TestCase(true)]
        public void Dash_LocksDirectionAndFreezesVerticalVelocity(bool grounded)
        {
            var state = new PlayerMovementState
            {
                IsGrounded = grounded,
                PositionFixed = new FixVec3(Fix.Zero, grounded ? Fix.Zero : Fix.FromInt(5), Fix.Zero),
                VelocityFixed = new FixVec3(Fix.Zero, Fix.FromInt(-3), Fix.Zero)
            };
            var input = Input(1, true);
            input.SprintHeld = true;
            input.JumpPressed = true;
            state = Step(state, input);
            Assert.That(state.VelocityFixed, Is.EqualTo(FixVec3.Forward * Config.Dash.Speed));
            Assert.That(state.StaminaSpentFixed, Is.EqualTo(Config.Dash.StaminaCost));
            Assert.That(state.IsSprinting, Is.False);
            Assert.That(state.DashSpentCharges, Is.EqualTo(1));
            for (uint tick = 2; tick <= 8; tick++)
            {
                state = Step(state, Input(tick, true, 90));
                Assert.That(state.VelocityFixed, Is.EqualTo(FixVec3.Forward * Config.Dash.Speed));
                Assert.That(state.DashSpentCharges, Is.EqualTo(1));
            }
            Assert.That(state.DashRemainingFixed, Is.EqualTo(Fix.Zero));
            Assert.That(state.DashVelocityFixed, Is.EqualTo(FixVec3.Zero));
            state = Step(state, Input(9));
            Assert.That(state.VelocityFixed.Z, Is.LessThan(Config.Dash.Speed));
            if (!grounded)
            {
                Assert.That(state.VelocityFixed.Y, Is.LessThan(Fix.Zero));
            }
        }

        [Test]
        public void IdleAndDiagonalInput_UseFullHorizontalSpeed()
        {
            var input = Input(1, true, 90);
            input.SetFixedControls(Fix.Zero, Fix.Zero, Fix.FromInt(90), Fix.FromInt(80));
            var idle = Step(new PlayerMovementState { IsGrounded = true }, input);
            Assert.That(idle.VelocityFixed.X.Raw, Is.EqualTo(Config.Dash.Speed.Raw).Within(16));
            input.SetFixedControls(Fix.One, Fix.One, Fix.Zero, Fix.Zero);
            var diagonal = Step(new PlayerMovementState { IsGrounded = true }, input);
            Assert.That(diagonal.VelocityFixed.Magnitude.Raw, Is.EqualTo(Config.Dash.Speed.Raw).Within(32));
        }

        [Test]
        public void InsufficientStaminaOrCharges_DoesNotSpendResources()
        {
            var state = new PlayerMovementState { IsGrounded = true, StaminaSpentFixed = Fix.One };
            state = Step(state, Input(1, true));
            Assert.That(state.DashSpentCharges, Is.EqualTo(0));
            Assert.That(state.DashRemainingFixed, Is.EqualTo(Fix.Zero));
            state.StaminaSpentFixed = Fix.Zero;
            state.DashSpentCharges = Config.Dash.MaxCharges;
            state.DashRechargeRemainingFixed = Fix.One;
            state = Step(state, Input(2, true));
            Assert.That(state.DashSpentCharges, Is.EqualTo(Config.Dash.MaxCharges));
            Assert.That(state.StaminaSpentFixed, Is.EqualTo(Fix.Zero));
            Assert.That(state.DashRemainingFixed, Is.EqualTo(Fix.Zero));
        }

        [Test]
        public void ExactStaminaThreshold_AllowsDashAndLatchesExhaustion()
        {
            var state = new PlayerMovementState
            {
                IsGrounded = true, StaminaSpentFixed = Fix.One - Config.Dash.StaminaCost
            };
            state = Step(state, Input(1, true));
            Assert.That(state.StaminaSpentFixed, Is.EqualTo(Fix.One));
            Assert.That(state.SprintExhausted, Is.True);
            Assert.That(state.DashSpentCharges, Is.EqualTo(1));
        }

        [Test]
        public void Dash_StopsAtWallAndConsumesOneCharge()
        {
            var wall = new FixedWorldBox(1, 0, FixedSurface.Solid, new FixedAabb(
                new FixVec3(Fix.FromInt(-5), Fix.Zero, Fix.One),
                new FixVec3(Fix.FromInt(5), Fix.FromInt(5), Fix.FromInt(2))));
            var floor = new FixedWorldPlane(2, 0, FixedSurface.Solid | FixedSurface.Ground, FixedAxis.Y, Fix.Zero);
            var context = new SimulationWorldContext(new FixedWorld(new[] { wall }, new[] { floor }),
                Config, SimulationStep.Step50Hz);
            var state = new PlayerMovementState { IsGrounded = true };
            for (uint tick = 1; tick <= 8; tick++)
            {
                state = PlayerMovementSimulation.Simulate(state, Input(tick, true), Config,
                    SimulationStep.Step50Hz, context.CollisionWorld);
                Assert.That(state.PositionFixed.Z, Is.LessThanOrEqualTo(Fix.Half));
            }
            Assert.That(state.VelocityFixed.Z, Is.EqualTo(Fix.Zero));
            Assert.That(state.DashRemainingFixed, Is.EqualTo(Fix.Zero));
            Assert.That(state.DashSpentCharges, Is.EqualTo(1));
            Assert.That(state.StaminaSpentFixed, Is.EqualTo(Config.Dash.StaminaCost));
        }

        [Test]
        public void Recharge_IsSequentialAndPreservesOvershoot()
        {
            var state = new PlayerMovementState { DashSpentCharges = 2, DashRechargeRemainingFixed = Fix.Half };
            PlayerDashSimulation.Begin(ref state, default, Config.Dash, FixVec3.Zero, FixVec3.Forward, Fix.One);
            Assert.That(state.DashSpentCharges, Is.EqualTo(1));
            Assert.That(state.DashRechargeRemainingFixed, Is.EqualTo(Fix.One));
            PlayerDashSimulation.Begin(ref state, default, Config.Dash, FixVec3.Zero, FixVec3.Forward, Fix.FromInt(60));
            Assert.That(state.DashSpentCharges, Is.EqualTo(0));
            Assert.That(state.DashRechargeRemainingFixed, Is.EqualTo(Fix.Zero));
        }

        [Test]
        public void EdgeInput_RoundtripsAndIsNeverRepeatedByFallback()
        {
            var input = Input(uint.MaxValue, true);
            var bytes = new byte[InputCommandBatchCodec.GetPacketSize(1)];
            Assert.That(InputCommandBatchCodec.TrySerialize(new InputCommandBatch(
                new ArraySegment<PlayerInputCommand>(new[] { input })), 1,
                new ArraySegment<byte>(bytes), out _), Is.True);
            var decoded = new PlayerInputCommand[1];
            Assert.That(InputCommandBatchCodec.TryDeserialize(new ArraySegment<byte>(bytes), decoded,
                0, out _, out _), Is.True);
            Assert.That(decoded[0].DashPressed, Is.True);
            Assert.That(decoded[0].CreateFallback(0, true).DashPressed, Is.False);
            Assert.That(decoded[0].CreateFallback(0, false).DashPressed, Is.False);
        }

        [Test]
        public void Authority_RoundtripsEveryRawFieldAndRejectsInvalidDash()
        {
            var state = Step(new PlayerMovementState { IsGrounded = true }, Input(1, true));
            var bytes = new byte[AuthoritativeMovementStateCodec.PacketSize];
            Assert.That(AuthoritativeMovementStateCodec.TrySerialize(new AuthoritativeMovementState(10, 1, state),
                1, new ArraySegment<byte>(bytes), out int count), Is.True);
            Assert.That(count, Is.EqualTo(152));
            Assert.That(AuthoritativeMovementStateCodec.TryDeserialize(new ArraySegment<byte>(bytes),
                out _, out var authority), Is.True);
            Assert.That(authority.State.RawEquals(state), Is.True);
            // 充能在整包第65字节；大于协议上限或与计时器矛盾都应拒绝。
            bytes[65] = 9;
            Assert.That(AuthoritativeMovementStateCodec.TryDeserialize(new ArraySegment<byte>(bytes), out _, out _), Is.False);
            bytes[65] = 0;
            Assert.That(AuthoritativeMovementStateCodec.TryDeserialize(new ArraySegment<byte>(bytes), out _, out _), Is.False);
            state.DashVelocityFixed = FixVec3.Up;
            Assert.That(AuthoritativeMovementStateCodec.TrySerialize(new AuthoritativeMovementState(10, 1, state),
                1, new ArraySegment<byte>(bytes), out _), Is.False);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void DashOnlyError_TriggersRollbackAcrossTickWrap(int field)
        {
            var authority = Step(new PlayerMovementState { Tick = uint.MaxValue - 1, IsGrounded = true }, Input(uint.MaxValue, true));
            var predicted = authority;
            switch (field)
            {
                case 0:
                    predicted.DashRemainingFixed += Fix.Epsilon;
                    break;
                case 1:
                    predicted.DashVelocityFixed += FixVec3.Right * Fix.Epsilon;
                    break;
                case 2:
                    predicted.DashSpentCharges++;
                    break;
                case 3:
                    predicted.DashRechargeRemainingFixed += Fix.Epsilon;
                    break;
            }
            Assert.That(PlayerMovementStateHash.Compute(predicted), Is.Not.EqualTo(PlayerMovementStateHash.Compute(authority)));
            var states = new TickBuffer<PlayerMovementState>(8);
            states.Store(predicted.Tick, predicted);
            var inputs = new TickBuffer<PlayerInputCommand>(8);
            inputs.Store(0, Input(0));
            inputs.Store(1, Input(1));
            var current = Step(Step(predicted, Input(0)), Input(1));
            var result = MovementPredictionReconciler.Reconcile(new AuthoritativeMovementState(10, authority.Tick, authority),
                1, current, inputs, states, Config, SimulationStep.Step50Hz, FixedFlatGroundCollisionWorld.Instance);
            Assert.That(result.Status, Is.EqualTo(MovementReconciliationStatus.Corrected));
            Assert.That(result.State.RawEquals(Step(Step(authority, Input(0)), Input(1))), Is.True);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void EveryDashRule_ChangesWorldFingerprint(int field)
        {
            var original = Config.Dash;
            var changed = new PlayerDashConfig(
                original.Speed + (field == 0 ? Fix.Epsilon : Fix.Zero),
                original.Duration + (field == 1 ? Fix.Epsilon : Fix.Zero),
                original.RechargeDuration + (field == 2 ? Fix.Epsilon : Fix.Zero),
                original.MaxCharges + (field == 3 ? 1 : 0),
                original.StaminaCost + (field == 4 ? Fix.Epsilon : Fix.Zero));
            var config = new PlayerMovementConfig(Fix.FromInt(6), Fix.FromInt(20),
                Fix.FromInt(20), Fix.FromInt(8), dash: changed);
            var world = new FixedWorld(null);
            Assert.That(new SimulationWorldContext(world, config, SimulationStep.Step50Hz).Identity.ConfigurationHash,
                Is.Not.EqualTo(new SimulationWorldContext(world, Config, SimulationStep.Step50Hz).Identity.ConfigurationHash));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerMovementConfig(
                Fix.One, Fix.One, Fix.One, Fix.One, dash: default(PlayerDashConfig)));
        }

        [Test]
        public void CapabilityReplay_MatchesGoldenHash()
        {
            Assert.That(DeterministicDashScenario.Run(), Is.EqualTo(DeterministicDashScenario.ExpectedHash));
        }
    }
}

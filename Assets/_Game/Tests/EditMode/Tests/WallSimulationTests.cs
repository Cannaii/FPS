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
    public sealed class WallSimulationTests
    {
        private static PlayerSimulationConfig Config => new PlayerSimulationConfig(
            Fix.FromInt(6), Fix.FromInt(20), Fix.FromInt(20), Fix.FromInt(8));

        private static PlayerState NearWall => new PlayerState
        {
            PositionFixed = new FixVec3(Fix.FromRatio(3, 2), Fix.FromInt(3), Fix.Zero),
            WallNormalFixed = -FixVec3.Right,
            WallIncomingVelocityFixed = new FixVec3(Fix.FromInt(16), -Fix.One, Fix.FromInt(2))
        };

        private static FixedCharacterCollisionWorld World => new FixedCharacterCollisionWorld(new FixedWorld(new[]
        {
            new FixedWorldBox(1, 0, FixedSurface.Solid, new FixedAabb(
                new FixVec3(Fix.FromInt(2), Fix.Zero, Fix.FromInt(-10)),
                new FixVec3(Fix.FromInt(3), Fix.FromInt(10), Fix.FromInt(10))))
        }));

        private static PlayerState Step(PlayerState state, bool jump = false, bool bounce = false, bool dash = false)
        {
            var input = new PlayerInputCommand
            {
                Tick = unchecked(state.Tick + 1),
                JumpPressed = jump,
                WallBouncePressed = bounce,
                DashPressed = dash
            };
            input.SetFixedControls(Fix.One, Fix.Zero, Fix.Zero, Fix.Zero);
            return PlayerSimulation.Simulate(state, input, Config, SimulationStep.Step50Hz, World);
        }

        [Test]
        public void WallJump_UsesContactPreservesTangentAndSpentAirJump()
        {
            var state = NearWall;
            state.AirJumpsUsed = 1;
            state.GroundJumpConsumed = true;
            state.VelocityFixed = FixVec3.Forward * Fix.FromInt(2);
            state = Step(state, jump: true);
            Assert.That(state.VelocityFixed.X, Is.EqualTo(-Config.Wall.PushSpeed));
            Assert.That(state.VelocityFixed.Z, Is.EqualTo(Fix.FromInt(2)));
            Assert.That(state.VelocityFixed.Y,
                Is.EqualTo(Config.Wall.UpSpeed - Config.GravityFixed * SimulationStep.Step50Hz));
            Assert.That(state.AirJumpsUsed, Is.EqualTo(1));
            Assert.That(state.CoyoteRemainingFixed, Is.EqualTo(Fix.Zero));
            Assert.That(state.WallCooldownRemainingFixed, Is.EqualTo(Config.Wall.Cooldown));
            Assert.That(state.StaminaSpentFixed, Is.EqualTo(Config.Wall.StaminaCost));
            Assert.That(state.WallNormalFixed, Is.EqualTo(FixVec3.Zero), "离墙后必须清接触。");
        }

        [Test]
        public void DashImpactThenBounce_ReflectsImpactAndCancelsDashWithoutNewCharge()
        {
            var state = new PlayerState
            {
                PositionFixed = new FixVec3(Fix.FromRatio(7, 5), Fix.FromInt(3), Fix.Zero)
            };
            state = Step(state, dash: true);
            Assert.That(state.WallNormalFixed, Is.EqualTo(-FixVec3.Right));
            Assert.That(state.WallIncomingVelocityFixed.X, Is.EqualTo(Config.Dash.Speed));
            Assert.That(state.VelocityFixed.X, Is.EqualTo(Fix.Zero));
            state = Step(state, bounce: true, dash: true);
            Assert.That(state.VelocityFixed.X, Is.EqualTo(-Config.Dash.Speed));
            Assert.That(state.VelocityFixed.Y, Is.LessThan(Fix.Zero));
            Assert.That(state.DashRemainingFixed, Is.EqualTo(Fix.Zero));
            Assert.That(state.DashSpentCharges, Is.EqualTo(1));
            Assert.That(state.StaminaSpentFixed, Is.EqualTo(Config.Dash.StaminaCost + Config.Wall.StaminaCost));
        }

        [TestCase(0, true)]
        [TestCase(1, false)]
        public void CooldownBoundary_IsCheckedAfterAdvancingTime(int extraRaw, bool expected)
        {
            var previous = NearWall;
            previous.WallCooldownRemainingFixed = SimulationStep.Step50Hz + Fix.FromRaw(extraRaw);
            var next = previous;
            PlayerWallSimulation.AdvanceTimers(ref next, SimulationStep.Step50Hz);
            Assert.That(PlayerWallSimulation.TryBounce(ref next, previous,
                new PlayerInputCommand { WallBouncePressed = true }, Config.Wall), Is.EqualTo(expected));
        }

        [TestCase(0, true)]
        [TestCase(1, false)]
        public void StaminaBoundary_DoesNotSpendOnRejectedAction(int extraRaw, bool expected)
        {
            var previous = NearWall;
            previous.StaminaSpentFixed = Fix.One - Config.Wall.StaminaCost + Fix.FromRaw(extraRaw);
            var next = previous;
            Assert.That(PlayerWallSimulation.TryBounce(ref next, previous,
                new PlayerInputCommand { WallBouncePressed = true }, Config.Wall), Is.EqualTo(expected));
            Assert.That(next.StaminaSpentFixed, Is.EqualTo(expected ? Fix.One : previous.StaminaSpentFixed));
            Assert.That(next.SprintExhausted, Is.EqualTo(expected));
        }

        [Test]
        public void GroundedOrMissingWall_DoesNotEnableWallAction()
        {
            var previous = NearWall;
            previous.IsGrounded = true;
            var next = previous;
            var input = new PlayerInputCommand { JumpPressed = true, WallBouncePressed = true };
            Assert.That(PlayerWallSimulation.TryBounce(ref next, previous, input, Config.Wall), Is.False);
            Assert.That(PlayerWallSimulation.TryJump(ref next, previous, input, Config.Wall), Is.False);
            previous = default;
            next = previous;
            Assert.That(PlayerWallSimulation.TryBounce(ref next, previous, input, Config.Wall), Is.False);
        }

        [Test]
        public void Reflection_ClampsExtremeInputBeforeDotProducts()
        {
            var previous = NearWall;
            previous.WallIncomingVelocityFixed = new FixVec3(Fix.MaxValue, Fix.MinValue, Fix.MaxValue);
            var next = previous;
            Assert.That(PlayerWallSimulation.TryBounce(ref next, previous,
                new PlayerInputCommand { WallBouncePressed = true }, Config.Wall), Is.True);
            Assert.That(next.VelocityFixed.Magnitude.Raw, Is.LessThanOrEqualTo(Config.Wall.MaximumBounceSpeed.Raw + 4));
            Assert.That(-next.VelocityFixed.X.Raw, Is.GreaterThanOrEqualTo(Config.Wall.PushSpeed.Raw - 4));
        }

        [Test]
        public void DashPriority_IgnoresWallJumpWithoutSpendingItsCost()
        {
            var state = Step(NearWall, jump: true, dash: true);
            Assert.That(state.WallCooldownRemainingFixed, Is.EqualTo(Fix.Zero));
            Assert.That(state.StaminaSpentFixed, Is.EqualTo(Config.Dash.StaminaCost));
            Assert.That(state.AirJumpsUsed, Is.EqualTo(0));
        }

        [Test]
        public void BounceEdge_RoundtripsAndFallbackAlwaysClearsIt()
        {
            var input = new PlayerInputCommand { Tick = uint.MaxValue, WallBouncePressed = true };
            var bytes = new byte[InputCommandBatchCodec.GetPacketSize(1)];
            Assert.That(InputCommandBatchCodec.TrySerialize(new InputCommandBatch(
                new ArraySegment<PlayerInputCommand>(new[] { input })), 1, new ArraySegment<byte>(bytes), out _), Is.True);
            var buffer = new PlayerInputCommand[1];
            Assert.That(InputCommandBatchCodec.TryDeserialize(new ArraySegment<byte>(bytes), buffer, 0, out _, out _), Is.True);
            Assert.That(buffer[0].WallBouncePressed, Is.True);
            Assert.That(buffer[0].CreateFallback(0, true).WallBouncePressed, Is.False);
            Assert.That(buffer[0].CreateFallback(0, false).WallBouncePressed, Is.False);
        }

        [Test]
        public void Authority_TransportsWallRawAndRejectsInvalidNormals()
        {
            var state = NearWall;
            state.WallCooldownRemainingFixed = Fix.FromRaw(12345);
            var bytes = new byte[AuthoritativePlayerStateCodec.PacketSize];
            Assert.That(AuthoritativePlayerStateCodec.TrySerialize(new AuthoritativePlayerState(10, 0, state),
                1, new ArraySegment<byte>(bytes), out int count), Is.True);
            Assert.That(count, Is.EqualTo(131));
            Assert.That(AuthoritativePlayerStateCodec.TryDeserialize(new ArraySegment<byte>(bytes),
                out _, out var decoded), Is.True);
            Assert.That(decoded.State.RawEquals(state), Is.True);
            bytes[98] = 128;
            Assert.That(AuthoritativePlayerStateCodec.TryDeserialize(new ArraySegment<byte>(bytes), out _, out _), Is.False);
            foreach (var normal in new[] { FixVec3.Up, FixVec3.Zero, FixVec3.Right * Fix.Half })
            {
                state.WallNormalFixed = normal;
                Assert.That(AuthoritativePlayerStateCodec.TrySerialize(new AuthoritativePlayerState(10, 0, state),
                    1, new ArraySegment<byte>(bytes), out _), Is.False);
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void WallOnlyError_TriggersRollbackAcrossTickWrap(int field)
        {
            var authority = NearWall;
            authority.Tick = uint.MaxValue;
            var predicted = authority;
            switch (field)
            {
                case 0:
                    predicted.WallNormalFixed = FixVec3.Forward;
                    break;
                case 1:
                    predicted.WallIncomingVelocityFixed += FixVec3.Up * Fix.Epsilon;
                    break;
                case 2:
                    predicted.WallCooldownRemainingFixed = Fix.One;
                    break;
            }
            Assert.That(PlayerStateHash.Compute(predicted), Is.Not.EqualTo(PlayerStateHash.Compute(authority)));
            var states = new TickBuffer<PlayerState>(8);
            var inputs = new TickBuffer<PlayerInputCommand>(8);
            states.Store(authority.Tick, predicted);
            inputs.Store(0, new PlayerInputCommand { Tick = 0, WallBouncePressed = true, MoveX = 1f });
            var current = Step(predicted, bounce: true);
            var result = ClientPredictionReconciler.Reconcile(new AuthoritativePlayerState(10, authority.Tick, authority),
                0, current, inputs, states, Config, SimulationStep.Step50Hz, World);
            Assert.That(result.Status, Is.EqualTo(ReconciliationStatus.Corrected));
            Assert.That(result.State.RawEquals(Step(authority, bounce: true)), Is.True);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void EveryWallRule_ChangesFingerprint(int field)
        {
            var wall = Config.Wall;
            var changed = new PlayerWallConfig(
                wall.PushSpeed + (field == 0 ? Fix.Epsilon : Fix.Zero),
                wall.UpSpeed + (field == 1 ? Fix.Epsilon : Fix.Zero),
                wall.MaximumBounceSpeed + (field == 2 ? Fix.Epsilon : Fix.Zero),
                wall.Cooldown + (field == 3 ? Fix.Epsilon : Fix.Zero),
                wall.StaminaCost + (field == 4 ? Fix.Epsilon : Fix.Zero));
            var config = new PlayerSimulationConfig(Fix.FromInt(6), Fix.FromInt(20), Fix.FromInt(20), Fix.FromInt(8), wall: changed);
            var world = new FixedWorld(null);
            Assert.That(new SimulationWorldContext(world, config, SimulationStep.Step50Hz).Identity.ConfigurationHash,
                Is.Not.EqualTo(new SimulationWorldContext(world, Config, SimulationStep.Step50Hz).Identity.ConfigurationHash));
        }

        [Test]
        public void WallReplay_MatchesGolden()
        {
            Assert.That(DeterministicWallScenario.Run(), Is.EqualTo(DeterministicWallScenario.ExpectedHash));
        }
    }
}

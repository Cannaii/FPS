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
    public sealed class ClimbSimulationTests
    {
        private static readonly PlayerSimulationConfig Config =
            new PlayerSimulationConfig(Fix.FromInt(6), Fix.FromInt(30), Fix.FromInt(20), Fix.FromInt(8));

        private static FixedWorldBox Ladder(uint id = 1, int layer = 8) => new FixedWorldBox(id, layer,
            FixedSurface.Ladder, new FixedAabb(new FixVec3(Fix.FromInt(2), Fix.Zero, Fix.FromInt(-1)),
                new FixVec3(Fix.FromRatio(21, 10), Fix.FromInt(4), Fix.One)));

        private static FixedCharacterCollisionWorld World(int mask = ~0, bool blocked = false)
        {
            var blocker = new FixedWorldBox(3, 8, FixedSurface.Solid, new FixedAabb(
                new FixVec3(Fix.FromRatio(19, 10), Fix.Zero, Fix.FromInt(-2)),
                new FixVec3(Fix.FromRatio(39, 20), Fix.FromInt(6), Fix.FromInt(2))));
            var floor = new FixedWorldPlane(2, 8, FixedSurface.Ground | FixedSurface.Solid, FixedAxis.Y, Fix.Zero);
            return new FixedCharacterCollisionWorld(new FixedWorld(blocked ? new[] { Ladder(), blocker } : new[] { Ladder() },
                new[] { floor }), mask);
        }

        private static PlayerState Initial => new PlayerState
        {
            PositionFixed = FixVec3.Right * Fix.FromRatio(3, 2), IsGrounded = true
        };

        private static PlayerInputCommand Input(uint tick, float vertical = 1f) => new PlayerInputCommand
        {
            Tick = tick, MoveY = vertical, LookYaw = 90f
        };

        private static PlayerState Step(in PlayerState state, PlayerInputCommand? input = null,
            IFixedCharacterCollisionWorld world = null) => PlayerSimulation.Simulate(state,
                input ?? Input(unchecked(state.Tick + 1)), Config, SimulationStep.Step50Hz, world ?? World());

        [Test]
        public void Climb_HoldsWithoutGravityAndDescendsWithoutChangingFeetXZ()
        {
            var state = Initial;
            for (int i = 0; i < 20; i++)
            {
                state = Step(state);
            }
            Assert.That(state.IsClimbing, Is.True);
            Assert.That(state.PositionFixed.Y, Is.GreaterThan(Fix.One));
            Assert.That(state.PositionFixed.X, Is.EqualTo(Initial.PositionFixed.X));
            var held = Step(state, Input(21, 0f));
            Assert.That(held.PositionFixed, Is.EqualTo(state.PositionFixed));
            Assert.That(held.VelocityFixed, Is.EqualTo(FixVec3.Zero));
            var down = Step(held, Input(22, -1f));
            Assert.That(down.PositionFixed.Y, Is.LessThan(held.PositionFixed.Y));
            Assert.That(down.IsGrounded, Is.False);
        }

        [Test]
        public void LadderTopAndBottom_DetachAndStartCooldown()
        {
            var state = Initial;
            state.PositionFixed += FixVec3.Up * Fix.FromRatio(399, 100);
            state.IsGrounded = false;
            state = Step(state);
            Assert.That(state.IsClimbing, Is.False);
            Assert.That(state.PositionFixed.Y, Is.GreaterThanOrEqualTo(Fix.FromInt(4)));
            Assert.That(state.ClimbCooldownRemainingFixed, Is.EqualTo(Config.Climb.ReattachDelay));
            Assert.That(state.VelocityFixed.X, Is.EqualTo(Config.Climb.ExitPushSpeed));
            state = Initial;
            state.PositionFixed += FixVec3.Up * Fix.FromRatio(1, 100);
            state.IsGrounded = false;
            state = Step(state, Input(1, -1f));
            Assert.That(state.IsClimbing, Is.False);
            Assert.That(state.PositionFixed.Y, Is.EqualTo(Fix.Zero));
            Assert.That(state.IsGrounded, Is.True);
            Assert.That(state.ClimbShapeId, Is.Zero);
        }

        [Test]
        public void JumpOff_PreservesSpentAirJumpAndDoesNotChargeWallStamina()
        {
            var state = Step(Initial);
            state.AirJumpsUsed = 1;
            var input = Input(2);
            input.JumpPressed = true;
            state = Step(state, input);
            Assert.That(state.IsClimbing, Is.False);
            Assert.That(state.VelocityFixed.X, Is.EqualTo(-Config.Climb.ExitPushSpeed));
            Assert.That(state.VelocityFixed.Y, Is.GreaterThan(Fix.FromInt(7)));
            Assert.That(state.AirJumpsUsed, Is.EqualTo(1));
            Assert.That(state.StaminaSpentFixed, Is.EqualTo(Fix.Zero));
            Assert.That(state.ClimbCooldownRemainingFixed, Is.EqualTo(Config.Climb.ReattachDelay));
            Assert.That(Step(state).IsClimbing, Is.False);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void DashCrouchAndStrafe_ExitLadder(int action)
        {
            var state = Step(Initial);
            var input = Input(2);
            input.DashPressed = action == 0;
            input.CrouchHeld = action == 1;
            input.MoveX = action == 2 ? 1f : 0f;
            state = Step(state, input);
            Assert.That(state.IsClimbing, Is.False);
            Assert.That(state.ClimbShapeId, Is.Zero);
            Assert.That(state.ClimbNormalFixed, Is.EqualTo(FixVec3.Zero));
            Assert.That(state.ClimbCooldownRemainingFixed, Is.EqualTo(Config.Climb.ReattachDelay));
        }

        [Test]
        public void Climb_RequiresFacingVisibilityAndCorrectBackend()
        {
            var away = Input(1);
            away.LookYaw = 270f;
            Assert.That(Step(Initial, away).IsClimbing, Is.False);
            Assert.That(Step(Initial, world: World(mask: 1)).IsClimbing, Is.False);
            Assert.That(Step(Initial, world: World(blocked: true)).IsClimbing, Is.False);
            Assert.That(Step(Initial, world: FixedFlatGroundCollisionWorld.Instance).IsClimbing, Is.False);
        }

        [Test]
        public void Ceiling_StopsClimbingWithoutPenetrating()
        {
            var ceiling = new FixedWorldBox(3, 8, FixedSurface.Solid,
                new FixedAabb(new FixVec3(Fix.FromInt(-3), Fix.FromInt(3), Fix.FromInt(-3)),
                    new FixVec3(Fix.FromInt(4), Fix.FromInt(4), Fix.FromInt(3))));
            var world = new FixedCharacterCollisionWorld(new FixedWorld(new[] { Ladder(), ceiling }));
            var state = Initial;
            for (int i = 0; i < 60; i++)
            {
                state = Step(state, world: world);
            }
            Assert.That(state.PositionFixed.Y, Is.LessThanOrEqualTo(Fix.One));
            Assert.That(state.IsClimbing, Is.True);
            Assert.That(state.VelocityFixed.Y, Is.EqualTo(Fix.Zero));
        }

        [Test]
        public void Owner_RoundtripsAndRejectsContradictoryClimbState()
        {
            var state = Step(Initial);
            var bytes = new byte[AuthoritativePlayerStateCodec.PacketSize];
            Assert.That(AuthoritativePlayerStateCodec.TrySerialize(new AuthoritativePlayerState(10, state.Tick, state),
                1, new ArraySegment<byte>(bytes), out int count), Is.True);
            Assert.That(count, Is.EqualTo(131));
            Assert.That(AuthoritativePlayerStateCodec.TryDeserialize(new ArraySegment<byte>(bytes), out _, out var decoded), Is.True);
            Assert.That(decoded.State.RawEquals(state), Is.True);
            bytes[130] = 128;
            Assert.That(AuthoritativePlayerStateCodec.TryDeserialize(new ArraySegment<byte>(bytes), out _, out _), Is.False);
            state.IsGrounded = true;
            Assert.That(AuthoritativePlayerStateCodec.TrySerialize(new AuthoritativePlayerState(10, state.Tick, state),
                1, new ArraySegment<byte>(bytes), out _), Is.False);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void ClimbOnlyDrift_TriggersRollbackAcrossWrap(int field)
        {
            var authority = Step(Initial);
            authority.Tick = uint.MaxValue;
            var predicted = authority;
            switch (field)
            {
                case 0:
                    predicted.IsClimbing = false;
                    break;
                case 1:
                    predicted.ClimbShapeId = 9;
                    break;
                case 2:
                    predicted.ClimbNormalFixed = FixVec3.Forward;
                    break;
                case 3:
                    predicted.ClimbCooldownRemainingFixed = Fix.One;
                    break;
            }
            Assert.That(PlayerStateHash.Compute(predicted), Is.Not.EqualTo(PlayerStateHash.Compute(authority)));
            var inputs = new TickBuffer<PlayerInputCommand>(8);
            var states = new TickBuffer<PlayerState>(8);
            states.Store(authority.Tick, predicted);
            inputs.Store(0, Input(0));
            var result = ClientPredictionReconciler.Reconcile(new AuthoritativePlayerState(10, authority.Tick, authority),
                0, Step(predicted), inputs, states, Config, SimulationStep.Step50Hz, World());
            Assert.That(result.Status, Is.EqualTo(ReconciliationStatus.Corrected));
            Assert.That(result.State.RawEquals(Step(authority)), Is.True);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void EveryClimbRule_ChangesFingerprint(int field)
        {
            var c = Config.Climb;
            var changed = new PlayerClimbConfig(c.Speed + (field == 0 ? Fix.Epsilon : Fix.Zero),
                c.Reach + (field == 1 ? Fix.Epsilon : Fix.Zero), c.ExitPushSpeed + (field == 2 ? Fix.Epsilon : Fix.Zero),
                c.ReattachDelay + (field == 3 ? Fix.Epsilon : Fix.Zero), c.FacingAlignment + (field == 4 ? Fix.Epsilon : Fix.Zero));
            var config = new PlayerSimulationConfig(Fix.FromInt(6), Fix.FromInt(30), Fix.FromInt(20), Fix.FromInt(8), climb: changed);
            var world = new FixedWorld(null);
            Assert.That(new SimulationWorldContext(world, config, SimulationStep.Step50Hz).Identity.ConfigurationHash,
                Is.Not.EqualTo(new SimulationWorldContext(world, Config, SimulationStep.Step50Hz).Identity.ConfigurationHash));
        }

        [Test]
        public void ZeroClimbConfiguration_IsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerSimulationConfig(
                Fix.FromInt(6), Fix.FromInt(30), Fix.FromInt(20), Fix.FromInt(8), climb: default(PlayerClimbConfig)));
        }

        [TestCase(-1, true)]
        [TestCase(0, true)]
        [TestCase(1, false)]
        public void LadderQuery_IncludesHorizontalReachBoundary(int offsetRaw, bool expected)
        {
            var world = new FixedWorld(new[] { Ladder() });
            var feet = FixVec3.Right * (Fix.FromInt(2) - Fix.Half - Config.Climb.Reach - Fix.FromRaw(offsetRaw));
            Assert.That(world.LadderCheck(feet, Fix.Half, Fix.FromInt(2), Config.Climb.Reach).HasLadder, Is.EqualTo(expected));
        }

        [Test]
        public void LadderQuery_UsesShapeIdTieAndPreferenceAndRespectsVerticalEnds()
        {
            var world = new FixedWorld(new[] { Ladder(7), Ladder(3) });
            var feet = Initial.PositionFixed;
            Assert.That(world.LadderCheck(feet, Fix.Half, Fix.FromInt(2), Config.Climb.Reach).ShapeId, Is.EqualTo(3));
            Assert.That(world.LadderCheck(feet, Fix.Half, Fix.FromInt(2), Config.Climb.Reach, 7).ShapeId, Is.EqualTo(7));
            Assert.That(world.LadderCheck(feet + FixVec3.Up * Fix.FromInt(4), Fix.Half, Fix.FromInt(2), Config.Climb.Reach).HasLadder, Is.False);
            IWorldQuery filtered = new FixedCharacterCollisionWorld(world, 1);
            Assert.That(filtered.LadderCheck(feet, Fix.Half, Fix.FromInt(2), Config.Climb.Reach, layerMask: 1 << 8).HasLadder, Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => world.LadderCheck(feet, Fix.Half, Fix.One, -Fix.Epsilon));
        }

        [Test]
        public void ClimbReplay_MatchesGolden()
        {
            Assert.That(DeterministicClimbScenario.Run(), Is.EqualTo(DeterministicClimbScenario.ExpectedHash));
        }

        [Test]
        public void LadderQuery_ExtremeCoordinateDifferenceDoesNotOverflow()
        {
            var box = new FixedWorldBox(1, 0, FixedSurface.Ladder, new FixedAabb(
                new FixVec3(Fix.FromRaw(int.MinValue), Fix.Zero, -Fix.One),
                new FixVec3(Fix.FromRaw(int.MinValue + 100), Fix.FromInt(4), Fix.One)));
            var world = new FixedWorld(new[] { box });
            Assert.That(world.LadderCheck(new FixVec3(Fix.MaxValue, Fix.Zero, Fix.Zero),
                Fix.Half, Fix.FromInt(2), Config.Climb.Reach).HasLadder, Is.False);
        }

        [Test]
        public void ZFacingLadder_ClimbsUsingWorldForward()
        {
            var box = new FixedWorldBox(1, 0, FixedSurface.Ladder, new FixedAabb(
                new FixVec3(-Fix.One, Fix.Zero, Fix.FromInt(2)),
                new FixVec3(Fix.One, Fix.FromInt(4), Fix.FromRatio(21, 10))));
            var world = new FixedCharacterCollisionWorld(new FixedWorld(new[] { box }));
            var state = Initial;
            state.PositionFixed = FixVec3.Forward * Fix.FromRatio(3, 2);
            var input = Input(1);
            input.LookYaw = 0f;
            state = Step(state, input, world);
            Assert.That(state.IsClimbing, Is.True);
            Assert.That(state.ClimbNormalFixed, Is.EqualTo(-FixVec3.Forward));
            Assert.That(state.PositionFixed.Y, Is.GreaterThan(Fix.Zero));
        }
    }
}

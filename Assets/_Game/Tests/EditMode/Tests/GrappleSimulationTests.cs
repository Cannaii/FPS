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
    public sealed class GrappleSimulationTests
    {
        private static readonly PlayerSimulationConfig Config =
            new PlayerSimulationConfig(Fix.FromInt(6), Fix.FromInt(30), Fix.FromInt(20), Fix.FromInt(8));
        private static PlayerState Initial => new PlayerState
        {
            PositionFixed = FixVec3.Up * Fix.FromInt(5), GroundJumpConsumed = true
        };

        private static FixedCharacterCollisionWorld World(bool grapple = true, int mask = ~0, bool blocker = false)
        {
            var anchor = new FixedWorldPlane(1, 8, FixedSurface.Solid | (grapple ? FixedSurface.Grapple : FixedSurface.Wall),
                FixedAxis.Z, Fix.FromInt(10), positiveNormal: false);
            var wall = new FixedWorldPlane(2, 8, FixedSurface.Solid, FixedAxis.Z, Fix.FromInt(8), positiveNormal: false);
            return new FixedCharacterCollisionWorld(new FixedWorld(null, blocker ? new[] { anchor, wall } : new[] { anchor }), mask);
        }

        private static PlayerInputCommand Input(uint tick, bool held = true) => new PlayerInputCommand { Tick = tick, GrappleHeld = held };
        private static PlayerState Step(in PlayerState state, PlayerInputCommand? input = null,
            IFixedCharacterCollisionWorld world = null) => PlayerSimulation.Simulate(state,
                input ?? Input(unchecked(state.Tick + 1)), Config, SimulationStep.Step50Hz, world ?? World());

        [Test]
        public void Acquire_UsesWorldAnchorAndReelsWithoutRetargetingToNewAim()
        {
            var first = Step(Initial);
            Assert.That(first.IsGrappling, Is.True);
            Assert.That(first.GrapplePointFixed.Z, Is.EqualTo(Fix.FromInt(10)));
            var input = Input(2);
            input.LookYaw = 90f;
            var next = Step(first, input);
            Assert.That(next.IsGrappling, Is.True);
            Assert.That(next.GrapplePointFixed, Is.EqualTo(first.GrapplePointFixed));
            Assert.That(next.GrappleLengthFixed, Is.LessThan(first.GrappleLengthFixed));
            Assert.That(next.VelocityFixed.X, Is.GreaterThan(Fix.Zero));
        }

        [Test]
        public void MissOrForcedCancel_RequiresReleaseBeforeRetry()
        {
            var missed = Step(Initial, world: World(false));
            Assert.That(missed.IsGrappling, Is.False);
            Assert.That(missed.GrappleConsumed, Is.True);
            Assert.That(Step(missed).IsGrappling, Is.False);
            var released = Step(missed, Input(2, false));
            Assert.That(released.GrappleConsumed, Is.False);
            Assert.That(Step(released).IsGrappling, Is.True);
        }

        [Test]
        public void Acquire_RejectsOccludedOrExcludedTargetsAndMissingWorldQuery()
        {
            Assert.That(Step(Initial, world: World(blocker: true)).IsGrappling, Is.False);
            Assert.That(Step(Initial, world: World(mask: 1)).IsGrappling, Is.False);
            Assert.That(Step(Initial, world: FixedFlatGroundCollisionWorld.Instance).IsGrappling, Is.False);
            Assert.That(Step(Initial, world: World(false)).IsGrappling, Is.False);
        }

        [Test]
        public void ExistingRope_BreaksWhenSolidOccludesSegment()
        {
            var active = Step(Initial);
            var next = Step(active, world: World(blocker: true));
            Assert.That(next.IsGrappling, Is.False);
            Assert.That(next.GrappleShapeId, Is.Zero);
            Assert.That(next.GrappleConsumed, Is.True);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void JumpDashAndCrouch_CancelRope(int action)
        {
            var previous = Step(Initial);
            var input = Input(2);
            input.JumpPressed = action == 0;
            input.DashPressed = action == 1;
            input.CrouchHeld = action == 2;
            var state = Step(previous, input);
            Assert.That(state.IsGrappling, Is.False);
            Assert.That(state.GrappleConsumed, Is.True);
            Assert.That(state.GrapplePointFixed, Is.EqualTo(FixVec3.Zero));
            Assert.That(state.GrappleLengthFixed, Is.EqualTo(Fix.Zero));
        }

        [Test]
        public void Pendulum_RemovesOutwardVelocityAndKeepsTangentWithoutPositionTeleport()
        {
            var c = Config.Grapple;
            var rules = new PlayerGrappleConfig(c.Range, Fix.Zero, Fix.Zero, c.MaximumSpeed,
                Fix.Zero, c.BreakDistance, c.OriginHeight);
            var config = new PlayerSimulationConfig(Fix.FromInt(6), Fix.FromInt(30), Fix.FromInt(20), Fix.FromInt(8), grapple: rules);
            var state = new PlayerState
            {
                IsGrappling = true, GrappleConsumed = true, GrappleShapeId = 1,
                GrapplePointFixed = FixVec3.Forward * Fix.FromInt(10), GrappleLengthFixed = Fix.FromInt(10)
            };
            PlayerGrappleSimulation.Apply(ref state, FixVec3.Right * Fix.FromInt(4) - FixVec3.Forward * Fix.FromInt(6), config, SimulationStep.Step50Hz);
            Assert.That(state.PositionFixed, Is.EqualTo(FixVec3.Zero));
            Assert.That(state.VelocityFixed.X, Is.EqualTo(Fix.FromInt(4)));
            Assert.That(state.VelocityFixed.Z, Is.EqualTo(Fix.Zero));
            state.GrappleLengthFixed = Fix.FromInt(8);
            PlayerGrappleSimulation.Apply(ref state, FixVec3.Zero, config, SimulationStep.Step50Hz);
            Assert.That(state.VelocityFixed.Z, Is.GreaterThan(Fix.Zero));
            Assert.That(state.VelocityFixed.Magnitude, Is.LessThanOrEqualTo(c.MaximumSpeed + Fix.Epsilon));
            Assert.That(state.PositionFixed, Is.EqualTo(FixVec3.Zero));
        }

        [Test]
        public void CloseAnchorAndExtremeState_CancelWithoutOverflow()
        {
            var state = Step(Initial);
            state.GrapplePointFixed = state.PositionFixed + FixVec3.Up;
            PlayerGrappleSimulation.Complete(ref state, Config.Grapple);
            Assert.That(state.IsGrappling, Is.False);
            Assert.That(PlayerGrappleSimulation.TryOffset(new FixVec3(Fix.MinValue, Fix.Zero, Fix.Zero),
                new FixVec3(Fix.MaxValue, Fix.Zero, Fix.Zero), out _, out _), Is.False);
            state = Initial;
            state.PositionFixed = FixVec3.Up * Fix.MaxValue;
            Assert.That(Step(state).IsGrappling, Is.False);
        }

        [Test]
        public void Input_UsesHighButtonByteAndFallbackSemantics()
        {
            var input = Input(uint.MaxValue);
            var bytes = new byte[InputCommandBatchCodec.GetPacketSize(1)];
            Assert.That(InputCommandBatchCodec.BytesPerCommand, Is.EqualTo(16));
            Assert.That(InputCommandBatchCodec.TrySerialize(new InputCommandBatch(
                new ArraySegment<PlayerInputCommand>(new[] { input })), 1, new ArraySegment<byte>(bytes), out _), Is.True);
            Assert.That(bytes[20], Is.EqualTo(1));
            var output = new PlayerInputCommand[1];
            Assert.That(InputCommandBatchCodec.TryDeserialize(new ArraySegment<byte>(bytes), output, 0, out _, out _), Is.True);
            Assert.That(output[0].GrappleHeld, Is.True);
            Assert.That(output[0].CreateFallback(0, true).GrappleHeld, Is.True);
            Assert.That(output[0].CreateFallback(0, false).GrappleHeld, Is.False);
            bytes[20] = 2;
            Assert.That(InputCommandBatchCodec.TryDeserialize(new ArraySegment<byte>(bytes), output, 0, out _, out _), Is.False);
        }

        [Test]
        public void Owner_RoundtripsAnchorAndRejectsContradictions()
        {
            var state = Step(Initial);
            var bytes = new byte[AuthoritativePlayerStateCodec.PacketSize];
            Assert.That(AuthoritativePlayerStateCodec.TrySerialize(new AuthoritativePlayerState(10, state.Tick, state),
                1, new ArraySegment<byte>(bytes), out int count), Is.True);
            Assert.That(count, Is.EqualTo(152));
            Assert.That(AuthoritativePlayerStateCodec.TryDeserialize(new ArraySegment<byte>(bytes), out _, out var decoded), Is.True);
            Assert.That(decoded.State.RawEquals(state), Is.True);
            bytes[131] = 4;
            Assert.That(AuthoritativePlayerStateCodec.TryDeserialize(new ArraySegment<byte>(bytes), out _, out _), Is.False);
            state.GrappleConsumed = false;
            Assert.That(AuthoritativePlayerStateCodec.TrySerialize(new AuthoritativePlayerState(10, state.Tick, state),
                1, new ArraySegment<byte>(bytes), out _), Is.False);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void GrappleOnlyDrift_TriggersRollbackAcrossWrap(int field)
        {
            var authority = Step(Initial);
            authority.Tick = uint.MaxValue;
            var predicted = authority;
            switch (field)
            {
                case 0:
                    predicted.IsGrappling = false;
                    break;
                case 1:
                    predicted.GrappleConsumed = false;
                    break;
                case 2:
                    predicted.GrapplePointFixed += FixVec3.Up * Fix.Epsilon;
                    break;
                case 3:
                    predicted.GrappleLengthFixed += Fix.Epsilon;
                    break;
                case 4:
                    predicted.GrappleShapeId++;
                    break;
            }
            Assert.That(PlayerStateHash.Compute(predicted), Is.Not.EqualTo(PlayerStateHash.Compute(authority)));
            var inputs = new TickBuffer<PlayerInputCommand>(8);
            var states = new TickBuffer<PlayerState>(8);
            inputs.Store(0, Input(0));
            states.Store(authority.Tick, predicted);
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
        [TestCase(5)]
        [TestCase(6)]
        public void EveryGrappleRule_ChangesWorldFingerprint(int field)
        {
            var c = Config.Grapple;
            var rules = new PlayerGrappleConfig(c.Range + (field == 0 ? Fix.Epsilon : Fix.Zero),
                c.PullAcceleration + (field == 1 ? Fix.Epsilon : Fix.Zero), c.ReelSpeed + (field == 2 ? Fix.Epsilon : Fix.Zero),
                c.MaximumSpeed + (field == 3 ? Fix.Epsilon : Fix.Zero), c.CameraInfluence + (field == 4 ? Fix.Epsilon : Fix.Zero),
                c.BreakDistance + (field == 5 ? Fix.Epsilon : Fix.Zero), c.OriginHeight + (field == 6 ? Fix.Epsilon : Fix.Zero));
            var config = new PlayerSimulationConfig(Fix.FromInt(6), Fix.FromInt(30), Fix.FromInt(20), Fix.FromInt(8), grapple: rules);
            var world = new FixedWorld(null);
            Assert.That(new SimulationWorldContext(world, config, SimulationStep.Step50Hz).Identity.ConfigurationHash,
                Is.Not.EqualTo(new SimulationWorldContext(world, Config, SimulationStep.Step50Hz).Identity.ConfigurationHash));
        }

        [Test]
        public void DefaultZeroRules_AreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerSimulationConfig(
                Fix.FromInt(6), Fix.FromInt(30), Fix.FromInt(20), Fix.FromInt(8), grapple: default(PlayerGrappleConfig)));
        }

        [Test]
        public void FastSwing_StillStopsAtCapsuleWall()
        {
            var anchor = new FixedWorldPlane(1, 8, FixedSurface.Solid | FixedSurface.Grapple,
                FixedAxis.Z, Fix.FromInt(10), positiveNormal: false);
            var side = new FixedWorldPlane(2, 8, FixedSurface.Solid, FixedAxis.X, Fix.One, positiveNormal: false);
            var world = new FixedCharacterCollisionWorld(new FixedWorld(null, new[] { anchor, side }));
            var state = Initial;
            state.VelocityFixed = FixVec3.Right * Fix.FromInt(100);
            for (int i = 0; i < 5; i++)
            {
                state = Step(state, world: world);
                Assert.That(state.PositionFixed.X, Is.LessThanOrEqualTo(Fix.Half));
            }
        }

        [Test]
        public void GrappleReplay_MatchesGolden()
        {
            Assert.That(DeterministicGrappleScenario.Run(), Is.EqualTo(DeterministicGrappleScenario.ExpectedHash));
        }
    }
}

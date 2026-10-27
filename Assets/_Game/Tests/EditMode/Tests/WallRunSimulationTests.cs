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
    public sealed class WallRunSimulationTests
    {
        private static readonly PlayerSimulationConfig Config =
            new PlayerSimulationConfig(Fix.FromInt(6), Fix.FromInt(30), Fix.FromInt(20), Fix.FromInt(8));

        private static FixedCharacterCollisionWorld World(bool tagged = true, int mask = ~0)
        {
            var wall = new FixedWorldBox(1, 8, FixedSurface.Solid | (tagged ? FixedSurface.Wall : FixedSurface.Ground),
                new FixedAabb(new FixVec3(Fix.FromInt(2), Fix.Zero, Fix.FromInt(-100)),
                    new FixVec3(Fix.FromInt(3), Fix.FromInt(40), Fix.FromInt(100))));
            var ground = new FixedWorldPlane(2, 8, FixedSurface.Solid | FixedSurface.Ground, FixedAxis.Y, Fix.Zero);
            return new FixedCharacterCollisionWorld(new FixedWorld(new[] { wall }, new[] { ground }), mask);
        }

        private static PlayerState Initial => new PlayerState
        {
            PositionFixed = new FixVec3(Fix.FromRatio(3, 2), Fix.FromInt(20), Fix.Zero),
            WallNormalFixed = -FixVec3.Right,
            GroundJumpConsumed = true
        };

        private static PlayerInputCommand Forward(uint tick = 1) => new PlayerInputCommand
        {
            Tick = tick, MoveY = 1f, SprintHeld = true
        };

        private static PlayerState Step(in PlayerState previous, PlayerInputCommand? input = null,
            IFixedCharacterCollisionWorld world = null) => PlayerSimulation.Simulate(previous,
                input ?? Forward(unchecked(previous.Tick + 1)), Config, SimulationStep.Step50Hz, world ?? World());

        [TestCase(0)]
        [TestCase(180)]
        public void WallRun_UsesViewAlignedTangentAndReducedGravityWithoutPenetrating(int yaw)
        {
            var input = Forward();
            input.LookYaw = yaw;
            var state = Step(Initial, input);
            Assert.That(state.IsWallRunning, Is.True);
            Assert.That(state.IsSprinting, Is.False);
            Assert.That(state.WallRunUsedFixed, Is.EqualTo(SimulationStep.Step50Hz));
            Assert.That(state.StaminaSpentFixed, Is.GreaterThan(Fix.Zero));
            Assert.That(state.VelocityFixed.Y, Is.EqualTo(-Config.GravityFixed * Config.WallRun.GravityMultiplier * SimulationStep.Step50Hz));
            Assert.That(state.PositionFixed.X, Is.LessThanOrEqualTo(Fix.FromRatio(3, 2)));
            Assert.That(yaw == 0 ? state.VelocityFixed.Z > Fix.Zero : state.VelocityFixed.Z < Fix.Zero, Is.True);
        }

        [Test]
        public void FallSpeed_IsCappedDuringWallRun()
        {
            var previous = Initial;
            previous.VelocityFixed = -FixVec3.Up * Fix.FromInt(100);
            var state = Step(previous);
            Assert.That(state.IsWallRunning, Is.True);
            Assert.That(state.VelocityFixed.Y, Is.EqualTo(-Config.WallRun.MaximumFallSpeed));
            Assert.That(state.PositionFixed.Y, Is.GreaterThan(Fix.FromInt(19)));
        }

        [Test]
        public void InheritedDashMomentum_IsCappedHorizontallyButKeepsUpwardJump()
        {
            var previous = Initial;
            previous.VelocityFixed = FixVec3.Forward * Fix.FromInt(16) + FixVec3.Up * Fix.FromInt(8);
            var state = Step(previous);
            Assert.That(state.IsWallRunning, Is.True);
            var horizontal = new FixVec3(state.VelocityFixed.X, Fix.Zero, state.VelocityFixed.Z);
            Assert.That(horizontal.Magnitude, Is.LessThanOrEqualTo(Config.WallRun.Speed + Fix.Epsilon));
            Assert.That(state.VelocityFixed.Y, Is.GreaterThan(Fix.FromInt(7)));
        }

        [Test]
        public void SameFlightBudget_ExpiresAndReleaseCannotRefreshIt()
        {
            var state = Initial;
            var world = World();
            for (int i = 0; i < 85; i++)
            {
                state = Step(state, world: world);
            }
            Assert.That(state.IsGrounded, Is.False);
            Assert.That(state.IsWallRunning, Is.False);
            Assert.That(state.WallRunUsedFixed, Is.EqualTo(Config.WallRun.Duration));
            var released = Forward(unchecked(state.Tick + 1));
            released.SprintHeld = false;
            state = Step(state, released, world);
            state = Step(state, world: world);
            Assert.That(state.IsWallRunning, Is.False);
            Assert.That(state.WallRunUsedFixed, Is.EqualTo(Config.WallRun.Duration));
        }

        [Test]
        public void GroundContact_ResetsAirBudget()
        {
            var previous = Initial;
            previous.PositionFixed = new FixVec3(previous.PositionFixed.X, Fix.Zero, Fix.Zero);
            previous.VelocityFixed = -FixVec3.Up;
            previous.WallRunUsedFixed = Config.WallRun.Duration;
            var input = Forward();
            input.SprintHeld = false;
            var state = Step(previous, input);
            Assert.That(state.IsGrounded, Is.True);
            Assert.That(state.WallRunUsedFixed, Is.EqualTo(Fix.Zero));
            Assert.That(state.IsWallRunning, Is.False);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        public void WallRun_RejectsIncompatibleIntentOrState(int condition)
        {
            var previous = Initial;
            var input = Forward();
            switch (condition)
            {
                case 0:
                    input.SprintHeld = false;
                    break;
                case 1:
                    input.CrouchHeld = true;
                    break;
                case 2:
                    input.DashPressed = true;
                    break;
                case 3:
                    input.JumpPressed = true;
                    break;
                case 4:
                    previous.IsGrounded = true;
                    break;
                case 5:
                    previous.SprintExhausted = true;
                    break;
                case 6:
                    input.MoveY = 0f;
                    break;
                case 7:
                    input.LookYaw = 90f;
                    break;
            }
            Assert.That(Step(previous, input).IsWallRunning, Is.False);
        }

        [Test]
        public void WallRun_RequiresWallTagVisibleThroughSessionMaskAndQueryBackend()
        {
            Assert.That(Step(Initial, world: World(false)).IsWallRunning, Is.False);
            Assert.That(Step(Initial, world: World(mask: 0)).IsWallRunning, Is.False);
            Assert.That(Step(Initial, world: FixedFlatGroundCollisionWorld.Instance).IsWallRunning, Is.False);
        }

        [Test]
        public void UntaggedSolidOccludesTaggedWallBehindIt()
        {
            var blocker = new FixedWorldBox(1, 8, FixedSurface.Solid,
                new FixedAabb(new FixVec3(Fix.FromInt(2), Fix.Zero, Fix.FromInt(-10)),
                    new FixVec3(Fix.FromRatio(41, 20), Fix.FromInt(40), Fix.FromInt(10))));
            var tagged = new FixedWorldBox(2, 8, FixedSurface.Solid | FixedSurface.Wall,
                new FixedAabb(new FixVec3(Fix.FromRatio(21, 10), Fix.Zero, Fix.FromInt(-10)),
                    new FixVec3(Fix.FromInt(3), Fix.FromInt(40), Fix.FromInt(10))));
            var world = new FixedCharacterCollisionWorld(new FixedWorld(new[] { tagged, blocker }));
            Assert.That(Step(Initial, world: world).IsWallRunning, Is.False);
        }

        [TestCase(-1, false)]
        [TestCase(0, true)]
        public void StaminaGate_IncludesExactPerTickCostAndLatchesExhaustion(int offsetRaw, bool allowed)
        {
            var previous = Initial;
            Fix cost = Config.WallRun.DrainPerSecond * SimulationStep.Step50Hz;
            previous.StaminaSpentFixed = Fix.One - cost - Fix.FromRaw(offsetRaw);
            var state = Step(previous);
            Assert.That(state.IsWallRunning, Is.EqualTo(allowed));
            Assert.That(state.SprintExhausted, Is.True);
            if (allowed)
            {
                Assert.That(state.StaminaSpentFixed, Is.EqualTo(Fix.One));
            }
            else
            {
                Assert.That(Step(state).IsWallRunning, Is.False);
            }
        }

        [Test]
        public void Authority_TransportsRunAndBudgetAndRejectsInvalidCombinations()
        {
            var state = Step(Initial);
            var bytes = new byte[AuthoritativePlayerStateCodec.PacketSize];
            Assert.That(AuthoritativePlayerStateCodec.TrySerialize(new AuthoritativePlayerState(10, state.Tick, state),
                1, new ArraySegment<byte>(bytes), out int count), Is.True);
            Assert.That(count, Is.EqualTo(115));
            Assert.That(AuthoritativePlayerStateCodec.TryDeserialize(new ArraySegment<byte>(bytes), out _, out var decoded), Is.True);
            Assert.That(decoded.State.RawEquals(state), Is.True);
            bytes[114] = 128;
            Assert.That(AuthoritativePlayerStateCodec.TryDeserialize(new ArraySegment<byte>(bytes), out _, out _), Is.False);
            state.IsCrouched = true;
            Assert.That(AuthoritativePlayerStateCodec.TrySerialize(new AuthoritativePlayerState(10, state.Tick, state),
                1, new ArraySegment<byte>(bytes), out _), Is.False);
        }

        [TestCase(0)]
        [TestCase(1)]
        public void WallRunOnlyDrift_TriggersRollbackAcrossWrap(int field)
        {
            var authority = Initial;
            authority.Tick = uint.MaxValue;
            var predicted = authority;
            if (field == 0)
            {
                predicted.IsWallRunning = true;
            }
            else
            {
                predicted.WallRunUsedFixed = Fix.Epsilon;
            }
            Assert.That(PlayerStateHash.Compute(predicted), Is.Not.EqualTo(PlayerStateHash.Compute(authority)));
            var inputs = new TickBuffer<PlayerInputCommand>(8);
            var states = new TickBuffer<PlayerState>(8);
            states.Store(authority.Tick, predicted);
            inputs.Store(0, Forward(0));
            var result = ClientPredictionReconciler.Reconcile(new AuthoritativePlayerState(10, authority.Tick, authority),
                0, Step(predicted), inputs, states, Config, SimulationStep.Step50Hz, World());
            Assert.That(result.Status, Is.EqualTo(ReconciliationStatus.Corrected));
            Assert.That(result.State.RawEquals(Step(authority)), Is.True);
        }

        private static PlayerWallRunConfig Rules(Fix[] values) => new PlayerWallRunConfig(
            values[0], values[1], values[2], values[3], values[4], values[5], values[6], values[7], values[8]);

        private static Fix[] Values()
        {
            var c = Config.WallRun;
            return new[] { c.Speed, c.Acceleration, c.Duration, c.GravityMultiplier, c.AdhesionSpeed,
                c.MaximumFallSpeed, c.DrainPerSecond, c.ProbeDistance, c.MinimumAlignment };
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        [TestCase(8)]
        public void EveryWallRunRule_ChangesFingerprintAndRejectsNegativeValues(int field)
        {
            var values = Values();
            values[field] += Fix.Epsilon;
            var changed = new PlayerSimulationConfig(Fix.FromInt(6), Fix.FromInt(30), Fix.FromInt(20),
                Fix.FromInt(8), wallRun: Rules(values));
            var world = new FixedWorld(null);
            Assert.That(new SimulationWorldContext(world, changed, SimulationStep.Step50Hz).Identity.ConfigurationHash,
                Is.Not.EqualTo(new SimulationWorldContext(world, Config, SimulationStep.Step50Hz).Identity.ConfigurationHash));
            values[field] = -Fix.Epsilon;
            Assert.Throws<ArgumentOutOfRangeException>(() => Rules(values));
        }

        [Test]
        public void DefaultZeroRules_AreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerSimulationConfig(
                Fix.FromInt(6), Fix.FromInt(30), Fix.FromInt(20), Fix.FromInt(8), wallRun: default(PlayerWallRunConfig)));
        }

        [Test]
        public void WallRunReplay_MatchesGolden()
        {
            Assert.That(DeterministicWallRunScenario.Run(), Is.EqualTo(DeterministicWallRunScenario.ExpectedHash));
        }
    }
}

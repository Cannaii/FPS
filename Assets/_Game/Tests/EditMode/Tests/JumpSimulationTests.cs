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
    public sealed class JumpSimulationTests
    {
        private sealed class EmptyWorld : IFixedCharacterCollisionWorld
        {
            public bool CapsuleCast(FixVec3 position, FixVec3 direction, Fix distance,
                in CharacterCollisionConfig config, out FixedCharacterCollisionHit hit)
            {
                hit = default;
                return false;
            }
        }

        private static PlayerSimulationConfig Config => new PlayerSimulationConfig(
            Fix.FromInt(6), Fix.FromInt(20), Fix.FromInt(20), Fix.FromInt(8));

        private static PlayerState Step(PlayerState state, bool jump = false, bool dash = false,
            IFixedCharacterCollisionWorld world = null)
        {
            var input = new PlayerInputCommand
            {
                Tick = unchecked(state.Tick + 1),
                JumpPressed = jump,
                DashPressed = dash
            };
            return PlayerSimulation.Simulate(state, input, Config, SimulationStep.Step50Hz,
                world ?? FixedFlatGroundCollisionWorld.Instance);
        }

        [Test]
        public void GroundJump_ClearsCoyoteThenAllowsExactlyOneAirJump()
        {
            var state = Step(new PlayerState { IsGrounded = true }, true);
            Fix launchVelocity = Config.JumpSpeedFixed - Config.GravityFixed * SimulationStep.Step50Hz;
            Assert.That(state.GroundJumpConsumed, Is.True);
            Assert.That(state.CoyoteRemainingFixed, Is.EqualTo(Fix.Zero));
            Assert.That(state.AirJumpsUsed, Is.EqualTo(0));
            Assert.That(state.VelocityFixed.Y, Is.EqualTo(launchVelocity));
            state = Step(state, true);
            Assert.That(state.AirJumpsUsed, Is.EqualTo(1));
            Assert.That(state.VelocityFixed.Y, Is.EqualTo(launchVelocity));
            state = Step(state, true);
            Assert.That(state.AirJumpsUsed, Is.EqualTo(1));
            Assert.That(state.VelocityFixed.Y, Is.EqualTo(launchVelocity - Config.GravityFixed * SimulationStep.Step50Hz));
        }

        [TestCase(1, 0)]
        [TestCase(4, 0)]
        [TestCase(5, 1)]
        public void WalkOff_CoyoteBeforeExpiryDoesNotConsumeAirJump(int elapsedTicks, int expectedUsed)
        {
            var state = new PlayerState
            {
                Tick = uint.MaxValue - 2,
                IsGrounded = true,
                PositionFixed = FixVec3.Up * Fix.FromInt(5)
            };
            for (int i = 0; i < elapsedTicks; i++)
            {
                state = Step(state, world: new EmptyWorld());
            }
            state = Step(state, true, world: new EmptyWorld());
            Assert.That(state.AirJumpsUsed, Is.EqualTo(expectedUsed));
            Assert.That(state.GroundJumpConsumed, Is.True);
            Assert.That(state.CoyoteRemainingFixed, Is.EqualTo(Fix.Zero));
            Assert.That(state.VelocityFixed.Y, Is.GreaterThan(Fix.Zero));
        }

        [TestCase(0, false)]
        [TestCase(1, true)]
        public void CoyoteBoundary_ZeroIsExpiredOneRawIsAvailable(int raw, bool allowed)
        {
            var state = new PlayerState { CoyoteRemainingFixed = Fix.FromRaw(raw) };
            var input = new PlayerInputCommand { JumpPressed = true };
            Assert.That(PlayerJumpSimulation.TryJump(ref state, input,
                new PlayerJumpConfig(Fix.Half, 0), false), Is.EqualTo(allowed));
        }

        [Test]
        public void DashPriority_DoesNotSpendOrQueueJump()
        {
            var state = Step(new PlayerState { IsGrounded = true }, true, true);
            Assert.That(state.AirJumpsUsed, Is.EqualTo(0));
            Assert.That(state.VelocityFixed.Y, Is.EqualTo(Fix.Zero));
            for (int i = 0; i < 9; i++)
            {
                state = Step(state);
            }
            Assert.That(state.IsGrounded, Is.True);
            Assert.That(state.PositionFixed.Y, Is.EqualTo(Fix.Zero));
            Assert.That(state.AirJumpsUsed, Is.EqualTo(0));
        }

        [Test]
        public void Landing_ResetsResourcesAndAllowsNextGroundAndAirJump()
        {
            var state = Step(Step(new PlayerState { IsGrounded = true }, true), true);
            for (int i = 0; i < 100 && !state.IsGrounded; i++)
            {
                state = Step(state);
            }
            Assert.That(state.IsGrounded, Is.True);
            Assert.That(state.AirJumpsUsed, Is.EqualTo(0));
            Assert.That(state.GroundJumpConsumed, Is.False);
            Assert.That(state.CoyoteRemainingFixed, Is.EqualTo(Config.Jump.CoyoteDuration));
            state = Step(state, true);
            Assert.That(state.AirJumpsUsed, Is.EqualTo(0));
            state = Step(state, true);
            Assert.That(state.AirJumpsUsed, Is.EqualTo(1));
        }

        [Test]
        public void FallingAirJump_ReplacesDownwardVelocityAndDoesNotConsumeStamina()
        {
            var state = new PlayerState
            {
                PositionFixed = FixVec3.Up * Fix.FromInt(5),
                VelocityFixed = -FixVec3.Up * Fix.FromInt(30)
            };
            state = Step(state, true);
            Assert.That(state.VelocityFixed.Y, Is.GreaterThan(Fix.Zero));
            Assert.That(state.AirJumpsUsed, Is.EqualTo(1));
            Assert.That(state.StaminaSpentFixed, Is.EqualTo(Fix.Zero));
        }

        [Test]
        public void CeilingContact_DoesNotResetAirJumpResources()
        {
            var ceiling = new FixedWorldBox(1, 0, FixedSurface.Solid, new FixedAabb(
                new FixVec3(Fix.FromInt(-5), Fix.FromInt(3), Fix.FromInt(-5)),
                new FixVec3(Fix.FromInt(5), Fix.FromInt(4), Fix.FromInt(5))));
            var context = new SimulationWorldContext(new FixedWorld(new[] { ceiling }), Config, SimulationStep.Step50Hz);
            var state = new PlayerState { PositionFixed = FixVec3.Up * Fix.Half };
            state = Step(state, true, world: context.CollisionWorld);
            for (int i = 0; i < 5; i++)
            {
                state = Step(state, world: context.CollisionWorld);
            }
            Assert.That(state.IsGrounded, Is.False);
            Assert.That(state.AirJumpsUsed, Is.EqualTo(1));
            Assert.That(state.GroundJumpConsumed, Is.True);
            Assert.That(state.VelocityFixed.Y, Is.LessThanOrEqualTo(Fix.Zero));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void JumpOnlyError_TriggersRollbackAcrossTickWrap(int field)
        {
            var authority = new PlayerState
            {
                Tick = uint.MaxValue,
                PositionFixed = FixVec3.Up * Fix.FromInt(5)
            };
            var predicted = authority;
            switch (field)
            {
                case 0:
                    predicted.CoyoteRemainingFixed = Fix.Epsilon;
                    break;
                case 1:
                    predicted.GroundJumpConsumed = true;
                    break;
                case 2:
                    predicted.AirJumpsUsed = 1;
                    break;
            }
            Assert.That(PlayerStateHash.Compute(predicted), Is.Not.EqualTo(PlayerStateHash.Compute(authority)));
            var states = new TickBuffer<PlayerState>(8);
            var inputs = new TickBuffer<PlayerInputCommand>(8);
            states.Store(authority.Tick, predicted);
            inputs.Store(0, new PlayerInputCommand { Tick = 0, JumpPressed = true });
            var current = Step(predicted, true);
            var result = ClientPredictionReconciler.Reconcile(new AuthoritativePlayerState(10, authority.Tick, authority),
                0, current, inputs, states, Config, SimulationStep.Step50Hz, FixedFlatGroundCollisionWorld.Instance);
            Assert.That(result.Status, Is.EqualTo(ReconciliationStatus.Corrected));
            Assert.That(result.State.RawEquals(Step(authority, true)), Is.True);
        }

        [Test]
        public void Authority_RoundtripsCoyoteAndAirJumpAndRejectsContradictions()
        {
            var bytes = new byte[AuthoritativePlayerStateCodec.PacketSize];
            foreach (var state in new[]
            {
                new PlayerState { CoyoteRemainingFixed = Fix.FromRaw(1234) },
                new PlayerState { GroundJumpConsumed = true, AirJumpsUsed = 1 }
            })
            {
                Assert.That(AuthoritativePlayerStateCodec.TrySerialize(new AuthoritativePlayerState(10, 0, state),
                    1, new ArraySegment<byte>(bytes), out int count), Is.True);
                Assert.That(count, Is.EqualTo(152));
                Assert.That(AuthoritativePlayerStateCodec.TryDeserialize(new ArraySegment<byte>(bytes),
                    out _, out var authority), Is.True);
                Assert.That(authority.State.RawEquals(state), Is.True);
            }
            bytes[74] = 9;
            Assert.That(AuthoritativePlayerStateCodec.TryDeserialize(new ArraySegment<byte>(bytes), out _, out _), Is.False);
            bytes[74] = 1;
            bytes[48] = 0;
            Assert.That(AuthoritativePlayerStateCodec.TryDeserialize(new ArraySegment<byte>(bytes), out _, out _), Is.False);
            var invalid = new PlayerState { GroundJumpConsumed = true, CoyoteRemainingFixed = Fix.Epsilon };
            Assert.That(AuthoritativePlayerStateCodec.TrySerialize(new AuthoritativePlayerState(10, 0, invalid),
                1, new ArraySegment<byte>(bytes), out _), Is.False);
        }

        [TestCase(0)]
        [TestCase(1)]
        public void EveryJumpRule_ChangesWorldFingerprint(int field)
        {
            var changed = new PlayerJumpConfig(Config.Jump.CoyoteDuration + (field == 0 ? Fix.Epsilon : Fix.Zero),
                Config.Jump.ExtraAirJumps + (field == 1 ? 1 : 0));
            var config = new PlayerSimulationConfig(Fix.FromInt(6), Fix.FromInt(20),
                Fix.FromInt(20), Fix.FromInt(8), jump: changed);
            var world = new FixedWorld(null);
            Assert.That(new SimulationWorldContext(world, config, SimulationStep.Step50Hz).Identity.ConfigurationHash,
                Is.Not.EqualTo(new SimulationWorldContext(world, Config, SimulationStep.Step50Hz).Identity.ConfigurationHash));
        }

        [TestCase(-1, 0)]
        [TestCase(32769, 0)]
        [TestCase(0, -1)]
        [TestCase(0, 9)]
        public void InvalidConfig_IsRejected(int raw, int extraJumps)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerJumpConfig(Fix.FromRaw(raw), extraJumps));
        }

        [Test]
        public void CapabilityReplay_MatchesGoldenHash()
        {
            Assert.That(DeterministicJumpScenario.Run(), Is.EqualTo(DeterministicJumpScenario.ExpectedHash));
        }
    }
}

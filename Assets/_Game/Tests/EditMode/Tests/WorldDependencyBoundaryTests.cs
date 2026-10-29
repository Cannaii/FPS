using System;
using System.Reflection;
using AFPS.Core.Collections;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Prediction;
using AFPS.NetCode.Sessions;
using AFPS.NetCode.Simulation;
using AFPS.NetCode.Transport;
using AFPS.Simulation.Characters;
using AFPS.Simulation.Characters.Collision;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class WorldDependencyBoundaryTests
    {
        [Test]
        public void PublicSimulationAndReplayApisDoNotOfferWorldlessCompatibility()
        {
            foreach (var method in typeof(PlayerSimulation).GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (method.Name == "Simulate")
                {
                    Assert.That(method.GetParameters().Length, Is.GreaterThanOrEqualTo(5));
                    Assert.That(method.GetParameters()[4].IsOptional, Is.False);
                }
            }
            foreach (var method in typeof(ClientPredictionReconciler).GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (method.Name == "Reconcile")
                {
                    var parameters = method.GetParameters();
                    Assert.That(parameters[parameters.Length - 1].IsOptional, Is.False);
                }
            }
        }

        [Test]
        public void ExplicitNullIsRejected_WhileOptInFlatGroundKeepsCanonicalBackend()
        {
            var state = new PlayerState { IsGrounded = true };
            var config = new PlayerSimulationConfig(6f, 20f, 20f, 8f);
            Assert.Throws<ArgumentNullException>(() => LegacyCharacterCollisionWorldAdapter.Adapt(null));
            Assert.That(LegacyCharacterCollisionWorldAdapter.Adapt(FlatGroundCollisionWorld.Instance),
                Is.SameAs(FixedFlatGroundCollisionWorld.Instance));
            Assert.Throws<ArgumentNullException>(() => PlayerSimulation.Simulate(state, default, config,
                0.02f, (ICharacterCollisionWorld)null));
            Assert.Throws<ArgumentNullException>(() => ClientPredictionReconciler.Reconcile(
                new AuthoritativePlayerState(0, 0, state), 0, state,
                new TickBuffer<PlayerInputCommand>(8), new TickBuffer<PlayerState>(8), config,
                0.02f, 0f, 0f, null));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void MissingWorldInSessionAssemblyFailsImmediately(int kind)
        {
            LocalGameTransport.CreatePair(out var server, out var client);
            try
            {
                var state = new PlayerState { IsGrounded = true };
                var config = new PlayerSimulationConfig(6f, 20f, 20f, 8f);
                // 刻意不传world：这些入口必须在构造时失败，不能到首Tick才偷偷选择平地。
                Assert.Throws<ArgumentNullException>(() =>
                {
                    switch (kind)
                    {
                        case 0:
                            new ClientPredictedMovementSession(client, new TransportConnectionId(1), state, config,
                                0.02f, 64, 3, AuthoritativePlayerStateCodec.RecommendedPositionErrorThreshold,
                                AuthoritativePlayerStateCodec.RecommendedVelocityErrorThreshold);
                            break;
                        case 1:
                            new ServerAuthoritativeMovementSession(server, new TransportConnectionId(1), state, config,
                                0.02f, 16);
                            break;
                        case 2:
                            new SimulatedAuthoritativeServer(state, config, 0.02f, 0, 0);
                            break;
                        case 3:
                            new NetworkMovementSessionManager(server, client, state, state, config, 0.02f,
                                64, 3, 16, 2, 2, AuthoritativePlayerStateCodec.RecommendedPositionErrorThreshold,
                                AuthoritativePlayerStateCodec.RecommendedVelocityErrorThreshold);
                            break;
                    }
                });
            }
            finally
            {
                server.Dispose();
                client.Dispose();
            }
        }
    }
}

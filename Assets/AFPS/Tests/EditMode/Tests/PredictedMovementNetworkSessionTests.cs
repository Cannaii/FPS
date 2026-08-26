using System;
using System.Threading;
using AFPS.NetCode.InputReplication;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Prediction;
using AFPS.NetCode.Sessions;
using AFPS.NetCode.StateReplication;
using AFPS.NetCode.Transport;
using AFPS.Simulation.Characters;
using AFPS.Transport.Unity;
using NUnit.Framework;
using UnityEngine;

namespace AFPS.Tests.EditMode
{
    public sealed class PredictedMovementNetworkSessionTests
    {
        private const ushort TestPort = 47993;
        private const int PumpLimit = 1000;

        [Test]
        public void ClientAheadOfServer_CorrectsAcknowledgedTickAndReplaysToCurrentTickThroughUdp()
        {
            using (var serverTransport = new UnityGameTransport())
            using (var clientTransport = new UnityGameTransport())
            {
                Assert.That(serverTransport.TryStartServer(TestPort, 2, out string serverError), Is.True, serverError);
                Assert.That(clientTransport.TryStartClient("127.0.0.1", TestPort, out string clientError), Is.True, clientError);
                byte[] serverReceiveBuffer = new byte[256];
                byte[] clientReceiveBuffer = new byte[256];
                TransportConnectionId serverConnection = default;
                TransportConnectionId clientConnection = default;
                PumpUntil(serverTransport, clientTransport, () =>
                {
                    DrainConnected(serverTransport, serverReceiveBuffer, ref serverConnection);
                    DrainConnected(clientTransport, clientReceiveBuffer, ref clientConnection);
                    return serverConnection.IsValid && clientConnection.IsValid;
                });

                PlayerState initialState = new PlayerState { Tick = 0, Position = Vector3.zero, Velocity = Vector3.zero, IsGrounded = true };
                PlayerSimulationConfig clientConfig = new PlayerSimulationConfig(6f, 20f, 20f, 8f);
                PlayerSimulationConfig serverConfig = new PlayerSimulationConfig(6f, 10f, 20f, 8f);
                ClientPredictedMovementSession clientSession = new ClientPredictedMovementSession(clientTransport, clientConnection, initialState, clientConfig, 0.02f, 64, 3, AuthoritativePlayerStateCodec.RecommendedPositionErrorThreshold, AuthoritativePlayerStateCodec.RecommendedVelocityErrorThreshold);
                ServerAuthoritativeMovementSession serverSession = new ServerAuthoritativeMovementSession(serverTransport, serverConnection, initialState, serverConfig, 0.02f, 64);

                for (uint tick = 1; tick <= 3; tick++)
                {
                    PlayerInputCommand command = new PlayerInputCommand { Tick = tick, MoveY = 1f };
                    clientSession.PredictAndSend(command, out InputBatchSendResult sendResult);
                    Assert.That(sendResult.Succeeded, Is.True);
                }
                Assert.That(clientSession.CurrentState.Tick, Is.EqualTo(3));

                bool serverReceivedInput = false;
                PumpUntil(serverTransport, clientTransport, () =>
                {
                    while (serverTransport.TryPollEvent(new ArraySegment<byte>(serverReceiveBuffer), out GameTransportEvent transportEvent))
                    {
                        if (transportEvent.Type == TransportEventType.Data)
                        {
                            serverReceivedInput |= serverSession.TryReceiveInputPacket(new ArraySegment<byte>(serverReceiveBuffer, 0, transportEvent.PayloadLength), out _);
                        }
                    }

                    return serverReceivedInput;
                });

                Assert.That(serverSession.TryAdvance(10, out AuthoritativePlayerState sentState, out AuthoritativeStateSendResult stateSendResult), Is.True);
                Assert.That(sentState.LastProcessedInputTick, Is.EqualTo(1));
                Assert.That(stateSendResult.Succeeded, Is.True);

                ReconciliationResult reconciliation = default;
                bool clientReceivedState = false;
                PumpUntil(serverTransport, clientTransport, () =>
                {
                    while (clientTransport.TryPollEvent(new ArraySegment<byte>(clientReceiveBuffer), out GameTransportEvent transportEvent))
                    {
                        if (transportEvent.Type == TransportEventType.Data)
                        {
                            clientReceivedState |= clientSession.TryReceiveAuthoritativePacket(new ArraySegment<byte>(clientReceiveBuffer, 0, transportEvent.PayloadLength), out _, out reconciliation);
                        }
                    }

                    return clientReceivedState;
                });

                Assert.That(reconciliation.Status, Is.EqualTo(ReconciliationStatus.Corrected));
                Assert.That(reconciliation.ReplayedTickCount, Is.EqualTo(2));
                Assert.That(clientSession.CurrentState.Tick, Is.EqualTo(3));
                Assert.That(clientSession.CurrentState.Tick, Is.Not.EqualTo(sentState.LastProcessedInputTick));
            }
        }

        [Test]
        public void ClientSession_RejectsThresholdBelowProtocolQuantizationNoise()
        {
            FakeTransport transport = new FakeTransport();
            PlayerState initialState = new PlayerState { Tick = 0 };
            PlayerSimulationConfig config = new PlayerSimulationConfig(6f, 20f, 20f, 8f);

            Assert.Throws<ArgumentOutOfRangeException>(() => new ClientPredictedMovementSession(transport, new TransportConnectionId(1), initialState, config, 0.02f, 64, 3, 0f, AuthoritativePlayerStateCodec.RecommendedVelocityErrorThreshold));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ClientPredictedMovementSession(transport, new TransportConnectionId(1), initialState, config, 0.02f, 64, 3, AuthoritativePlayerStateCodec.RecommendedPositionErrorThreshold, 0f));
        }

        [Test]
        public void ServerSession_WaitsThenUsesBoundedFallbackAndRecoversOnRealInput()
        {
            FakeTransport transport = new FakeTransport();
            PlayerState initialState = new PlayerState { Tick = 0, IsGrounded = true };
            PlayerSimulationConfig config = new PlayerSimulationConfig(6f, 20f, 20f, 8f);
            ServerAuthoritativeMovementSession session = new ServerAuthoritativeMovementSession(transport, new TransportConnectionId(1), initialState, config, 0.02f, 16, 2, 1);

            Assert.That(session.TryReceiveInputPacket(new ArraySegment<byte>(CreateInputPacket(1, new PlayerInputCommand { Tick = 1, MoveY = 1f, JumpPressed = true })), out _), Is.True);
            Assert.That(session.TryReceiveInputPacket(new ArraySegment<byte>(CreateInputPacket(2, new PlayerInputCommand { Tick = 4, MoveX = 1f })), out _), Is.True);

            Assert.That(session.TryAdvance(10, out _, out _), Is.True);
            Assert.That(session.LastAdvanceStatus, Is.EqualTo(ServerInputAdvanceStatus.ReceivedInput));
            Assert.That(session.LastAppliedInput.Tick, Is.EqualTo(1));

            Assert.That(session.TryAdvance(11, out _, out _), Is.False);
            Assert.That(session.LastAdvanceStatus, Is.EqualTo(ServerInputAdvanceStatus.WaitingForInput));
            Assert.That(session.MissingInputWaitTicks, Is.EqualTo(1));
            Assert.That(session.TryAdvance(12, out _, out _), Is.False);
            Assert.That(session.MissingInputWaitTicks, Is.EqualTo(2));

            Assert.That(session.TryAdvance(13, out AuthoritativePlayerState repeatedState, out _), Is.True);
            Assert.That(session.LastAdvanceStatus, Is.EqualTo(ServerInputAdvanceStatus.RepeatedContinuousInput));
            Assert.That(session.LastAppliedInput.Tick, Is.EqualTo(2));
            Assert.That(session.LastAppliedInput.MoveY, Is.EqualTo(1f));
            Assert.That(session.LastAppliedInput.JumpPressed, Is.False);
            Assert.That(repeatedState.LastProcessedInputTick, Is.EqualTo(2));

            Assert.That(session.TryAdvance(14, out AuthoritativePlayerState neutralState, out _), Is.True);
            Assert.That(session.LastAdvanceStatus, Is.EqualTo(ServerInputAdvanceStatus.NeutralFallback));
            Assert.That(session.LastAppliedInput.Tick, Is.EqualTo(3));
            Assert.That(session.LastAppliedInput.MoveX, Is.Zero);
            Assert.That(session.LastAppliedInput.MoveY, Is.Zero);
            Assert.That(neutralState.LastProcessedInputTick, Is.EqualTo(3));

            Assert.That(session.TryAdvance(15, out _, out _), Is.True);
            Assert.That(session.LastAdvanceStatus, Is.EqualTo(ServerInputAdvanceStatus.ReceivedInput));
            Assert.That(session.LastAppliedInput.Tick, Is.EqualTo(4));
            Assert.That(session.LastAppliedInput.MoveX, Is.EqualTo(1f));
            Assert.That(session.MissingInputWaitTicks, Is.Zero);
            Assert.That(session.ConsecutiveSubstitutedInputTicks, Is.Zero);
        }

        [Test]
        public void ServerSession_UsesNeutralFallbackWhenNoPreviousInputExists()
        {
            FakeTransport transport = new FakeTransport();
            PlayerState initialState = new PlayerState { Tick = 0, IsGrounded = true };
            PlayerSimulationConfig config = new PlayerSimulationConfig(6f, 20f, 20f, 8f);
            ServerAuthoritativeMovementSession session = new ServerAuthoritativeMovementSession(transport, new TransportConnectionId(1), initialState, config, 0.02f, 16, 0, 5);

            Assert.That(session.TryAdvance(1, out AuthoritativePlayerState state, out _), Is.True);
            Assert.That(session.LastAdvanceStatus, Is.EqualTo(ServerInputAdvanceStatus.NeutralFallback));
            Assert.That(session.LastAppliedInput.Tick, Is.EqualTo(1));
            Assert.That(session.LastAppliedInput.MoveX, Is.Zero);
            Assert.That(session.LastAppliedInput.MoveY, Is.Zero);
            Assert.That(session.LastAppliedInput.JumpPressed, Is.False);
            Assert.That(state.LastProcessedInputTick, Is.EqualTo(1));
        }

        private static byte[] CreateInputPacket(uint sequence, params PlayerInputCommand[] commands)
        {
            byte[] packet = new byte[InputCommandBatchCodec.GetPacketSize(commands.Length)];
            InputCommandBatch batch = new InputCommandBatch(new ArraySegment<PlayerInputCommand>(commands));
            Assert.That(InputCommandBatchCodec.TrySerialize(batch, sequence, new ArraySegment<byte>(packet), out _), Is.True);
            return packet;
        }

        private static void PumpUntil(UnityGameTransport server, UnityGameTransport client, Func<bool> condition)
        {
            for (int i = 0; i < PumpLimit; i++)
            {
                server.Pump();
                client.Pump();
                if (condition())
                {
                    return;
                }

                Thread.Sleep(1);
            }

            Assert.Fail("在限定时间内没有完成预测移动网络会话步骤。");
        }

        private static void DrainConnected(UnityGameTransport transport, byte[] receiveBuffer, ref TransportConnectionId connectionId)
        {
            while (transport.TryPollEvent(new ArraySegment<byte>(receiveBuffer), out GameTransportEvent transportEvent))
            {
                if (transportEvent.Type == TransportEventType.Connected)
                {
                    connectionId = transportEvent.ConnectionId;
                }
            }
        }

        private sealed class FakeTransport : IGameTransport
        {
            public bool IsRunning => true;
            public TransportRole Role => TransportRole.Client;

            public bool TryStartServer(ushort port, int maxConnections, out string error)
            {
                error = null;
                return false;
            }

            public bool TryStartClient(string address, ushort port, out string error)
            {
                error = null;
                return false;
            }

            public void Pump()
            {
            }

            public bool TryPollEvent(ArraySegment<byte> receiveBuffer, out GameTransportEvent transportEvent)
            {
                transportEvent = default;
                return false;
            }

            public TransportSendResult Send(TransportConnectionId connectionId, TransportDelivery delivery, ArraySegment<byte> payload) => TransportSendResult.Success;

            public void Disconnect(TransportConnectionId connectionId)
            {
            }

            public void Stop()
            {
            }

            public void Dispose()
            {
            }
        }
    }
}

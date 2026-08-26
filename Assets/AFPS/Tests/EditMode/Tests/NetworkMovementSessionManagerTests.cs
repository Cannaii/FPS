using System;
using System.Collections.Generic;
using AFPS.NetCode.InputReplication;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Prediction;
using AFPS.NetCode.Runtime;
using AFPS.NetCode.Sessions;
using AFPS.NetCode.StateReplication;
using AFPS.NetCode.Transport;
using AFPS.Simulation.Characters;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class NetworkMovementSessionManagerTests
    {
        [Test]
        public void ConnectionLifecycle_CreatesAndRemovesExpectedSessions()
        {
            FakeTransport serverTransport = new FakeTransport(TransportRole.Server);
            FakeTransport clientTransport = new FakeTransport(TransportRole.Client);
            NetworkMovementSessionManager manager = CreateManager(serverTransport, clientTransport);
            TransportConnectionId serverConnection = new TransportConnectionId(10);
            TransportConnectionId clientConnection = new TransportConnectionId(20);

            Assert.That(manager.HandleConnected(NetworkTransportSide.Server, serverConnection), Is.True);
            Assert.That(manager.HandleConnected(NetworkTransportSide.Client, clientConnection), Is.True);
            Assert.That(manager.ServerSessionCount, Is.EqualTo(1));
            Assert.That(manager.ClientSession, Is.Not.Null);
            Assert.That(manager.HandleDisconnected(NetworkTransportSide.Server, serverConnection), Is.True);
            Assert.That(manager.HandleDisconnected(NetworkTransportSide.Client, clientConnection), Is.True);
            Assert.That(manager.ServerSessionCount, Is.Zero);
            Assert.That(manager.ClientSession, Is.Null);
        }

        [Test]
        public void RoutedInputAndState_DriveAuthorityAndClientReconciliation()
        {
            FakeTransport serverTransport = new FakeTransport(TransportRole.Server);
            FakeTransport clientTransport = new FakeTransport(TransportRole.Client);
            NetworkMovementSessionManager manager = CreateManager(serverTransport, clientTransport);
            TransportConnectionId serverConnection = new TransportConnectionId(10);
            TransportConnectionId clientConnection = new TransportConnectionId(20);
            manager.HandleConnected(NetworkTransportSide.Server, serverConnection);
            manager.HandleConnected(NetworkTransportSide.Client, clientConnection);

            PlayerInputCommand command = new PlayerInputCommand { Tick = 1, MoveY = 1f };
            Assert.That(manager.TryPredictAndSend(command, out PlayerState predictedState, out InputBatchSendResult inputSend), Is.True);
            Assert.That(inputSend.Succeeded, Is.True);
            Assert.That(predictedState.Tick, Is.EqualTo(1));
            Assert.That(manager.TryHandleData(NetworkTransportSide.Server, serverConnection, clientTransport.SentPackets[0], out _), Is.True);

            Assert.That(manager.AdvanceServerSessions(100), Is.EqualTo(1));
            Assert.That(serverTransport.SentPackets, Has.Count.EqualTo(1));
            Assert.That(manager.TryHandleData(NetworkTransportSide.Client, clientConnection, serverTransport.SentPackets[0], out ReconciliationResult reconciliation), Is.True);
            Assert.That(reconciliation.Status, Is.EqualTo(ReconciliationStatus.NoCorrection));
            Assert.That(manager.ClientSession.CurrentState.Tick, Is.EqualTo(1));
        }

        [Test]
        public void DataRouting_RejectsWrongMessageSideAndUnknownConnection()
        {
            FakeTransport serverTransport = new FakeTransport(TransportRole.Server);
            FakeTransport clientTransport = new FakeTransport(TransportRole.Client);
            NetworkMovementSessionManager manager = CreateManager(serverTransport, clientTransport);
            TransportConnectionId serverConnection = new TransportConnectionId(10);
            TransportConnectionId clientConnection = new TransportConnectionId(20);
            manager.HandleConnected(NetworkTransportSide.Server, serverConnection);
            manager.HandleConnected(NetworkTransportSide.Client, clientConnection);
            manager.TryPredictAndSend(new PlayerInputCommand { Tick = 1 }, out _, out _);

            ArraySegment<byte> inputPacket = clientTransport.SentPackets[0];
            Assert.That(manager.TryHandleData(NetworkTransportSide.Client, clientConnection, inputPacket, out _), Is.False);
            Assert.That(manager.TryHandleData(NetworkTransportSide.Server, new TransportConnectionId(999), inputPacket, out _), Is.False);
        }

        private static NetworkMovementSessionManager CreateManager(IGameTransport serverTransport, IGameTransport clientTransport)
        {
            PlayerState initialState = new PlayerState { Tick = 0, IsGrounded = true };
            PlayerSimulationConfig config = new PlayerSimulationConfig(6f, 20f, 20f, 8f);
            return new NetworkMovementSessionManager(serverTransport, clientTransport, initialState, initialState, config, 0.02f, 64, 3, 16, 2, 2, AuthoritativePlayerStateCodec.RecommendedPositionErrorThreshold, AuthoritativePlayerStateCodec.RecommendedVelocityErrorThreshold);
        }

        private sealed class FakeTransport : IGameTransport
        {
            public readonly List<ArraySegment<byte>> SentPackets = new List<ArraySegment<byte>>();
            public bool IsRunning => true;
            public TransportRole Role { get; }

            public FakeTransport(TransportRole role)
            {
                Role = role;
            }

            public bool TryStartServer(ushort port, int maxConnections, out string error) { error = null; return false; }
            public bool TryStartClient(string address, ushort port, out string error) { error = null; return false; }
            public void Pump() { }
            public bool TryPollEvent(ArraySegment<byte> receiveBuffer, out GameTransportEvent transportEvent) { transportEvent = default; return false; }

            public TransportSendResult Send(TransportConnectionId connectionId, TransportDelivery delivery, ArraySegment<byte> payload)
            {
                byte[] copy = new byte[payload.Count];
                Array.Copy(payload.Array, payload.Offset, copy, 0, payload.Count);
                SentPackets.Add(new ArraySegment<byte>(copy));
                return TransportSendResult.Success;
            }

            public void Disconnect(TransportConnectionId connectionId) { }
            public void Stop() { }
            public void Dispose() { }
        }
    }
}

using System;
using System.Threading;
using AFPS.NetCode.Messages;
using AFPS.NetCode.StateReplication;
using AFPS.NetCode.Transport;
using AFPS.Simulation.Characters;
using AFPS.Transport.Unity;
using NUnit.Framework;
using UnityEngine;

namespace AFPS.Tests.EditMode
{
    public sealed class AuthoritativeStateReplicationTests
    {
        private const ushort TestPort = 47992;
        private const int PumpLimit = 1000;

        [Test]
        public void Sender_UsesUnreliableSequencedAndAdvancesSequenceOnlyAfterSuccess()
        {
            FakeTransport transport = new FakeTransport { SendResult = TransportSendResult.TransportError };
            ServerAuthoritativeStateSender sender = new ServerAuthoritativeStateSender(transport, new TransportConnectionId(4), 20);
            AuthoritativePlayerState state = CreateState(100, 50);

            Assert.That(sender.TrySend(state, out AuthoritativeStateSendResult failed), Is.False);
            Assert.That(failed.PacketSequence, Is.EqualTo(20));
            transport.SendResult = TransportSendResult.Success;
            Assert.That(sender.TrySend(state, out AuthoritativeStateSendResult succeeded), Is.True);
            Assert.That(succeeded.PacketSequence, Is.EqualTo(20));
            Assert.That(succeeded.PacketBytes, Is.EqualTo(AuthoritativePlayerStateCodec.PacketSize));
            Assert.That(transport.LastDelivery, Is.EqualTo(TransportDelivery.UnreliableSequenced));

            Assert.That(AuthoritativePlayerStateCodec.TryDeserialize(new ArraySegment<byte>(transport.LastPayload), out _, out AuthoritativePlayerState decoded), Is.True);
            Assert.That(decoded.ServerTick, Is.EqualTo(100));
            Assert.That(decoded.LastProcessedInputTick, Is.EqualTo(50));
            Assert.That(sender.TrySend(state, out AuthoritativeStateSendResult next), Is.True);
            Assert.That(next.PacketSequence, Is.EqualTo(21));
        }

        [Test]
        public void Receiver_RejectsDuplicateAndRegressiveState()
        {
            ClientAuthoritativeStateReceiver receiver = new ClientAuthoritativeStateReceiver();
            Assert.That(Receive(receiver, CreatePacket(10, 100, 50), out AuthoritativeStateReceiveResult accepted), Is.True);
            Assert.That(accepted.Status, Is.EqualTo(AuthoritativeStateReceiveStatus.Accepted));

            Assert.That(Receive(receiver, CreatePacket(10, 100, 50), out AuthoritativeStateReceiveResult duplicate), Is.False);
            Assert.That(duplicate.Status, Is.EqualTo(AuthoritativeStateReceiveStatus.StaleOrDuplicateSequence));

            Assert.That(Receive(receiver, CreatePacket(11, 99, 51), out AuthoritativeStateReceiveResult serverRegression), Is.False);
            Assert.That(serverRegression.Status, Is.EqualTo(AuthoritativeStateReceiveStatus.RegressiveServerTick));

            Assert.That(Receive(receiver, CreatePacket(11, 101, 49), out AuthoritativeStateReceiveResult acknowledgementRegression), Is.False);
            Assert.That(acknowledgementRegression.Status, Is.EqualTo(AuthoritativeStateReceiveStatus.RegressiveInputAcknowledgement));

            Assert.That(Receive(receiver, CreatePacket(11, 101, 51), out AuthoritativeStateReceiveResult newer), Is.True);
            Assert.That(newer.Status, Is.EqualTo(AuthoritativeStateReceiveStatus.Accepted));
        }

        [Test]
        public void Receiver_AllowsSequenceAndTickWrapAround()
        {
            ClientAuthoritativeStateReceiver receiver = new ClientAuthoritativeStateReceiver();
            Assert.That(Receive(receiver, CreatePacket(uint.MaxValue, uint.MaxValue, uint.MaxValue), out _), Is.True);
            Assert.That(Receive(receiver, CreatePacket(0, 0, 0), out _), Is.True);
        }

        [Test]
        public void StatePacket_TravelsThroughRealUnityTransportLoopback()
        {
            using (var server = new UnityGameTransport())
            using (var client = new UnityGameTransport())
            {
                Assert.That(server.TryStartServer(TestPort, 2, out string serverError), Is.True, serverError);
                Assert.That(client.TryStartClient("127.0.0.1", TestPort, out string clientError), Is.True, clientError);

                byte[] receiveBuffer = new byte[128];
                TransportConnectionId serverConnection = default;
                TransportConnectionId clientConnection = default;
                PumpUntil(server, client, () =>
                {
                    DrainConnected(server, receiveBuffer, ref serverConnection);
                    DrainConnected(client, receiveBuffer, ref clientConnection);
                    return serverConnection.IsValid && clientConnection.IsValid;
                });

                ServerAuthoritativeStateSender sender = new ServerAuthoritativeStateSender(server, serverConnection, 7);
                Assert.That(sender.TrySend(CreateState(200, 80), out _), Is.True);

                ClientAuthoritativeStateReceiver receiver = new ClientAuthoritativeStateReceiver();
                AuthoritativePlayerState receivedState = default;
                PumpUntil(server, client, () =>
                {
                    while (client.TryPollEvent(new ArraySegment<byte>(receiveBuffer), out GameTransportEvent transportEvent))
                    {
                        if (transportEvent.Type == TransportEventType.Data && receiver.TryReceivePacket(new ArraySegment<byte>(receiveBuffer, 0, transportEvent.PayloadLength), out receivedState, out _))
                        {
                            return true;
                        }
                    }

                    return false;
                });

                Assert.That(receivedState.ServerTick, Is.EqualTo(200));
                Assert.That(receivedState.LastProcessedInputTick, Is.EqualTo(80));
                Assert.That(receivedState.State.Position.x, Is.EqualTo(2f));
            }
        }

        private static AuthoritativePlayerState CreateState(uint serverTick, uint inputTick)
        {
            PlayerState state = new PlayerState
            {
                Tick = inputTick,
                Position = new Vector3(2f, 0f, 3f),
                Velocity = new Vector3(1f, 0f, 0f),
                IsGrounded = true
            };
            return new AuthoritativePlayerState(serverTick, inputTick, state);
        }

        private static byte[] CreatePacket(uint sequence, uint serverTick, uint inputTick)
        {
            byte[] packet = new byte[AuthoritativePlayerStateCodec.PacketSize];
            Assert.That(AuthoritativePlayerStateCodec.TrySerialize(CreateState(serverTick, inputTick), sequence, new ArraySegment<byte>(packet), out _), Is.True);
            return packet;
        }

        private static bool Receive(ClientAuthoritativeStateReceiver receiver, byte[] packet, out AuthoritativeStateReceiveResult result)
        {
            return receiver.TryReceivePacket(new ArraySegment<byte>(packet), out _, out result);
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

            Assert.Fail("在限定时间内没有收到预期的权威状态网络事件。");
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
            public TransportRole Role => TransportRole.Server;
            public TransportSendResult SendResult = TransportSendResult.Success;
            public TransportDelivery LastDelivery;
            public byte[] LastPayload;

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

            public TransportSendResult Send(TransportConnectionId connectionId, TransportDelivery delivery, ArraySegment<byte> payload)
            {
                LastDelivery = delivery;
                LastPayload = new byte[payload.Count];
                Array.Copy(payload.Array, payload.Offset, LastPayload, 0, payload.Count);
                return SendResult;
            }

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

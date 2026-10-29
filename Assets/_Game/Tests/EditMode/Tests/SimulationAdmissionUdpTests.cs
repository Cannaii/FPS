using System;
using System.Threading;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.Runtime;
using AFPS.NetCode.Sessions;
using AFPS.NetCode.Transport;
using AFPS.Simulation.Characters;
using AFPS.Transport.Unity;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class SimulationAdmissionUdpTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void ActualUdpAdmitsMatchingWorldAndRejectsDifferentWorld(bool mismatch)
        {
            using (var serverTransport = new UnityGameTransport())
            using (var clientTransport = new UnityGameTransport())
            {
                Assert.That(serverTransport.TryStartServer(47995, 2, out string serverError), Is.True, serverError);
                Assert.That(clientTransport.TryStartClient("127.0.0.1", 47995, out string clientError), Is.True, clientError);
                var server = SimulationAdmissionTests.CreateManager(serverTransport, null,
                    SimulationAdmissionTests.Context(), timeout: 500, retry: 10);
                var client = SimulationAdmissionTests.CreateManager(null, clientTransport,
                    SimulationAdmissionTests.Context(mismatch ? SimulationCompatibility.WorldMismatch : SimulationCompatibility.Compatible),
                    timeout: 500, retry: 10);
                byte[] buffer = new byte[2048];
                TransportConnectionId serverId = default;
                TransportConnectionId clientId = default;
                bool completed = false;
                for (uint tick = 0; tick < 1000; tick++)
                {
                    serverTransport.Pump();
                    clientTransport.Pump();
                    Drain(serverTransport, server, NetworkTransportSide.Server, buffer, ref serverId);
                    Drain(clientTransport, client, NetworkTransportSide.Client, buffer, ref clientId);
                    completed = mismatch
                        ? client.ClientAdmissionStatus == ConnectionAdmissionStatus.WorldMismatch
                        : server.ServerSessionCount == 1 && client.RemotePlayers.LocalEntityId != 0;
                    if (completed)
                    {
                        break;
                    }

                    server.AdvanceServerSessions(tick);
                    client.AdvanceServerSessions(tick);
                    Thread.Sleep(1);
                }

                Assert.That(completed, Is.True, "限定时间内未完成真实 UTP 准入。");
                if (mismatch)
                {
                    Assert.That(server.ServerSessionCount, Is.Zero);
                    Assert.That(client.ClientSession, Is.Null);
                    return;
                }

                Assert.That(client.TryPredictAndSend(new PlayerInputCommand { Tick = 1, MoveY = 1f }, out _, out _), Is.True);
                bool inputArrived = false;
                for (int i = 0; i < 1000; i++)
                {
                    clientTransport.Pump();
                    serverTransport.Pump();
                    while (serverTransport.TryPollEvent(new ArraySegment<byte>(buffer), out GameTransportEvent transportEvent))
                    {
                        if (transportEvent.Type == TransportEventType.Data)
                        {
                            var packet = new ArraySegment<byte>(buffer, 0, transportEvent.PayloadLength);
                            bool handled = server.TryHandleData(NetworkTransportSide.Server, transportEvent.ConnectionId, packet, out _);
                            inputArrived |= handled && PacketHeaderCodec.TryRead(packet, out PacketHeader header)
                                && header.MessageType == NetworkMessageType.InputCommandBatch;
                        }
                    }

                    if (inputArrived)
                    {
                        break;
                    }

                    Thread.Sleep(1);
                }

                Assert.That(inputArrived, Is.True);
                Assert.That(server.AdvanceServerSessions(2000), Is.EqualTo(1));
                bool authorityReceived = false;
                for (int i = 0; i < 1000; i++)
                {
                    serverTransport.Pump();
                    clientTransport.Pump();
                    authorityReceived |= Drain(clientTransport, client, NetworkTransportSide.Client, buffer, ref clientId);
                    server.TryGetServerSession(serverId, out ServerAuthoritativePlayerSession session);
                    if (authorityReceived && session.CurrentState.RawEquals(client.ClientSession.CurrentState))
                    {
                        Assert.That(session.CurrentState.IsGrounded, Is.True);
                        return;
                    }

                    Thread.Sleep(1);
                }

                Assert.Fail("准入后的真实 UDP 输入/权威状态未收敛。");
            }
        }

        private static bool Drain(
            UnityGameTransport transport,
            NetworkSessionCoordinator manager,
            NetworkTransportSide side,
            byte[] buffer,
            ref TransportConnectionId id)
        {
            bool authorityReceived = false;
            while (transport.TryPollEvent(new ArraySegment<byte>(buffer), out GameTransportEvent transportEvent))
            {
                switch (transportEvent.Type)
                {
                    case TransportEventType.Connected:
                        id = transportEvent.ConnectionId;
                        manager.HandleConnected(side, id);
                        break;
                    case TransportEventType.Data:
                        var packet = new ArraySegment<byte>(buffer, 0, transportEvent.PayloadLength);
                        bool handled = manager.TryHandleData(side, transportEvent.ConnectionId, packet, out _);
                        authorityReceived |= handled && PacketHeaderCodec.TryRead(packet, out PacketHeader header)
                            && header.MessageType == NetworkMessageType.AuthoritativeMovementState;
                        break;
                    case TransportEventType.Disconnected:
                        manager.HandleDisconnected(side, transportEvent.ConnectionId);
                        break;
                }
            }

            return authorityReceived;
        }
    }
}

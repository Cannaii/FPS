using System;
using AFPS.NetCode.Match;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.Runtime;
using AFPS.NetCode.Sessions;
using AFPS.NetCode.Transport;
using NUnit.Framework;
using static AFPS.Tests.EditMode.SimulationAdmissionTests;

namespace AFPS.Tests.EditMode
{
    public sealed class LobbySessionTests
    {
        [Test]
        public void AdmittedClientsCannotMoveOrStartBeforeBothReadyAndFullCountdown()
        {
            var test = new Harness();
            test.Step(0);
            Assert.That(test.Server.ServerDeathmatchPhase, Is.EqualTo(DeathmatchPhase.WaitingForPlayers));
            Assert.That(test.Clients[0].CanEnterMatch, Is.False);
            test.Server.TryGetServerSession(test.ServerIds[0], out var authority);
            var position = authority.CurrentState.PositionFixed;
            Assert.That(test.Clients[0].TrySetLobbyReady(true), Is.True);
            test.Step(1);
            Assert.That(test.Clients[0].ClientLobby.Phase, Is.EqualTo(LobbyPhase.Waiting));
            var input = new AFPS.Simulation.Characters.PlayerInputCommand { Tick = 2, MoveX = 1f, FirePressed = true, ShotSequence = 1 };
            test.Server.TryHandleData(NetworkTransportSide.Server, test.ServerIds[0], InputPacket(input), out _);
            test.Step(2);
            Assert.That(authority.CurrentState.PositionFixed, Is.EqualTo(position));
            Assert.That(test.Clients[1].TrySetLobbyReady(true), Is.True);
            test.Step(3);
            Assert.That(test.Clients[0].ClientLobby.Phase, Is.EqualTo(LobbyPhase.Countdown));
            Assert.That(test.Clients[0].LobbyCountdownSeconds, Is.EqualTo(3));
            test.Step(152);
            Assert.That(test.Server.ServerDeathmatchPhase, Is.EqualTo(DeathmatchPhase.WaitingForPlayers));
            test.Step(153);
            Assert.That(test.Server.ServerDeathmatchPhase, Is.EqualTo(DeathmatchPhase.Playing));
            Assert.That(test.Clients[0].CanEnterMatch && test.Clients[1].CanEnterMatch, Is.True);
        }

        [Test]
        public void CancellationRestartsFullCountdownAndDisconnectRemovesReadiness()
        {
            var test = new Harness();
            test.Step(0);
            test.Clients[0].TrySetLobbyReady(true);
            test.Clients[1].TrySetLobbyReady(true);
            test.Step(1);
            test.Step(100);
            test.Clients[0].TrySetLobbyReady(false);
            test.Step(101);
            Assert.That(test.Clients[1].ClientLobby.Phase, Is.EqualTo(LobbyPhase.Waiting));
            test.Clients[0].TrySetLobbyReady(true);
            test.Step(102);
            Assert.That(test.Clients[1].ClientLobby.RemainingTicks, Is.EqualTo(150));
            test.Server.HandleDisconnected(NetworkTransportSide.Server, test.ServerIds[0]);
            test.Step(103);
            Assert.That(test.Clients[1].ClientLobby.Phase, Is.EqualTo(LobbyPhase.Waiting));
            Assert.That(test.Server.RetainedServerSessionCount, Is.EqualTo(1));
            Assert.That(test.Server.ServerDeathmatchPhase, Is.EqualTo(DeathmatchPhase.WaitingForPlayers));
        }

        [Test]
        public void LostReadyAndSnapshotRetryUntilExactAuthoritativeSequenceAcknowledgement()
        {
            var test = new Harness();
            test.Step(0);
            test.ClientTransports[0].FailType = NetworkMessageType.LobbyReady;
            test.Clients[0].TrySetLobbyReady(true);
            Assert.That(test.Clients[0].LobbyReadyPending, Is.True);
            test.ServerTransport.FailType = NetworkMessageType.LobbySnapshot;
            test.Step(1);
            Assert.That(test.Clients[0].LobbyReadyPending, Is.True);
            test.Step(2);
            Assert.That(test.Clients[0].LobbyReadyPending, Is.False);
            test.ClientTransports[0].FailType = NetworkMessageType.LobbyReady;
            test.Clients[0].TrySetLobbyReady(false);
            test.ClientTransports[0].FailType = NetworkMessageType.LobbyReady;
            test.Clients[0].TrySetLobbyReady(true);
            test.ClientTransports[0].FailType = NetworkMessageType.LobbyReady;
            test.Step(20);
            Assert.That(test.Clients[0].TryGetLocalLobbyPlayer(out var old), Is.True);
            Assert.That(old.Ready, Is.True);
            Assert.That(old.ReadySequence, Is.EqualTo(1));
            Assert.That(test.Clients[0].LobbyReadyPending, Is.True, "相同布尔值的旧确认不能清掉第3条意图。");
            test.Step(21);
            Assert.That(test.Clients[0].LobbyReadyPending, Is.False);
            test.Clients[0].TryGetLocalLobbyPlayer(out var confirmed);
            Assert.That(confirmed.ReadySequence, Is.EqualTo(3));
        }

        [Test]
        public void PacketWrapperRejectsWrongConnectionOldVersionAndConflictingHeaderSequence()
        {
            var test = new Harness();
            test.Step(0);
            test.Clients[0].TryGetLocalLobbyPlayer(out var player);
            var command = new LobbyReadyCommand(test.Clients[0].ClientLobby.RoundEpoch, player.MembershipEpoch, 1, true);
            var bytes = new byte[LobbyMessageCodec.ReadyPacketSize];
            Assert.That(LobbyMessageCodec.TryWriteReady(command, new ArraySegment<byte>(bytes)), Is.True);
            Assert.That(test.Server.TryHandleData(NetworkTransportSide.Server,
                test.ServerIds[1], new ArraySegment<byte>(bytes), out _), Is.False);
            bytes[4] = 26;
            Assert.That(LobbyMessageCodec.TryReadReady(new ArraySegment<byte>(bytes), out _), Is.False);
            bytes[4] = PacketHeader.CurrentProtocolVersion;
            bytes[8] = 2;
            Assert.That(LobbyMessageCodec.TryReadReady(new ArraySegment<byte>(bytes), out _), Is.False);
        }

        private sealed class Harness
        {
            internal readonly FakeTransport ServerTransport = new FakeTransport(TransportRole.Server);
            internal readonly FakeTransport[] ClientTransports = { new FakeTransport(TransportRole.Client), new FakeTransport(TransportRole.Client) };
            internal readonly NetworkMovementSessionManager Server;
            internal readonly NetworkMovementSessionManager[] Clients = new NetworkMovementSessionManager[2];
            internal readonly TransportConnectionId[] ServerIds = { new TransportConnectionId(10), new TransportConnectionId(11) };
            private readonly TransportConnectionId[] clientIds = { new TransportConnectionId(20), new TransportConnectionId(21) };

            internal Harness()
            {
                var context = Context();
                var state = new AFPS.Simulation.Characters.PlayerState
                {
                    PositionFixed = context.SessionSpatial.SpawnOrigin,
                    IsGrounded = true
                };
                Server = new NetworkMovementSessionManager(ServerTransport, null, state, state,
                    context.Configuration, 0.02f, 64, 3, 16, 2, 2, 0, 0, simulationWorld: context);
                for (int index = 0; index < 2; index++)
                {
                    Clients[index] = new NetworkMovementSessionManager(null, ClientTransports[index], state, state,
                        context.Configuration, 0.02f, 64, 3, 16, 2, 2, 0, 0, simulationWorld: context);
                    Server.HandleConnected(NetworkTransportSide.Server, ServerIds[index]);
                    Clients[index].HandleConnected(NetworkTransportSide.Client, clientIds[index]);
                }
                Flush();
            }

            internal void Step(uint tick)
            {
                foreach (var client in Clients)
                {
                    client.AdvanceServerSessions(tick);
                }
                Flush();
                Server.AdvanceServerSessions(tick);
                Flush();
            }

            private void Flush()
            {
                for (int pass = 0; pass < 32; pass++)
                {
                    bool any = false;
                    for (int index = 0; index < 2; index++)
                    {
                        while (ClientTransports[index].Pending.Count > 0)
                        {
                            any = true;
                            Server.TryHandleData(NetworkTransportSide.Server, ServerIds[index],
                                ClientTransports[index].Pending.Dequeue().Data, out _);
                        }
                    }
                    while (ServerTransport.Pending.Count > 0)
                    {
                        any = true;
                        var packet = ServerTransport.Pending.Dequeue();
                        int target = packet.Connection == ServerIds[0] ? 0 : 1;
                        Clients[target].TryHandleData(NetworkTransportSide.Client, clientIds[target], packet.Data, out _);
                    }
                    if (!any)
                    {
                        return;
                    }
                }
                Assert.Fail("协议队列未收敛。");
            }
        }
    }
}

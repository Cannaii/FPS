using System;
using System.Diagnostics;
using System.Threading;
using AFPS.NetCode.Runtime;
using AFPS.NetCode.Sessions;
using AFPS.NetCode.Transport;
using AFPS.Simulation.Characters;
using AFPS.Transport.Unity;
using NUnit.Framework;
using static AFPS.Tests.EditMode.SimulationAdmissionTests;

namespace AFPS.Tests.EditMode
{
    public sealed class UnitySessionRecoveryTests
    {
        [Test]
        public void RealUtpDisconnectAndExplicitRedial_RecoversSameEntitySessionAndAcceptsNewInput()
        {
            using (var pair = new Loopback())
            {
                pair.Until(() => pair.Client.ClientResumeCredential.IsValid && pair.Client.CanPredictClientInput);
                uint entity = pair.Client.RemotePlayers.LocalEntityId;
                Assert.That(pair.Server.TryGetServerSession(pair.ServerId, out var authority), Is.True);
                uint tick = unchecked(pair.Client.ClientSession.CurrentState.Tick + 1);
                pair.Client.TryPredictAndSend(new PlayerInputCommand { Tick = tick, MoveY = 1f }, out _, out _);
                pair.Until(() => authority.CurrentState.Tick == tick
                    && pair.Client.ClientSession.LastAuthoritativeState.Tick == tick);
                pair.ServerTransport.Disconnect(pair.ServerId);
                // 本地主动Disconnect由调用方同步撤销会话，远端通过真实UTP断线事件获知。
                pair.Server.HandleDisconnected(NetworkTransportSide.Server, pair.ServerId);
                pair.Until(() => pair.Client.ClientSession == null && pair.Server.ServerSessionCount == 0);
                Assert.That(pair.Server.RetainedServerSessionCount, Is.EqualTo(1));
                Assert.That(authority.IsRetainedOffline, Is.True);
                var retainedPosition = authority.CurrentState.PositionFixed;

                // 本步明确重拨，验证真实驱动及协议；Unity界面的自动重拨调度留11C-2。
                pair.ClientTransport.Stop();
                Assert.That(pair.ClientTransport.TryStartClient("127.0.0.1", Loopback.Port, out string error), Is.True, error);
                pair.Until(() => pair.Client.CanPredictClientInput && pair.Client.ClientResumeCredential.IsValid);
                Assert.That(pair.Client.RemotePlayers.LocalEntityId, Is.EqualTo(entity));
                Assert.That(pair.Server.TryGetServerSession(pair.ServerId, out var restored), Is.True);
                Assert.That(restored, Is.SameAs(authority));
                Assert.That(pair.Server.IsServerRecoveryPending(pair.ServerId), Is.False);
                Assert.That(pair.Server.RetainedServerSessionCount, Is.Zero);
                Assert.That(restored.CurrentState.PositionFixed.Z, Is.GreaterThanOrEqualTo(retainedPosition.Z));

                uint nextTick = unchecked(pair.Client.ClientSession.CurrentState.Tick + 1);
                Assert.That(pair.Client.TryPredictAndSend(new PlayerInputCommand { Tick = nextTick, MoveY = 1f },
                    out _, out _), Is.True);
                pair.Until(() => authority.CurrentState.Tick == nextTick
                    && pair.Client.ClientSession.LastAuthoritativeState.Tick == nextTick);
            }
        }

        private sealed class Loopback : IDisposable
        {
            internal const ushort Port = 47993;
            internal readonly UnityGameTransport ServerTransport = new UnityGameTransport();
            internal readonly UnityGameTransport ClientTransport = new UnityGameTransport();
            internal readonly NetworkMovementSessionManager Server;
            internal readonly NetworkMovementSessionManager Client;
            internal TransportConnectionId ServerId;
            private readonly byte[] receiveBuffer = new byte[2048];
            private readonly Stopwatch clock = Stopwatch.StartNew();
            private uint previousTick;

            internal Loopback()
            {
                try
                {
                    Assert.That(ServerTransport.TryStartServer(Port, 4, out string error), Is.True, error);
                    Assert.That(ClientTransport.TryStartClient("127.0.0.1", Port, out error), Is.True, error);
                    var context = Context();
                    Server = CreateManager(ServerTransport, null, context, timeout: 250, retry: 25);
                    Client = CreateManager(null, ClientTransport, context, timeout: 250, retry: 25);
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            internal void Until(Func<bool> condition)
            {
                long started = clock.ElapsedMilliseconds;
                while (clock.ElapsedMilliseconds - started < 6000)
                {
                    ServerTransport.Pump();
                    ClientTransport.Pump();
                    Drain(ServerTransport, Server, NetworkTransportSide.Server);
                    Drain(ClientTransport, Client, NetworkTransportSide.Client);
                    uint tick = (uint)(clock.ElapsedMilliseconds / 20);
                    if (tick != previousTick)
                    {
                        Server.AdvanceServerSessions(tick);
                        Client.AdvanceServerSessions(tick);
                        previousTick = tick;
                    }
                    if (condition())
                    {
                        return;
                    }
                    Thread.Sleep(1);
                }
                Assert.Fail("真实UTP恢复流程未在6秒内达到预期状态。");
            }

            private void Drain(UnityGameTransport transport, NetworkMovementSessionManager manager, NetworkTransportSide side)
            {
                while (transport.TryPollEvent(new ArraySegment<byte>(receiveBuffer), out var transportEvent))
                {
                    switch (transportEvent.Type)
                    {
                        case TransportEventType.Connected:
                            if (side == NetworkTransportSide.Server)
                            {
                                ServerId = transportEvent.ConnectionId;
                            }
                            manager.HandleConnected(side, transportEvent.ConnectionId);
                            break;
                        case TransportEventType.Disconnected:
                            manager.HandleDisconnected(side, transportEvent.ConnectionId);
                            break;
                        case TransportEventType.Data:
                            manager.TryHandleData(side, transportEvent.ConnectionId,
                                new ArraySegment<byte>(receiveBuffer, 0, transportEvent.PayloadLength), out _);
                            break;
                    }
                }
            }

            public void Dispose()
            {
                ClientTransport.Dispose();
                ServerTransport.Dispose();
            }
        }
    }
}

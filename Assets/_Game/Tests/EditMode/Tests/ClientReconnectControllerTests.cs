using System;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.Runtime;
using AFPS.NetCode.Sessions;
using AFPS.NetCode.Transport;
using AFPS.Simulation.World;
using NUnit.Framework;
using static AFPS.Tests.EditMode.SimulationAdmissionTests;

namespace AFPS.Tests.EditMode
{
    public sealed class ClientReconnectControllerTests
    {
        [Test]
        public void Disconnect_RedialsSameTransportAfterDelayAndCompletesOriginalSession()
        {
            using (var test = new Harness())
            {
                var transport = test.Runtime.ClientTransport;
                test.Server.TryGetServerSession(ServerId, out var original);
                test.Drop(1);
                test.Controller.Advance(1.49);
                Assert.That(test.Transport.Starts, Is.EqualTo(1));
                test.Controller.Advance(1.5);
                Assert.That(test.Transport.Starts, Is.EqualTo(2));
                Assert.That(test.Runtime.ClientTransport, Is.SameAs(transport));
                test.Connect(new TransportConnectionId(30), new TransportConnectionId(40));
                test.Server.AdvanceServerSessions(2);
                test.Flush();
                test.Client.AdvanceServerSessions(2);
                test.Flush();
                test.Controller.Advance(2);
                Assert.That(test.Controller.Status, Is.EqualTo(ClientReconnectStatus.Idle));
                Assert.That(test.Controller.CompletedRecoveryCount, Is.EqualTo(1));
                Assert.That(test.Client.RemotePlayers.LocalEntityId, Is.EqualTo(1));
                Assert.That(test.Server.TryGetServerSession(new TransportConnectionId(30), out var restored), Is.True);
                Assert.That(restored, Is.SameAs(original));
                Assert.That(test.Transport.Address, Is.EqualTo("192.0.2.10"));
                Assert.That(test.Transport.Port, Is.EqualTo(7777));
            }
        }

        [Test]
        public void RepeatedDisconnect_DoesNotExtendTotalBudget_AndFailureStopsAutomaticAttempts()
        {
            using (var test = new Harness())
            {
                test.Drop(1);
                test.Controller.Advance(1.5);
                test.Controller.NotifyDisconnected(ClientId, 2);
                test.Controller.Advance(2.5);
                test.Controller.Advance(26);
                Assert.That(test.Controller.Status, Is.EqualTo(ClientReconnectStatus.Failed));
                int starts = test.Transport.Starts;
                test.Controller.NotifyDisconnected(ClientId, 27);
                test.Controller.Advance(100);
                Assert.That(test.Transport.Starts, Is.EqualTo(starts));
                Assert.That(test.Controller.Error, Does.Contain("超时"));
                Assert.That(test.Client.ClientResumeCredential.IsValid, Is.True);
            }
        }

        [Test]
        public void AttemptTimeout_RetriesAfterDelayRatherThanResettingEveryFrame()
        {
            using (var test = new Harness())
            {
                test.Drop(1);
                test.Controller.Advance(1.5);
                test.Controller.Advance(6.49);
                Assert.That(test.Controller.Status, Is.EqualTo(ClientReconnectStatus.Connecting));
                test.Controller.Advance(6.5);
                Assert.That(test.Controller.Status, Is.EqualTo(ClientReconnectStatus.Waiting));
                test.Controller.Advance(6.99);
                Assert.That(test.Transport.Starts, Is.EqualTo(2));
                test.Controller.Advance(7);
                Assert.That(test.Transport.Starts, Is.EqualTo(3));
            }
        }

        [Test]
        public void Cancel_StopsDriverAndAutomaticRetries_ManualRetryRetainsCredential()
        {
            using (var test = new Harness())
            {
                var credential = test.Client.ClientResumeCredential;
                test.Drop(1);
                test.Controller.Cancel();
                test.Controller.Advance(30);
                Assert.That(test.Controller.Status, Is.EqualTo(ClientReconnectStatus.Cancelled));
                Assert.That(test.Transport.IsRunning, Is.False);
                Assert.That(test.Transport.Starts, Is.EqualTo(1));
                test.Controller.Retry(30);
                test.Controller.Advance(30.5);
                Assert.That(test.Transport.Starts, Is.EqualTo(2));
                Assert.That(test.Client.ClientResumeCredential.Equals(credential), Is.True);
            }
        }

        [Test]
        public void ExplicitNewJoin_ClearsCredentialAndCreatesNewEntityOnlyOnUserPath()
        {
            using (var test = new Harness())
            {
                test.Drop(1);
                test.Controller.JoinAsNewPlayer(2);
                Assert.That(test.Client.ClientResumeCredential.IsValid, Is.False);
                test.Controller.Advance(2.5);
                test.ClientWire.Sent.Clear();
                test.Connect(new TransportConnectionId(30), new TransportConnectionId(40));
                Assert.That(test.ClientWire.Sent[0].Type, Is.EqualTo(NetworkMessageType.SimulationHello));
                Assert.That(test.Client.RemotePlayers.LocalEntityId, Is.EqualTo(2));
                test.Controller.Advance(3);
                Assert.That(test.Controller.Status, Is.EqualTo(ClientReconnectStatus.Idle));
                Assert.That(test.Controller.CompletedRecoveryCount, Is.Zero);
                Assert.That(test.Server.RetainedServerSessionCount, Is.EqualTo(1));
            }
        }

        [Test]
        public void StartupFailure_IsThrottledAndDoesNotLoseCredentialsOrRuntimeMode()
        {
            using (var test = new Harness())
            {
                test.Drop(1);
                test.Transport.FailStart = true;
                test.Controller.Advance(1.5);
                test.Controller.Advance(1.6);
                Assert.That(test.Transport.Starts, Is.EqualTo(2));
                Assert.That(test.Controller.Status, Is.EqualTo(ClientReconnectStatus.Waiting));
                test.Controller.Advance(2);
                Assert.That(test.Transport.Starts, Is.EqualTo(3));
                Assert.That(test.Runtime.Mode, Is.EqualTo(NetworkLaunchMode.Client));
                Assert.That(test.Client.ClientResumeCredential.IsValid, Is.True);
            }
        }

        [Test]
        public void MissingCredential_FailsWithoutSilentlyAllocatingNewIdentity()
        {
            using (var test = new Harness(false))
            {
                test.Controller.NotifyDisconnected(ClientId, 1);
                test.Controller.Advance(2);
                Assert.That(test.Controller.Status, Is.EqualTo(ClientReconnectStatus.Failed));
                Assert.That(test.Transport.Starts, Is.EqualTo(1));
                Assert.That(test.Server.ServerSessionCount, Is.Zero);
            }
        }

        [Test]
        public void OldConnectionDisconnect_DoesNotInterruptCurrentConnection()
        {
            using (var test = new Harness())
            {
                test.Controller.NotifyDisconnected(new TransportConnectionId(999), 1);
                Assert.That(test.Controller.Status, Is.EqualTo(ClientReconnectStatus.Idle));
                Assert.That(test.Client.CanPredictClientInput, Is.True);
            }
        }

        [TestCase(-1)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void InvalidClock_IsRejected(double time)
        {
            using (var test = new Harness())
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => test.Controller.Advance(time));
            }
        }

        [Test]
        public void ClockRollback_IsRejected()
        {
            using (var test = new Harness())
            {
                test.Controller.Advance(2);
                Assert.Throws<ArgumentOutOfRangeException>(() => test.Controller.Advance(1));
            }
        }

        [Test]
        public void LiveSession_CannotForgetItsRecoveryIdentity()
        {
            using (var test = new Harness())
            {
                Assert.Throws<InvalidOperationException>(() => test.Client.ForgetClientRecovery());
                Assert.That(test.Client.ClientResumeCredential.IsValid, Is.True);
            }
        }

        [Test]
        public void IncompatibleWelcome_StopsRetryButDoesNotPoisonExplicitNewJoin()
        {
            using (var test = new Harness())
            {
                test.Drop(1);
                test.Controller.Advance(1.5);
                var newId = new TransportConnectionId(40);
                test.Client.HandleConnected(NetworkTransportSide.Client, newId);
                test.Controller.NotifyConnected(newId);
                var local = test.Client.WorldContext.Identity;
                var wrong = new SimulationWorldIdentity(local.AlgorithmVersion + 1, local.WorldHash, local.ConfigurationHash);
                Assert.That(test.Client.TryHandleData(NetworkTransportSide.Client, newId,
                    Handshake(NetworkMessageType.SimulationWelcome, 2, wrong, SimulationCompatibility.AlgorithmMismatch), out _), Is.True);
                test.Controller.NotifyDisconnected(newId, 2);
                Assert.That(test.Controller.Status, Is.EqualTo(ClientReconnectStatus.Failed));
                Assert.That(test.Controller.Error, Does.Contain("不一致"));
                test.Controller.JoinAsNewPlayer(3);
                test.Controller.Advance(3.5);
                Assert.That(test.Controller.Status, Is.EqualTo(ClientReconnectStatus.Connecting));
                test.ClientWire.Pending.Clear();
                test.ServerWire.Pending.Clear();
                test.Connect(new TransportConnectionId(31), new TransportConnectionId(41));
                test.Controller.Advance(4);
                Assert.That(test.Controller.Status, Is.EqualTo(ClientReconnectStatus.Idle));
                Assert.That(test.Client.RemotePlayers.LocalEntityId, Is.EqualTo(2));
            }
        }

        private sealed class Harness : IDisposable
        {
            internal readonly FakeTransport ServerWire = new FakeTransport(TransportRole.Server);
            internal readonly FakeTransport ClientWire = new FakeTransport(TransportRole.Client);
            internal readonly RestartTransport Transport;
            internal readonly GameNetworkRuntime Runtime;
            internal readonly NetworkMovementSessionManager Server;
            internal readonly NetworkMovementSessionManager Client;
            internal readonly ClientReconnectController Controller;
            private TransportConnectionId serverId = ServerId;
            private TransportConnectionId clientId = ClientId;

            internal Harness(bool connect = true)
            {
                Transport = new RestartTransport(ClientWire);
                Runtime = new GameNetworkRuntime(() => Transport);
                Assert.That(Runtime.TryStart(new NetworkLaunchOptions(NetworkLaunchMode.Client,
                    "192.0.2.10", 7777, 32), out _), Is.True);
                var context = Context();
                Server = CreateManager(ServerWire, null, context);
                Client = CreateManager(null, Transport, context);
                Controller = new ClientReconnectController(Runtime, Client);
                if (connect)
                {
                    Connect(ServerId, ClientId);
                }
            }

            internal void Connect(TransportConnectionId serverConnection, TransportConnectionId clientConnection)
            {
                serverId = serverConnection;
                clientId = clientConnection;
                Assert.That(Server.HandleConnected(NetworkTransportSide.Server, serverId), Is.True);
                Assert.That(Client.HandleConnected(NetworkTransportSide.Client, clientId), Is.True);
                Controller.NotifyConnected(clientId);
                Flush();
            }

            internal void Flush()
            {
                for (int pass = 0; pass < 32 && (ServerWire.Pending.Count != 0 || ClientWire.Pending.Count != 0); pass++)
                {
                    while (ClientWire.Pending.Count != 0)
                    {
                        Server.TryHandleData(NetworkTransportSide.Server, serverId, ClientWire.Pending.Dequeue().Data, out _);
                    }
                    while (ServerWire.Pending.Count != 0)
                    {
                        Client.TryHandleData(NetworkTransportSide.Client, clientId, ServerWire.Pending.Dequeue().Data, out _);
                    }
                }
            }

            internal void Drop(double time)
            {
                Server.HandleDisconnected(NetworkTransportSide.Server, serverId);
                Client.HandleDisconnected(NetworkTransportSide.Client, clientId);
                Controller.NotifyDisconnected(clientId, time);
                ServerWire.Pending.Clear();
                ClientWire.Pending.Clear();
            }

            public void Dispose() => Runtime.Dispose();
        }

        private sealed class RestartTransport : IGameTransport
        {
            private readonly FakeTransport wire;
            public bool IsRunning { get; private set; }
            public TransportRole Role => TransportRole.Client;
            internal int Starts;
            internal bool FailStart;
            internal string Address;
            internal ushort Port;

            internal RestartTransport(FakeTransport wire) => this.wire = wire;

            public bool TryStartClient(string address, ushort port, out string error)
            {
                Starts++;
                Address = address;
                Port = port;
                IsRunning = !FailStart;
                error = FailStart ? "测试启动失败" : null;
                return IsRunning;
            }

            public bool TryStartServer(ushort port, int maxConnections, out string error)
            {
                error = "非服务器";
                return false;
            }

            public TransportSendResult Send(TransportConnectionId id, TransportDelivery delivery, ArraySegment<byte> data)
            {
                return IsRunning ? wire.Send(id, delivery, data) : TransportSendResult.TransportError;
            }

            public bool TryPollEvent(ArraySegment<byte> buffer, out GameTransportEvent transportEvent)
            {
                transportEvent = default;
                return false;
            }

            public void Disconnect(TransportConnectionId id) => wire.Disconnect(id);
            public void Pump()
            {
            }
            public void Stop() => IsRunning = false;
            public void Dispose() => Stop();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Threading;
using AFPS.NetCode.Runtime;
using AFPS.NetCode.Transport;
using AFPS.Transport.Unity;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class GameNetworkRuntimeTests
    {
        private const ushort HostIntegrationTestPort = 47995;
        private const int PumpLimit = 1000;

        [Test]
        public void Client_StartsOnlyClientTransportWithConfiguredEndpoint()
        {
            FakeTransport client = new FakeTransport();
            using (var runtime = new GameNetworkRuntime(new QueueFactory(client).Create))
            {
                NetworkLaunchOptions options = new NetworkLaunchOptions(NetworkLaunchMode.Client, "192.0.2.10", 7777, 32);
                Assert.That(runtime.TryStart(options, out string error), Is.True, error);

                Assert.That(runtime.Mode, Is.EqualTo(NetworkLaunchMode.Client));
                Assert.That(runtime.ServerTransport, Is.Null);
                Assert.That(runtime.ClientTransport, Is.SameAs(client));
                Assert.That(client.StartedClientAddress, Is.EqualTo("192.0.2.10"));
                Assert.That(client.StartedPort, Is.EqualTo(7777));
            }

            Assert.That(client.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public void DedicatedServer_StartsOnlyServerTransport()
        {
            FakeTransport server = new FakeTransport();
            using (var runtime = new GameNetworkRuntime(new QueueFactory(server).Create))
            {
                NetworkLaunchOptions options = new NetworkLaunchOptions(NetworkLaunchMode.DedicatedServer, "ignored", 8000, 64);
                Assert.That(runtime.TryStart(options, out string error), Is.True, error);

                Assert.That(runtime.Mode, Is.EqualTo(NetworkLaunchMode.DedicatedServer));
                Assert.That(runtime.ServerTransport, Is.SameAs(server));
                Assert.That(runtime.ClientTransport, Is.Null);
                Assert.That(server.StartedPort, Is.EqualTo(8000));
                Assert.That(server.StartedMaxConnections, Is.EqualTo(64));
            }
        }

        [Test]
        public void Host_StartsSeparateServerAndLoopbackClient()
        {
            FakeTransport server = new FakeTransport();
            FakeTransport client = new FakeTransport();
            using (var runtime = new GameNetworkRuntime(new QueueFactory(server, client).Create))
            {
                NetworkLaunchOptions options = new NetworkLaunchOptions(NetworkLaunchMode.Host, "203.0.113.20", 9000, 16);
                Assert.That(runtime.TryStart(options, out string error), Is.True, error);

                Assert.That(runtime.ServerTransport, Is.SameAs(server));
                Assert.That(runtime.ClientTransport, Is.SameAs(client));
                Assert.That(server.Role, Is.EqualTo(TransportRole.Server));
                Assert.That(client.Role, Is.EqualTo(TransportRole.Client));
                Assert.That(client.StartedClientAddress, Is.EqualTo("127.0.0.1"));
                Assert.That(client.StartedPort, Is.EqualTo(9000));
            }
        }

        [Test]
        public void Host_ClientStartFailureCleansUpBothTransports()
        {
            FakeTransport server = new FakeTransport();
            FakeTransport client = new FakeTransport { ClientStartSucceeds = false };
            GameNetworkRuntime runtime = new GameNetworkRuntime(new QueueFactory(server, client).Create);
            NetworkLaunchOptions options = new NetworkLaunchOptions(NetworkLaunchMode.Host, "ignored", 9001, 16);

            Assert.That(runtime.TryStart(options, out string error), Is.False);
            Assert.That(error, Is.EqualTo("客户端启动失败。"));
            Assert.That(runtime.Mode, Is.EqualTo(NetworkLaunchMode.None));
            Assert.That(runtime.ServerTransport, Is.Null);
            Assert.That(runtime.ClientTransport, Is.Null);
            Assert.That(server.DisposeCount, Is.EqualTo(1));
            Assert.That(client.DisposeCount, Is.EqualTo(1));
            runtime.Dispose();
        }

        [Test]
        public void CommandLine_OverridesDefaultsAndRejectsInvalidPort()
        {
            NetworkLaunchOptions defaults = new NetworkLaunchOptions(NetworkLaunchMode.Host, "127.0.0.1", 7777, 32);
            string[] validArguments = { "game.exe", "-batchmode", "-afpsMode=server", "-afpsPort", "8888", "-afpsMaxConnections=48" };

            Assert.That(NetworkLaunchCommandLine.TryApply(validArguments, defaults, out NetworkLaunchOptions parsed, out string validError), Is.True, validError);
            Assert.That(parsed.Mode, Is.EqualTo(NetworkLaunchMode.DedicatedServer));
            Assert.That(parsed.Port, Is.EqualTo(8888));
            Assert.That(parsed.MaxConnections, Is.EqualTo(48));

            string[] invalidArguments = { "game.exe", "-afpsPort=0" };
            Assert.That(NetworkLaunchCommandLine.TryApply(invalidArguments, defaults, out _, out string invalidError), Is.False);
            Assert.That(invalidError, Does.Contain("1 到 65535"));
        }

        [Test]
        public void Host_WithUnityTransportConnectsServerAndLocalClientThroughLoopback()
        {
            using (var runtime = new GameNetworkRuntime(() => new UnityGameTransport()))
            {
                NetworkLaunchOptions options = new NetworkLaunchOptions(NetworkLaunchMode.Host, "ignored", HostIntegrationTestPort, 4);
                Assert.That(runtime.TryStart(options, out string error), Is.True, error);

                byte[] serverBuffer = new byte[64];
                byte[] clientBuffer = new byte[64];
                bool serverConnected = false;
                bool clientConnected = false;
                for (int i = 0; i < PumpLimit && (!serverConnected || !clientConnected); i++)
                {
                    runtime.Pump();
                    serverConnected |= DrainConnected(runtime.ServerTransport, serverBuffer);
                    clientConnected |= DrainConnected(runtime.ClientTransport, clientBuffer);
                    Thread.Sleep(1);
                }

                Assert.That(serverConnected, Is.True, "Host 的服务器传输没有收到本地客户端连接事件。");
                Assert.That(clientConnected, Is.True, "Host 的本地客户端传输没有完成回环连接。");
            }
        }

        private static bool DrainConnected(IGameTransport transport, byte[] receiveBuffer)
        {
            bool connected = false;
            while (transport.TryPollEvent(new ArraySegment<byte>(receiveBuffer), out GameTransportEvent transportEvent))
            {
                connected |= transportEvent.Type == TransportEventType.Connected;
            }

            return connected;
        }

        private sealed class QueueFactory
        {
            private readonly Queue<IGameTransport> transports;

            public QueueFactory(params IGameTransport[] transports)
            {
                this.transports = new Queue<IGameTransport>(transports);
            }

            public IGameTransport Create() => transports.Dequeue();
        }

        private sealed class FakeTransport : IGameTransport
        {
            public bool IsRunning { get; private set; }
            public TransportRole Role { get; private set; }
            public bool ClientStartSucceeds = true;
            public string StartedClientAddress;
            public ushort StartedPort;
            public int StartedMaxConnections;
            public int DisposeCount;

            public bool TryStartServer(ushort port, int maxConnections, out string error)
            {
                StartedPort = port;
                StartedMaxConnections = maxConnections;
                IsRunning = true;
                Role = TransportRole.Server;
                error = null;
                return true;
            }

            public bool TryStartClient(string address, ushort port, out string error)
            {
                StartedClientAddress = address;
                StartedPort = port;
                if (!ClientStartSucceeds)
                {
                    error = "客户端启动失败。";
                    return false;
                }

                IsRunning = true;
                Role = TransportRole.Client;
                error = null;
                return true;
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
                IsRunning = false;
                Role = TransportRole.None;
            }

            public void Dispose()
            {
                DisposeCount++;
                Stop();
            }
        }
    }
}

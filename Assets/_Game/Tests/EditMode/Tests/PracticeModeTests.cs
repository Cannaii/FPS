using System;
using AFPS.NetCode.Match;
using AFPS.NetCode.Runtime;
using AFPS.NetCode.Sessions;
using AFPS.NetCode.Transport;
using AFPS.Simulation.Characters;
using AFPS.Simulation.Weapons;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class PracticeModeTests
    {
        [Test]
        public void Practice_IgnoresNetworkFieldsAndDoesNotCreateSocketTransport()
        {
            Assert.That(NetworkConnectionInput.TryCreateOptions(NetworkLaunchMode.Practice, "bad IP", "bad port", 0,
                out var options, out _), Is.True);
            Assert.That(options.Port, Is.EqualTo(0));
            using (var runtime = new GameNetworkRuntime(() => throw new InvalidOperationException("No sockets")))
            {
                Assert.That(runtime.TryStart(options, out string error), Is.True, error);
                Assert.That(runtime.IsRunning, Is.True);
                Assert.That(runtime.ServerTransport, Is.InstanceOf<LocalGameTransport>());
                Assert.That(runtime.ClientTransport, Is.InstanceOf<LocalGameTransport>());
                runtime.Stop();
                Assert.That(runtime.IsRunning, Is.False);
                Assert.That(runtime.TryStart(options, out error), Is.True, error);
            }
        }

        [Test]
        public void Practice_CommandLineCreatesOfflineOptions()
        {
            var defaults = new NetworkLaunchOptions(NetworkLaunchMode.Host, "irrelevant", 7777, 32);
            Assert.That(NetworkLaunchCommandLine.TryApply(new[] { "-afpsMode", "practice" }, defaults,
                out var options, out _), Is.True);
            Assert.That(options.Mode, Is.EqualTo(NetworkLaunchMode.Practice));
            Assert.That(options.Port, Is.EqualTo(0));
            Assert.That(options.MaxConnections, Is.EqualTo(1));
        }

        [Test]
        public void LocalTransport_CopiesSegmentsPreservesDeliveryAndReportsSmallBuffer()
        {
            LocalGameTransport.CreatePair(out var server, out var client);
            using (server)
            using (client)
            {
                var receive = new byte[8];
                Assert.That(server.TryPollEvent(new ArraySegment<byte>(receive), out var connected), Is.True);
                Assert.That(connected.Type, Is.EqualTo(TransportEventType.Connected));
                client.TryPollEvent(new ArraySegment<byte>(receive), out _);
                var payload = new byte[] { 9, 1, 2, 3, 9 };
                Assert.That(client.Send(connected.ConnectionId, TransportDelivery.ReliableSequenced,
                    new ArraySegment<byte>(payload, 1, 3)), Is.EqualTo(TransportSendResult.Success));
                payload[1] = 99;
                Assert.That(server.TryPollEvent(new ArraySegment<byte>(receive, 2, 3), out var packet), Is.True);
                Assert.That(packet.Delivery, Is.EqualTo(TransportDelivery.ReliableSequenced));
                Assert.That(new[] { receive[2], receive[3], receive[4] }, Is.EqualTo(new byte[] { 1, 2, 3 }));
                client.Send(connected.ConnectionId, TransportDelivery.Unreliable, new ArraySegment<byte>(payload));
                Assert.That(server.TryPollEvent(new ArraySegment<byte>(receive, 0, 1), out packet), Is.True);
                Assert.That(packet.Type, Is.EqualTo(TransportEventType.ReceiveBufferTooSmall));
                Assert.That(packet.PayloadLength, Is.EqualTo(5));
                client.Stop();
                Assert.That(server.TryPollEvent(new ArraySegment<byte>(receive), out packet), Is.True);
                Assert.That(packet.Type, Is.EqualTo(TransportEventType.Disconnected));
                Assert.That(server.Send(connected.ConnectionId, default, new ArraySegment<byte>(payload)),
                    Is.EqualTo(TransportSendResult.InvalidConnection));
            }
        }

        [Test]
        public void LocalTransport_RejectsOversizeAndBoundsQueue()
        {
            LocalGameTransport.CreatePair(out var server, out var client);
            using (server)
            using (client)
            {
                var id = new TransportConnectionId(1);
                Assert.That(client.Send(id, default, new ArraySegment<byte>(new byte[2049])),
                    Is.EqualTo(TransportSendResult.PayloadTooLarge));
                var bytes = new ArraySegment<byte>(new byte[1]);
                for (int i = 0; i < 255; i++)
                {
                    Assert.That(client.Send(id, default, bytes), Is.EqualTo(TransportSendResult.Success));
                }
                Assert.That(client.Send(id, default, bytes), Is.EqualTo(TransportSendResult.TransportError));
            }
        }

        [Test]
        public void Practice_SinglePlayerUsesAuthorityAndDoesNotExpire()
        {
            LocalGameTransport.CreatePair(out var server, out var client);
            using (server)
            using (client)
            {
                var initial = new PlayerState { IsGrounded = true };
                var config = new PlayerSimulationConfig(6f, 20f, 20f, 8f);
                var manager = new NetworkMovementSessionManager(server, client, initial, initial, config,
                    0.02f, 64, 3, 32, 2, 2, 0f, 0f, weaponConfig: WeaponSimulationConfig.DefaultRifle,
                    deathmatchDurationSeconds: 0.02f, isLocalPractice: true);
                Drain(server, NetworkTransportSide.Server, manager);
                Drain(client, NetworkTransportSide.Client, manager);
                for (uint tick = 1; tick <= 20; tick++)
                {
                    var input = new PlayerInputCommand
                    {
                        Tick = tick,
                        FirePressed = tick == 2,
                        ShotSequence = tick == 2 ? 1u : 0u
                    };
                    Assert.That(manager.TryPredictAndSend(input, out _, out _), Is.True);
                    Drain(server, NetworkTransportSide.Server, manager);
                    manager.AdvanceServerSessions(tick);
                    Drain(client, NetworkTransportSide.Client, manager);
                }
                Assert.That(manager.ServerSessionCount, Is.EqualTo(1));
                Assert.That(manager.ClientDeathmatchState.Phase, Is.EqualTo(DeathmatchPhase.Playing));
                Assert.That(manager.ClientDeathmatchState.RemainingTicks, Is.EqualTo(0));
                Assert.That(manager.ClientWeapon.State.Ammo, Is.EqualTo(29));
                Assert.That(manager.ShotResultVersion, Is.GreaterThan(0));
            }
        }

        private static void Drain(IGameTransport transport, NetworkTransportSide side, NetworkMovementSessionManager manager)
        {
            var bytes = new byte[2048];
            while (transport.TryPollEvent(new ArraySegment<byte>(bytes), out var packet))
            {
                if (packet.Type == TransportEventType.Connected)
                {
                    manager.HandleConnected(side, packet.ConnectionId);
                }
                else if (packet.Type == TransportEventType.Data)
                {
                    manager.TryHandleData(side, packet.ConnectionId,
                        new ArraySegment<byte>(bytes, 0, packet.PayloadLength), out _);
                }
            }
        }
    }
}

using System;
using System.Threading;
using System.Text.RegularExpressions;
using AFPS.NetCode.Transport;
using AFPS.Transport.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class UnityGameTransportTests
{
    private const ushort TestPort = 47991;
    private const int PumpLimit = 1000;

    [Test]
    public void LoopbackConnection_CanSendOnEveryDeliveryChannel()
    {
        using (var server = new UnityGameTransport())
        using (var client = new UnityGameTransport())
        {
            Assert.That(server.TryStartServer(TestPort, 4, out string serverError), Is.True, serverError);
            Assert.That(client.TryStartClient("127.0.0.1", TestPort, out string clientError), Is.True, clientError);

            TransportConnectionId serverConnection = default;
            TransportConnectionId clientConnection = default;
            byte[] receiveBuffer = new byte[128];
            PumpUntil(server, client, () =>
            {
                DrainConnectedEvent(server, receiveBuffer, ref serverConnection);
                DrainConnectedEvent(client, receiveBuffer, ref clientConnection);
                return serverConnection.IsValid && clientConnection.IsValid;
            });

            TransportDelivery[] deliveries =
            {
                TransportDelivery.Unreliable,
                TransportDelivery.UnreliableSequenced,
                TransportDelivery.ReliableSequenced
            };

            for (int i = 0; i < deliveries.Length; i++)
            {
                byte[] payload = { (byte)(10 + i), (byte)(20 + i), (byte)(30 + i) };
                Assert.That(client.Send(clientConnection, deliveries[i], new ArraySegment<byte>(payload)), Is.EqualTo(TransportSendResult.Success));

                GameTransportEvent receivedEvent = default;
                PumpUntil(server, client, () => TryReceiveData(server, receiveBuffer, out receivedEvent));
                Assert.That(receivedEvent.ConnectionId, Is.EqualTo(serverConnection));
                Assert.That(receivedEvent.Delivery, Is.EqualTo(deliveries[i]));
                Assert.That(receivedEvent.PayloadLength, Is.EqualTo(payload.Length));
                CollectionAssert.AreEqual(payload, new ArraySegment<byte>(receiveBuffer, 0, receivedEvent.PayloadLength));
            }
        }
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Stop_NotifiesPeerWithoutFurtherPumpingStoppedSideAndCanReuseInstance(bool stopServer)
    {
        using (var server = new UnityGameTransport())
        using (var client = new UnityGameTransport())
        {
            for (int round = 0; round < 3; round++)
            {
                Assert.That(server.TryStartServer(47986, 4, out string error), Is.True, error);
                Assert.That(client.TryStartClient("127.0.0.1", 47986, out error), Is.True, error);
                TransportConnectionId serverId = default;
                TransportConnectionId clientId = default;
                var buffer = new byte[64];
                PumpUntil(server, client, () =>
                {
                    DrainConnectedEvent(server, buffer, ref serverId);
                    DrainConnectedEvent(client, buffer, ref clientId);
                    return serverId.IsValid && clientId.IsValid;
                });
                var stopped = stopServer ? server : client;
                var peer = stopServer ? client : server;
                var expectedId = stopServer ? clientId : serverId;
                stopped.Stop();
                stopped.Stop();
                Assert.That(stopped.Role, Is.EqualTo(TransportRole.None));
                Assert.That(stopped.Send(expectedId, TransportDelivery.ReliableSequenced,
                    new ArraySegment<byte>(new byte[1])), Is.EqualTo(TransportSendResult.NotRunning));

                // 只推进存活端，预算约1秒，远短于UTP默认30秒断线超时。
                bool disconnected = false;
                for (int i = 0; i < PumpLimit && !disconnected; i++)
                {
                    peer.Pump();
                    while (peer.TryPollEvent(new ArraySegment<byte>(buffer), out var transportEvent))
                    {
                        if (transportEvent.Type == TransportEventType.Disconnected)
                        {
                            Assert.That(transportEvent.ConnectionId, Is.EqualTo(expectedId));
                            disconnected = true;
                        }
                    }
                    Thread.Sleep(1);
                }
                Assert.That(disconnected, Is.True, "正常停止应通知对端，不能依赖连接超时。");
                peer.Stop();
            }
        }
    }

    [Test]
    public void FailedBind_ReleasesDriverAndCanRetryAfterPortIsReleased()
    {
        using (var owner = new UnityGameTransport())
        using (var retry = new UnityGameTransport())
        {
            Assert.That(owner.TryStartServer(47987, 2, out _), Is.True);
            LogAssert.Expect(LogType.Error, new Regex("^Socket creation failed.*Address in use"));
            Assert.That(retry.TryStartServer(47987, 2, out _), Is.False);
            Assert.That(retry.IsRunning, Is.False);
            retry.Stop();
            owner.Stop();
            Assert.That(retry.TryStartServer(47987, 2, out string error), Is.True, error);
        }
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

        Assert.Fail("在限定时间内没有收到预期的 Unity Transport 事件。");
    }

    private static void DrainConnectedEvent(UnityGameTransport transport, byte[] receiveBuffer, ref TransportConnectionId connectionId)
    {
        while (transport.TryPollEvent(new ArraySegment<byte>(receiveBuffer), out GameTransportEvent transportEvent))
        {
            if (transportEvent.Type == TransportEventType.Connected)
            {
                connectionId = transportEvent.ConnectionId;
            }
        }
    }

    private static bool TryReceiveData(UnityGameTransport transport, byte[] receiveBuffer, out GameTransportEvent receivedEvent)
    {
        while (transport.TryPollEvent(new ArraySegment<byte>(receiveBuffer), out GameTransportEvent transportEvent))
        {
            if (transportEvent.Type == TransportEventType.Data)
            {
                receivedEvent = transportEvent;
                return true;
            }
        }

        receivedEvent = default;
        return false;
    }
}

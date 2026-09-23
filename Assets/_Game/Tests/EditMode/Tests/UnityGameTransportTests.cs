using System;
using System.Threading;
using AFPS.NetCode.Transport;
using AFPS.Transport.Unity;
using NUnit.Framework;

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

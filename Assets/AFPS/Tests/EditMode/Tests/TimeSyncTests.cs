using System;
using System.Threading;
using AFPS.NetCode.TimeSync;
using AFPS.NetCode.Transport;
using AFPS.Transport.Unity;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class TimeSyncTests
    {
        private const ushort TestPort = 47994;
        private const int PumpLimit = 1000;

        [Test]
        public void Codec_RoundTripsRequestAndResponseWithArrayOffset()
        {
            byte[] requestStorage = new byte[TimeSyncCodec.RequestPacketSize + 4];
            ArraySegment<byte> requestPacket = new ArraySegment<byte>(requestStorage, 2, TimeSyncCodec.RequestPacketSize);
            Assert.That(TimeSyncCodec.TrySerializeRequest(7, 123456789, requestPacket, out int requestBytes), Is.True);
            Assert.That(requestBytes, Is.EqualTo(TimeSyncCodec.RequestPacketSize));
            Assert.That(TimeSyncCodec.TryDeserializeRequest(requestPacket, out TimeSyncRequest request), Is.True);
            Assert.That(request.RequestSequence, Is.EqualTo(7));
            Assert.That(request.ClientSendTimestampMicroseconds, Is.EqualTo(123456789));

            TimeSyncResponse sourceResponse = new TimeSyncResponse(7, 123456789, 5000, 5500, 200, 32768);
            byte[] responseStorage = new byte[TimeSyncCodec.ResponsePacketSize + 6];
            ArraySegment<byte> responsePacket = new ArraySegment<byte>(responseStorage, 3, TimeSyncCodec.ResponsePacketSize);
            Assert.That(TimeSyncCodec.TrySerializeResponse(sourceResponse, 9, responsePacket, out int responseBytes), Is.True);
            Assert.That(responseBytes, Is.EqualTo(TimeSyncCodec.ResponsePacketSize));
            Assert.That(TimeSyncCodec.TryDeserializeResponse(responsePacket, out TimeSyncResponse decoded), Is.True);
            Assert.That(decoded.RequestSequence, Is.EqualTo(7));
            Assert.That(decoded.ServerReceiveTimestampMicroseconds, Is.EqualTo(5000));
            Assert.That(decoded.ServerSendTimestampMicroseconds, Is.EqualTo(5500));
            Assert.That(decoded.ServerWorldTick, Is.EqualTo(200));
            Assert.That(decoded.ServerTickFraction, Is.EqualTo(32768));
        }

        [Test]
        public void Synchronizer_SubtractsServerProcessingAndEstimatesCurrentServerTick()
        {
            ClientServerTickSynchronizer synchronizer = new ClientServerTickSynchronizer(50, smoothingFactor: 1.0);
            synchronizer.RegisterSentRequest(1, 1000000);
            TimeSyncResponse response = new TimeSyncResponse(1, 1000000, 500000, 502000, 200, 32768);

            Assert.That(synchronizer.TryProcessResponse(response, 1102000, out ServerTickSyncSample sample), Is.True);
            Assert.That(sample.NetworkRoundTripMilliseconds, Is.EqualTo(100.0).Within(0.0001));
            Assert.That(sample.EstimatedOneWayMilliseconds, Is.EqualTo(50.0).Within(0.0001));
            Assert.That(sample.EstimatedServerTickAtClientReceive, Is.EqualTo(203.0).Within(0.001));

            Assert.That(synchronizer.TryGetEstimatedServerTick(1142000, out double estimatedLaterTick), Is.True);
            Assert.That(estimatedLaterTick, Is.EqualTo(205.0).Within(0.001));
        }

        [Test]
        public void Synchronizer_RejectsUnknownRequestAndExcessiveRoundTrip()
        {
            ClientServerTickSynchronizer synchronizer = new ClientServerTickSynchronizer(50, maxRoundTripMilliseconds: 100.0);
            TimeSyncResponse unknown = new TimeSyncResponse(2, 1000, 500, 500, 10, 0);
            Assert.That(synchronizer.TryProcessResponse(unknown, 2000, out _), Is.False);

            synchronizer.RegisterSentRequest(1, 1000000);
            TimeSyncResponse slow = new TimeSyncResponse(1, 1000000, 500000, 500000, 20, 0);
            Assert.That(synchronizer.TryProcessResponse(slow, 1200000, out _), Is.False);
            Assert.That(synchronizer.HasEstimate, Is.False);
        }

        [Test]
        public void Synchronizer_UnwrapsServerTickFromMaxValueToZero()
        {
            ClientServerTickSynchronizer synchronizer = new ClientServerTickSynchronizer(50, smoothingFactor: 1.0);
            synchronizer.RegisterSentRequest(1, 1000000);
            Assert.That(synchronizer.TryProcessResponse(new TimeSyncResponse(1, 1000000, 10, 10, uint.MaxValue, 10000), 1020000, out ServerTickSyncSample beforeWrap), Is.True);

            synchronizer.RegisterSentRequest(2, 1040000);
            Assert.That(synchronizer.TryProcessResponse(new TimeSyncResponse(2, 1040000, 20, 20, 0, 10000), 1060000, out ServerTickSyncSample afterWrap), Is.True);
            Assert.That(afterWrap.EstimatedServerTickAtClientReceive, Is.GreaterThan(beforeWrap.EstimatedServerTickAtClientReceive));
        }

        [Test]
        public void TimeSyncSample_TravelsThroughRealUnityTransportLoopback()
        {
            using (var serverTransport = new UnityGameTransport())
            using (var clientTransport = new UnityGameTransport())
            {
                Assert.That(serverTransport.TryStartServer(TestPort, 2, out string serverError), Is.True, serverError);
                Assert.That(clientTransport.TryStartClient("127.0.0.1", TestPort, out string clientError), Is.True, clientError);
                byte[] serverBuffer = new byte[128];
                byte[] clientBuffer = new byte[128];
                TransportConnectionId serverConnection = default;
                TransportConnectionId clientConnection = default;
                PumpUntil(serverTransport, clientTransport, () =>
                {
                    DrainConnected(serverTransport, serverBuffer, ref serverConnection);
                    DrainConnected(clientTransport, clientBuffer, ref clientConnection);
                    return serverConnection.IsValid && clientConnection.IsValid;
                });

                ClientServerTickSynchronizer synchronizer = new ClientServerTickSynchronizer(50, smoothingFactor: 1.0);
                ClientTimeSyncSession clientSession = new ClientTimeSyncSession(clientTransport, clientConnection, synchronizer);
                ServerTimeSyncResponder serverResponder = new ServerTimeSyncResponder(serverTransport, serverConnection);
                Assert.That(clientSession.SendRequest(1000000, out _), Is.EqualTo(TransportSendResult.Success));

                bool responded = false;
                PumpUntil(serverTransport, clientTransport, () =>
                {
                    while (serverTransport.TryPollEvent(new ArraySegment<byte>(serverBuffer), out GameTransportEvent transportEvent))
                    {
                        if (transportEvent.Type == TransportEventType.Data)
                        {
                            responded |= serverResponder.TryRespond(new ArraySegment<byte>(serverBuffer, 0, transportEvent.PayloadLength), 500000, 502000, 200, 0.5f, out _);
                        }
                    }

                    return responded;
                });

                ServerTickSyncSample receivedSample = default;
                bool received = false;
                PumpUntil(serverTransport, clientTransport, () =>
                {
                    while (clientTransport.TryPollEvent(new ArraySegment<byte>(clientBuffer), out GameTransportEvent transportEvent))
                    {
                        if (transportEvent.Type == TransportEventType.Data)
                        {
                            received |= clientSession.TryReceiveResponse(new ArraySegment<byte>(clientBuffer, 0, transportEvent.PayloadLength), 1102000, out receivedSample);
                        }
                    }

                    return received;
                });

                Assert.That(receivedSample.NetworkRoundTripMilliseconds, Is.EqualTo(100.0).Within(0.001));
                Assert.That(receivedSample.EstimatedServerTickAtClientReceive, Is.EqualTo(203.0).Within(0.001));
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

            Assert.Fail("在限定时间内没有完成时间同步网络步骤。");
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
    }
}

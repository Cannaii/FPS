using System;
using System.Collections.Generic;
using AFPS.NetCode.Transport;
using AFPS.NetCode.Transport.Simulation;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class NetworkImpairmentTransportTests
    {
        [Test]
        public void FixedLatency_CopiesPayloadAndReleasesOnlyAfterDeadline()
        {
            ManualTime time = new ManualTime();
            FakeTransport inner = new FakeTransport();
            NetworkImpairmentConfig config = Config(baseLatencySeconds: 0.1d);
            using (var transport = new NetworkImpairmentTransport(inner, config, time.GetNow))
            {
                Assert.That(transport.TryStartClient("127.0.0.1", 7777, out _), Is.True);
                byte[] payload = { 10 };
                Assert.That(transport.Send(new TransportConnectionId(1), TransportDelivery.UnreliableSequenced, new ArraySegment<byte>(payload)), Is.EqualTo(TransportSendResult.Success));
                payload[0] = 99;

                time.Now = 0.099d;
                transport.Pump();
                Assert.That(inner.SentFirstBytes, Is.Empty);
                Assert.That(transport.QueuedPacketCount, Is.EqualTo(1));

                time.Now = 0.1d;
                transport.Pump();
                CollectionAssert.AreEqual(new byte[] { 10 }, inner.SentFirstBytes);
                Assert.That(transport.ReleasedPacketCount, Is.EqualTo(1));
                Assert.That(transport.QueuedPacketCount, Is.Zero);
            }
        }

        [Test]
        public void FullPacketLoss_ReturnsSuccessWithoutCallingInnerTransport()
        {
            ManualTime time = new ManualTime();
            FakeTransport inner = new FakeTransport();
            NetworkImpairmentConfig config = Config(packetLossProbability: 1d);
            using (var transport = new NetworkImpairmentTransport(inner, config, time.GetNow))
            {
                Assert.That(transport.TryStartClient("127.0.0.1", 7777, out _), Is.True);
                Assert.That(SendByte(transport, 1), Is.EqualTo(TransportSendResult.Success));
                transport.Pump();

                Assert.That(inner.SentFirstBytes, Is.Empty);
                Assert.That(transport.DroppedPacketCount, Is.EqualTo(1));
                Assert.That(transport.QueuedPacketCount, Is.Zero);
            }
        }

        [Test]
        public void Reorder_SendsLaterPacketBeforePreviousPacket()
        {
            ManualTime time = new ManualTime();
            FakeTransport inner = new FakeTransport();
            NetworkImpairmentConfig config = Config(baseLatencySeconds: 0.1d, reorderProbability: 1d, reorderExtraDelaySeconds: 0.2d);
            using (var transport = new NetworkImpairmentTransport(inner, config, time.GetNow))
            {
                Assert.That(transport.TryStartClient("127.0.0.1", 7777, out _), Is.True);
                SendByte(transport, 1);
                SendByte(transport, 2);

                time.Now = 0.1d;
                transport.Pump();
                CollectionAssert.AreEqual(new byte[] { 2 }, inner.SentFirstBytes);

                time.Now = 0.31d;
                transport.Pump();
                CollectionAssert.AreEqual(new byte[] { 2, 1 }, inner.SentFirstBytes);
            }
        }

        [Test]
        public void ReliableSequenced_BypassesApplicationLevelImpairment()
        {
            ManualTime time = new ManualTime();
            FakeTransport inner = new FakeTransport();
            NetworkImpairmentConfig config = Config(baseLatencySeconds: 10d, packetLossProbability: 1d);
            using (var transport = new NetworkImpairmentTransport(inner, config, time.GetNow))
            {
                Assert.That(transport.TryStartClient("127.0.0.1", 7777, out _), Is.True);
                byte[] payload = { 7 };
                Assert.That(transport.Send(new TransportConnectionId(1), TransportDelivery.ReliableSequenced, new ArraySegment<byte>(payload)), Is.EqualTo(TransportSendResult.Success));

                CollectionAssert.AreEqual(new byte[] { 7 }, inner.SentFirstBytes);
                Assert.That(transport.DroppedPacketCount, Is.Zero);
                Assert.That(transport.QueuedPacketCount, Is.Zero);
            }
        }

        [Test]
        public void Reorder_DoesNotExchangePacketsFromDifferentConnections()
        {
            ManualTime time = new ManualTime();
            FakeTransport inner = new FakeTransport();
            NetworkImpairmentConfig config = Config(baseLatencySeconds: 0.1d, reorderProbability: 1d, reorderExtraDelaySeconds: 0.2d);
            using (var transport = new NetworkImpairmentTransport(inner, config, time.GetNow))
            {
                Assert.That(transport.TryStartServer(7777, 4, out _), Is.True);
                SendByte(transport, 1, new TransportConnectionId(1));
                SendByte(transport, 2, new TransportConnectionId(2));

                time.Now = 0.1d;
                transport.Pump();
                CollectionAssert.AreEqual(new byte[] { 1, 2 }, inner.SentFirstBytes);
            }
        }

        [Test]
        public void SameSeedAndTraffic_ProduceSameLossAndDeliverySequence()
        {
            ManualTime firstTime = new ManualTime();
            ManualTime secondTime = new ManualTime();
            FakeTransport firstInner = new FakeTransport();
            FakeTransport secondInner = new FakeTransport();
            NetworkImpairmentConfig config = Config(baseLatencySeconds: 0.1d, jitterSeconds: 0.05d, packetLossProbability: 0.25d, reorderProbability: 0.3d, reorderExtraDelaySeconds: 0.02d, randomSeed: 9876);

            using (var first = new NetworkImpairmentTransport(firstInner, config, firstTime.GetNow))
            using (var second = new NetworkImpairmentTransport(secondInner, config, secondTime.GetNow))
            {
                Assert.That(first.TryStartClient("127.0.0.1", 7777, out _), Is.True);
                Assert.That(second.TryStartClient("127.0.0.1", 7777, out _), Is.True);
                for (byte value = 1; value <= 20; value++)
                {
                    SendByte(first, value);
                    SendByte(second, value);
                }

                firstTime.Now = 1d;
                secondTime.Now = 1d;
                first.Pump();
                second.Pump();

                CollectionAssert.AreEqual(firstInner.SentFirstBytes, secondInner.SentFirstBytes);
                Assert.That(first.DroppedPacketCount, Is.EqualTo(second.DroppedPacketCount));
                Assert.That(first.ReleasedPacketCount, Is.EqualTo(second.ReleasedPacketCount));
            }
        }

        private static NetworkImpairmentConfig Config(double baseLatencySeconds = 0d, double jitterSeconds = 0d, double packetLossProbability = 0d, double reorderProbability = 0d, double reorderExtraDelaySeconds = 0d, uint randomSeed = 1234, int maxQueuedPackets = 64)
        {
            return new NetworkImpairmentConfig(baseLatencySeconds, jitterSeconds, packetLossProbability, reorderProbability, reorderExtraDelaySeconds, randomSeed, maxQueuedPackets);
        }

        private static TransportSendResult SendByte(IGameTransport transport, byte value, TransportConnectionId connectionId = default)
        {
            byte[] payload = { value };
            return transport.Send(connectionId.IsValid ? connectionId : new TransportConnectionId(1), TransportDelivery.UnreliableSequenced, new ArraySegment<byte>(payload));
        }

        private sealed class ManualTime
        {
            public double Now;
            public double GetNow() => Now;
        }

        private sealed class FakeTransport : IGameTransport
        {
            public bool IsRunning { get; private set; }
            public TransportRole Role { get; private set; }
            public readonly List<byte> SentFirstBytes = new List<byte>();

            public bool TryStartServer(ushort port, int maxConnections, out string error)
            {
                IsRunning = true;
                Role = TransportRole.Server;
                error = null;
                return true;
            }

            public bool TryStartClient(string address, ushort port, out string error)
            {
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

            public TransportSendResult Send(TransportConnectionId connectionId, TransportDelivery delivery, ArraySegment<byte> payload)
            {
                SentFirstBytes.Add(payload.Count > 0 ? payload.Array[payload.Offset] : (byte)0);
                return TransportSendResult.Success;
            }

            public void Disconnect(TransportConnectionId connectionId)
            {
            }

            public void Stop()
            {
                IsRunning = false;
                Role = TransportRole.None;
            }

            public void Dispose() => Stop();
        }
    }
}

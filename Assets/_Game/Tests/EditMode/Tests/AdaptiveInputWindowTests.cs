using System;
using AFPS.NetCode.InputReplication;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.TimeSync;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class AdaptiveInputWindowTests
    {
        [Test]
        public void WindowUsesMeasuredDelayAndJitter_WithBoundsAndGradualShrink()
        {
            var policy = new AdaptiveInputWindow(50, 2, 2, 16);
            policy.Observe(1d, 0d);
            Assert.That(policy.WaitTicks, Is.EqualTo(2));
            policy.Observe(200d, 20d);
            Assert.That(policy.WaitTicks, Is.EqualTo(7));
            Assert.That(policy.RepeatTicks, Is.EqualTo(6));
            policy.Observe(double.MaxValue, double.MaxValue);
            Assert.That(policy.WaitTicks, Is.EqualTo(8));
            Assert.That(policy.RepeatTicks, Is.EqualTo(6));
            policy.Observe(0d, 0d);
            Assert.That(policy.WaitTicks, Is.EqualTo(7));
            Assert.That(policy.RepeatTicks, Is.EqualTo(5));
            policy.Reset();
            Assert.That(policy.WaitTicks, Is.EqualTo(2));
            Assert.That(policy.RepeatTicks, Is.EqualTo(2));
        }

        [Test]
        public void WindowHonorsDisabledSmallCapacityAndExplicitLegacyLimits()
        {
            var disabled = new AdaptiveInputWindow(50, 0, 0, 16);
            disabled.Observe(1000d, 100d);
            Assert.That(disabled.WaitTicks, Is.Zero);
            Assert.That(disabled.RepeatTicks, Is.Zero);
            var small = new AdaptiveInputWindow(50, 2, 2, 4);
            small.Observe(1000d, 100d);
            Assert.That(small.WaitTicks, Is.EqualTo(3));
            Assert.That(small.RepeatTicks, Is.EqualTo(3));
            var legacy = new AdaptiveInputWindow(50, 10, 9, 16);
            legacy.Observe(1000d, 100d);
            Assert.That(legacy.WaitTicks, Is.EqualTo(10));
            Assert.That(legacy.RepeatTicks, Is.EqualTo(9));
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(-1d)]
        public void InvalidQualityDoesNotMutateWindow(double value)
        {
            var policy = new AdaptiveInputWindow(50, 2, 2, 16);
            Assert.Throws<ArgumentOutOfRangeException>(() => policy.Observe(value, 0d));
            Assert.Throws<ArgumentOutOfRangeException>(() => policy.Observe(0d, value));
            Assert.That(policy.WaitTicks, Is.EqualTo(2));
        }

        [TestCase(0, 2, 2, 16)]
        [TestCase(50, -1, 2, 16)]
        [TestCase(50, 2, -1, 16)]
        [TestCase(50, 2, 2, 0)]
        public void InvalidConfigurationIsRejected(int rate, int wait, int repeat, int capacity)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new AdaptiveInputWindow(rate, wait, repeat, capacity));
        }

        [TestCase(0u)]
        [TestCase(uint.MaxValue)]
        public void AckCodecRequiresExactPacketAndCurrentProtocol(uint sequence)
        {
            var buffer = new byte[TimeSyncAcknowledgementCodec.PacketSize + 6];
            var slice = new ArraySegment<byte>(buffer, 3, TimeSyncAcknowledgementCodec.PacketSize);
            Assert.That(TimeSyncAcknowledgementCodec.TrySerialize(sequence, slice, out int count), Is.True);
            Assert.That(count, Is.EqualTo(12));
            Assert.That(TimeSyncAcknowledgementCodec.TryDeserialize(slice, out uint decoded), Is.True);
            Assert.That(decoded, Is.EqualTo(sequence));
            for (int length = 0; length < count; length++)
            {
                Assert.That(TimeSyncAcknowledgementCodec.TryDeserialize(
                    new ArraySegment<byte>(buffer, 3, length), out _), Is.False);
            }
            Assert.That(TimeSyncAcknowledgementCodec.TryDeserialize(
                new ArraySegment<byte>(buffer, 3, count + 1), out _), Is.False);
            buffer[3 + 4] = (byte)(PacketHeader.CurrentProtocolVersion - 1);
            Assert.That(TimeSyncAcknowledgementCodec.TryDeserialize(slice, out _), Is.False);
        }

        [Test]
        public void ServerRttRejectsUnknownDuplicateOldExpiredAndClockReversedAcks()
        {
            var tracker = new ServerRoundTripTracker();
            Assert.That(tracker.TryAcknowledge(1, 200000), Is.False);
            tracker.RecordSent(1, 100000);
            Assert.That(tracker.TryAcknowledge(1, 99999), Is.False);
            Assert.That(tracker.TryAcknowledge(1, 200000), Is.True);
            Assert.That(tracker.RoundTripMilliseconds, Is.EqualTo(100d));
            Assert.That(tracker.TryAcknowledge(1, 200001), Is.False);
            tracker.RecordSent(2, 300000);
            Assert.That(tracker.TryAcknowledge(2, 700000), Is.True);
            Assert.That(tracker.JitterMilliseconds, Is.EqualTo(60d));
            Assert.That(tracker.HasFreshSample(3700000), Is.True);
            Assert.That(tracker.HasFreshSample(3700001), Is.False);
            tracker.RecordSent(3, 4000000);
            Assert.That(tracker.TryAcknowledge(3, 4100000), Is.True);
            Assert.That(tracker.JitterMilliseconds, Is.Zero);
            tracker.RecordSent(2, 4200000);
            Assert.That(tracker.TryAcknowledge(2, 4300000), Is.False);
            tracker.RecordSent(4, 4200000);
            Assert.That(tracker.TryAcknowledge(4, 5200001), Is.False);
        }

        [Test]
        public void ServerRttPendingStorageIsBoundedAndSequenceWraps()
        {
            var tracker = new ServerRoundTripTracker();
            for (uint sequence = 1; sequence <= 9; sequence++)
            {
                tracker.RecordSent(sequence, 1000);
            }
            Assert.That(tracker.TryAcknowledge(1, 2000), Is.False);
            Assert.That(tracker.TryAcknowledge(9, 2000), Is.True);
            tracker = new ServerRoundTripTracker();
            tracker.RecordSent(uint.MaxValue, 1000);
            Assert.That(tracker.TryAcknowledge(uint.MaxValue, 2000), Is.True);
            tracker.RecordSent(0, 3000);
            Assert.That(tracker.TryAcknowledge(0, 4000), Is.True);
        }
    }
}

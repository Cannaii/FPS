using System;
using AFPS.NetCode.SnapshotInterpolation;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class AdaptiveInterpolationClockTests
    {
        [Test]
        public void CadenceFloorAndQualityMargin_AreBounded()
        {
            var clock = new AdaptiveInterpolationClock(50, 20, 2d);
            Assert.That(clock.CurrentDelayTicks, Is.EqualTo(3d));
            clock.Update(0, true, 100d, 10d);
            Assert.That(clock.TargetDelayTicks, Is.EqualTo(6.5d));
            clock.Update(250000, true, 10000d, 10000d);
            Assert.That(clock.TargetDelayTicks, Is.EqualTo(12d));
            Assert.That(clock.CurrentDelayTicks, Is.EqualTo(4d));
            var configured = new AdaptiveInterpolationClock(50, 20, 15d);
            Assert.That(configured.MinimumDelayTicks, Is.EqualTo(15d));
            Assert.That(configured.MaximumDelayTicks, Is.EqualTo(15d));
            Assert.That(new AdaptiveInterpolationClock(60, 20, 0d).MinimumDelayTicks, Is.EqualTo(3d));
        }

        [Test]
        public void DelayRisesSmoothlyAndDecaysSlower_WithoutPauseCatchup()
        {
            var clock = new AdaptiveInterpolationClock(50, 20, 2d);
            clock.Update(0, true, 400d, 100d);
            for (ulong now = 250000; now <= 1000000; now += 250000)
            {
                clock.Update(now, true, 400d, 100d);
            }
            Assert.That(clock.CurrentDelayTicks, Is.EqualTo(7d));
            clock.Update(20000000, true, 0d, 0d);
            Assert.That(clock.CurrentDelayTicks, Is.EqualTo(6.75d));
            clock.Update(20000000, true, 1000d, 0d);
            clock.Update(19000000, true, 1000d, 0d);
            Assert.That(clock.CurrentDelayTicks, Is.EqualTo(6.75d));
        }

        [Test]
        public void StaleQualityReturnsToFloor_AndResetClearsOldPlaybackEpoch()
        {
            var clock = new AdaptiveInterpolationClock(50, 20, 2d);
            clock.Update(0, true, 400d, 0d);
            clock.Update(250000, true, 400d, 0d);
            clock.Update(500000, false, double.NaN, double.PositiveInfinity);
            Assert.That(clock.TargetDelayTicks, Is.EqualTo(3d));
            Assert.That(clock.CurrentDelayTicks, Is.EqualTo(3.75d));
            clock.TryGetRenderTime(1000d, out _, out _);
            clock.Reset();
            Assert.That(clock.CurrentDelayTicks, Is.EqualTo(3d));
            Assert.That(clock.TargetDelayTicks, Is.EqualTo(3d));
            clock.TryGetRenderTime(10d, out _, out double restarted);
            Assert.That(restarted, Is.EqualTo(7d));
        }

        [Test]
        public void QualityRiseAndClockCorrection_NeverReversePlayback()
        {
            var clock = new AdaptiveInterpolationClock(50, 20, 2d);
            clock.Update(0, true, 400d, 0d);
            clock.TryGetRenderTime(100d, out _, out double previous);
            for (ulong frame = 1; frame <= 100; frame++)
            {
                clock.Update(frame * 20000, true, 400d, 0d);
                clock.TryGetRenderTime(100d + frame, out _, out double current);
                Assert.That(current, Is.GreaterThan(previous));
                previous = current;
            }
            clock.TryGetRenderTime(199d, out _, out double held);
            Assert.That(held, Is.EqualTo(previous));
            clock.TryGetRenderTime(201d, out _, out double resumed);
            Assert.That(resumed, Is.GreaterThan(held));
        }

        [Test]
        public void TimeSyncAndRawFallback_ShareWrapEpoch()
        {
            var clock = new AdaptiveInterpolationClock(50, 20, 2d);
            double period = 4294967296d;
            clock.TryGetRenderTime(period - 1d, out _, out double before);
            clock.TryGetRenderTime(0.5d, out double continuous, out double after);
            Assert.That(continuous, Is.EqualTo(period + 0.5d));
            Assert.That(after - before, Is.EqualTo(1.5d));
            clock.TryGetRenderTime(period + 1.5d, out continuous, out after);
            Assert.That(continuous, Is.EqualTo(period + 1.5d));
            Assert.That(after, Is.EqualTo(period - 1.5d));
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(-1d)]
        public void InvalidSamples_DoNotPoisonClock(double value)
        {
            var clock = new AdaptiveInterpolationClock(50, 20, 2d);
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.Update(0, true, value, 0d));
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.Update(0, true, 0d, value));
            Assert.That(clock.TryGetRenderTime(value, out _, out _), Is.False);
            Assert.That(clock.CurrentDelayTicks, Is.EqualTo(3d));
        }

        [TestCase(0, 20, 2d)]
        [TestCase(50, 0, 2d)]
        [TestCase(50, 20, double.NaN)]
        [TestCase(50, 20, -1d)]
        public void InvalidConfiguration_IsRejected(int tickRate, int rate, double minimum)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new AdaptiveInterpolationClock(tickRate, rate, minimum));
        }
    }
}

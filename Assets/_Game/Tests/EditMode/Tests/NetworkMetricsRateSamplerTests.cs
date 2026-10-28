using AFPS.NetCode.Metrics;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class NetworkMetricsRateSamplerTests
    {
        [Test]
        public void RatesUseElapsedWindow_AndIdleBecomesZero()
        {
            var sampler = new NetworkMetricsRateSampler();
            sampler.Update(1000000, 1000, 10);
            sampler.Update(1499999, 2000, 20);
            Assert.That(sampler.HasRate, Is.False);
            sampler.Update(1500000, 2000, 20);
            Assert.That(sampler.BytesPerSecond, Is.EqualTo(2000d));
            Assert.That(sampler.PacketsPerSecond, Is.EqualTo(20d));
            sampler.Update(2500000, 2000, 20);
            Assert.That(sampler.BytesPerSecond, Is.Zero);
            Assert.That(sampler.PacketsPerSecond, Is.Zero);
        }

        [Test]
        public void ResetCountersAndReversedClock_DoNotProduceHugeRates()
        {
            var sampler = new NetworkMetricsRateSampler();
            sampler.Update(1000000, ulong.MaxValue - 2, 100);
            sampler.Update(1500000, ulong.MaxValue, 101);
            Assert.That(sampler.BytesPerSecond, Is.EqualTo(4d));
            sampler.Update(2000000, 0, 0);
            Assert.That(sampler.HasRate, Is.False);
            Assert.That(sampler.BytesPerSecond, Is.Zero);
            sampler.Update(1000000, 100, 10);
            Assert.That(sampler.HasRate, Is.False);
            sampler.Update(2000000, 300, 15);
            Assert.That(sampler.BytesPerSecond, Is.EqualTo(200d));
        }
    }
}

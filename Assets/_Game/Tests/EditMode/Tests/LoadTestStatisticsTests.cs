using System;
using AFPS.Verification;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class LoadTestStatisticsTests
    {
        [Test]
        public void NearestRankAndBudgetBoundaryPreserveOutlierAndSourceOrder()
        {
            var samples = new double[100];
            for (int index = 0; index < samples.Length; index++)
            {
                samples[index] = 100 - index;
            }
            var result = LoadTestStatistics.Compute(samples, 100, 99);
            Assert.That(result.meanMicroseconds, Is.EqualTo(50.5).Within(0.000001));
            Assert.That(result.p50Microseconds, Is.EqualTo(50));
            Assert.That(result.p95Microseconds, Is.EqualTo(95));
            Assert.That(result.p99Microseconds, Is.EqualTo(99));
            Assert.That(result.maximumMicroseconds, Is.EqualTo(100));
            Assert.That(result.overBudgetCount, Is.EqualTo(1));
            Assert.That(samples[0], Is.EqualTo(100));
        }

        [Test]
        public void UnusedBufferTailDoesNotPolluteSmallSample()
        {
            var result = LoadTestStatistics.Compute(new[] { 0d, 20d, double.NaN }, 2, 20);
            Assert.That(result.count, Is.EqualTo(2));
            Assert.That(result.p50Microseconds, Is.Zero);
            Assert.That(result.p99Microseconds, Is.EqualTo(20));
            Assert.That(result.overBudgetCount, Is.Zero);
        }

        [Test]
        public void MissingAndInvalidMeasurementsCannotProduceSuccessfulStatistics()
        {
            Assert.Throws<ArgumentException>(() => LoadTestStatistics.Compute(null, 1, 20));
            Assert.Throws<ArgumentException>(() => LoadTestStatistics.Compute(new[] { 1d }, 0, 20));
            Assert.Throws<ArgumentException>(() => LoadTestStatistics.Compute(new[] { 1d }, 2, 20));
            foreach (double invalid in new[] { -1d, double.NaN, double.PositiveInfinity })
            {
                Assert.Throws<ArgumentException>(() => LoadTestStatistics.Compute(new[] { invalid }, 1, 20));
                Assert.Throws<ArgumentException>(() => LoadTestStatistics.Compute(new[] { 1d }, 1, invalid));
            }
        }
    }
}

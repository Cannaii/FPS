using System;

namespace AFPS.Verification
{
    [Serializable]
    public sealed class LoadTestStatistics
    {
        public int count;
        public double meanMicroseconds;
        public double p50Microseconds;
        public double p95Microseconds;
        public double p99Microseconds;
        public double maximumMicroseconds;
        public int overBudgetCount;

        /// <summary>微秒采样，使用nearest-rank百分位；保留原数组顺序供CSV复核，不插值掩盖尖峰。</summary>
        public static LoadTestStatistics Compute(double[] samples, int count, double budgetMicroseconds)
        {
            if (samples == null || count <= 0 || count > samples.Length
                || double.IsNaN(budgetMicroseconds) || double.IsInfinity(budgetMicroseconds) || budgetMicroseconds <= 0)
            {
                throw new ArgumentException("统计需要非空有界采样和正微秒预算。");
            }
            var sorted = new double[count];
            var result = new LoadTestStatistics { count = count };
            for (int index = 0; index < count; index++)
            {
                double value = samples[index];
                if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
                {
                    throw new ArgumentException("耗时必须是非负有限微秒。");
                }
                sorted[index] = value;
                result.meanMicroseconds += value / count;
                if (value > budgetMicroseconds)
                {
                    result.overBudgetCount++;
                }
            }
            Array.Sort(sorted);
            result.p50Microseconds = sorted[(int)System.Math.Ceiling(count * 0.50) - 1];
            result.p95Microseconds = sorted[(int)System.Math.Ceiling(count * 0.95) - 1];
            result.p99Microseconds = sorted[(int)System.Math.Ceiling(count * 0.99) - 1];
            result.maximumMicroseconds = sorted[count - 1];
            return result;
        }
    }
}

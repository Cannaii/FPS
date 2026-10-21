using System;
using AFPS.Math;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class FixTests
    {
        [Test]
        public void Arithmetic_UsesGoldenRawValues()
        {
            Fix a = Fix.FromRatio(3, 2), b = Fix.FromInt(2);
            Assert.That((a + b).Raw, Is.EqualTo(229376));
            Assert.That((a - b).Raw, Is.EqualTo(-32768));
            Assert.That((a * b).Raw, Is.EqualTo(196608));
            Assert.That((a / b).Raw, Is.EqualTo(49152));
            Assert.That((-a).Raw, Is.EqualTo(-98304));
            Assert.That((Fix.FromRaw(-65537) % b).Raw, Is.EqualTo(-65537));
            Assert.That((Fix.MinValue % Fix.FromRaw(-1)).Raw, Is.Zero);
        }
        [TestCase(1, 32767, 0)]
        [TestCase(1, 32768, 1)]
        [TestCase(1, 32769, 1)]
        [TestCase(-1, 32767, 0)]
        [TestCase(-1, 32768, -1)]
        [TestCase(-1, 32769, -1)]
        [TestCase(-1, -32768, 1)]
        public void Multiply_RoundsNearestWithTiesAwayFromZero(int a, int b, int expected) =>
            Assert.That((Fix.FromRaw(a) * Fix.FromRaw(b)).Raw, Is.EqualTo(expected));
        [Test]
        public void DivisionAndIntegerConversion_TruncateTowardZero()
        {
            Assert.That((Fix.FromRaw(-1) / Fix.FromInt(3)).Raw, Is.Zero);
            Assert.That((Fix.FromInt(-1) / Fix.FromInt(3)).Raw, Is.EqualTo(-21845));
            Assert.That(Fix.FromRaw(-65535).ToInt(), Is.Zero);
            Assert.That(Fix.FromRaw(-65537).ToInt(), Is.EqualTo(-1));
            Assert.That(Fix.FromRatio(-1, 3).Raw, Is.EqualTo(-21845));
        }
        [Test]
        public void BoundaryConversions_UseExplicitRoundingAndRejectInvalidNumbers()
        {
            Assert.That(Fix.FromFloat(0.5f / 65536f).Raw, Is.EqualTo(1));
            Assert.That(Fix.FromFloat(-0.5f / 65536f).Raw, Is.EqualTo(-1));
            foreach (int raw in new[] { -65537, -32768, -1, 0, 1, 32768, 65537 })
                Assert.That(Fix.FromFloat(Fix.FromRaw(raw).ToFloat()).Raw, Is.EqualTo(raw));
            Assert.Throws<ArgumentOutOfRangeException>(() => Fix.FromFloat(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => Fix.FromDouble(double.PositiveInfinity));
            Assert.Throws<OverflowException>(() => Fix.FromInt(32768));
            Assert.Throws<OverflowException>(() => Fix.FromDouble(32768));
            Assert.That(Fix.FromInt(-32768), Is.EqualTo(Fix.MinValue));
        }
        [Test]
        public void OverflowAndZeroDivisors_NeverWrapSilently()
        {
            Assert.Throws<OverflowException>(() => { var value = Fix.MaxValue + Fix.Epsilon; });
            Assert.Throws<OverflowException>(() => { var value = Fix.MinValue - Fix.Epsilon; });
            Assert.Throws<OverflowException>(() => { var value = -Fix.MinValue; });
            Assert.Throws<OverflowException>(() => { var value = Fix.MaxValue * Fix.FromInt(2); });
            Assert.Throws<OverflowException>(() => { var value = Fix.MinValue / -Fix.One; });
            Assert.Throws<DivideByZeroException>(() => { var value = Fix.One / Fix.Zero; });
            Assert.Throws<DivideByZeroException>(() => { var value = Fix.One % Fix.Zero; });
            Assert.Throws<DivideByZeroException>(() => Fix.FromRatio(1, 0));
        }
        [Test]
        public void ComparisonsAndHash_AreRawBased()
        {
            Fix a = Fix.FromRaw(-1), b = Fix.Zero;
            Assert.That(a < b && a <= b && b > a && b >= a && a != b, Is.True);
            Assert.That(a.CompareTo(b), Is.LessThan(0));
            Assert.That(a.Equals((object)Fix.FromRaw(-1)), Is.True);
            Assert.That(a.GetHashCode(), Is.EqualTo(-1));
        }
        [TestCase(0, 0)]
        [TestCase(1, 256)]
        [TestCase(65536, 65536)]
        [TestCase(131072, 92681)]
        [TestCase(262144, 131072)]
        [TestCase(int.MaxValue, 11863283)]
        public void Sqrt_HasGoldenRawAndFloorBound(int raw, int expected)
        {
            long actual = FixMath.Sqrt(Fix.FromRaw(raw)).Raw;
            long squared = (long)raw * Fix.Scale;
            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(actual * actual, Is.LessThanOrEqualTo(squared));
            Assert.That((actual + 1) * (actual + 1), Is.GreaterThan(squared));
        }
        [Test]
        public void Sqrt_RandomInputsSatisfyExactIntegerBounds()
        {
            FixRandom random = new FixRandom(1234);
            for (int i = 0; i < 10000; i++)
            {
                int raw = (int)(random.NextUInt() & int.MaxValue);
                long result = FixMath.Sqrt(Fix.FromRaw(raw)).Raw, squared = (long)raw * Fix.Scale;
                Assert.That(result * result <= squared && (result + 1) * (result + 1) > squared, Is.True);
            }
            Assert.Throws<ArgumentOutOfRangeException>(() => FixMath.Sqrt(-Fix.One));
        }
        [Test]
        public void Helpers_HandleWholeRangeAndClampEndpoints()
        {
            Assert.That(FixMath.Abs(Fix.FromInt(-2)).Raw, Is.EqualTo(131072));
            Assert.Throws<OverflowException>(() => FixMath.Abs(Fix.MinValue));
            Assert.That(FixMath.Min(Fix.Zero, Fix.One), Is.EqualTo(Fix.Zero));
            Assert.That(FixMath.Max(Fix.Zero, Fix.One), Is.EqualTo(Fix.One));
            Assert.That(FixMath.Clamp01(-Fix.One), Is.EqualTo(Fix.Zero));
            Assert.That(FixMath.Clamp01(Fix.FromInt(2)), Is.EqualTo(Fix.One));
            Assert.That(FixMath.Lerp(Fix.MinValue, Fix.MaxValue, Fix.Zero), Is.EqualTo(Fix.MinValue));
            Assert.That(FixMath.Lerp(Fix.MinValue, Fix.MaxValue, Fix.One), Is.EqualTo(Fix.MaxValue));
            Assert.That(FixMath.Lerp(Fix.MinValue, Fix.MaxValue, Fix.Half).Raw, Is.Zero);
            Assert.That(FixMath.MoveTowards(Fix.MinValue, Fix.MaxValue, Fix.One).Raw, Is.EqualTo(int.MinValue + 65536));
            Assert.That(FixMath.MoveTowards(Fix.Zero, Fix.One, Fix.FromInt(2)), Is.EqualTo(Fix.One));
            Assert.Throws<ArgumentOutOfRangeException>(() => FixMath.MoveTowards(Fix.Zero, Fix.One, -Fix.One));
            Assert.Throws<ArgumentException>(() => FixMath.Clamp(Fix.Zero, Fix.One, Fix.Zero));
        }
    }
}

using AFPS.Math;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class FixTrigTests
    {
        [TestCase(0, 0, 65536)]
        [TestCase(12345, 12272, 64377)]
        [TestCase(102944, 65536, -1)]
        [TestCase(205887, 0, -65536)]
        [TestCase(-12345, -12272, 64377)]
        public void Lookup_HasGoldenRawValues(int angle, int sine, int cosine)
        {
            Assert.That(FixTrig.Sin(Fix.FromRaw(angle)).Raw, Is.EqualTo(sine));
            Assert.That(FixTrig.Cos(Fix.FromRaw(angle)).Raw, Is.EqualTo(cosine));
        }
        [Test]
        public void PeriodicAndExtremeInputs_RemainBounded()
        {
            foreach (int raw in new[] { int.MinValue, -1000000, -1, 0, 1, 1000000, int.MaxValue })
            {
                Fix angle = Fix.FromRaw(raw);
                Assert.That(FixTrig.Sin(angle).Raw, Is.InRange(-65536, 65536));
                Assert.That(FixTrig.Cos(angle).Raw, Is.InRange(-65536, 65536));
                int wrapped = raw % FixTrig.TwoPi.Raw;
                Assert.That(FixTrig.Sin(angle), Is.EqualTo(FixTrig.Sin(Fix.FromRaw(wrapped))));
                Assert.That(FixTrig.Cos(angle), Is.EqualTo(FixTrig.Cos(Fix.FromRaw(wrapped))));
            }
            for (int raw = 0; raw < FixTrig.TwoPi.Raw; raw += 97)
            {
                Fix a = Fix.FromRaw(raw);
                Assert.That(FixTrig.Sin(a), Is.EqualTo(FixTrig.Sin(a + FixTrig.TwoPi)));
                Assert.That(FixTrig.Sin(-a), Is.EqualTo(-FixTrig.Sin(a)));
                Assert.That(FixTrig.Cos(-a), Is.EqualTo(FixTrig.Cos(a)));
            }
        }
        [Test]
        public void FullCycle_MatchesReferenceWithinFourRawUnits()
        {
            // 浮点只用于测试误差预算，运行时表不由平台三角函数生成。
            for (int raw = 0; raw < FixTrig.TwoPi.Raw; raw += 37)
            {
                double phase = raw * (2d * System.Math.PI) / FixTrig.TwoPi.Raw;
                int expected = (int)System.Math.Round(System.Math.Sin(phase) * Fix.Scale, System.MidpointRounding.AwayFromZero);
                Assert.That(System.Math.Abs(FixTrig.Sin(Fix.FromRaw(raw)).Raw - expected), Is.LessThanOrEqualTo(4));
            }
        }
    }
}

using System;
using AFPS.Math;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class FixRandomTests
    {
        [Test]
        public void ZeroSeed_PreservesExistingNetworkImpairmentSequence()
        {
            uint[] golden = { 686983322u, 1172273450u, 4122470071u, 625033959u, 2930321737u, 4220116393u, 3350145879u, 2055117973u };
            FixRandom explicitZero = new FixRandom(0), implicitZero = default, canonical = new FixRandom(FixRandom.ZeroSeed);
            foreach (uint expected in golden)
            {
                Assert.That(explicitZero.NextUInt(), Is.EqualTo(expected));
                Assert.That(implicitZero.NextUInt(), Is.EqualTo(expected));
                Assert.That(canonical.NextUInt(), Is.EqualTo(expected));
            }
        }
        [Test]
        public void SameSeedAndRestoredState_ProduceIdenticalRawSequences()
        {
            FixRandom first = new FixRandom(1234), second = new FixRandom(1234), different = new FixRandom(5678);
            Assert.That(first.State, Is.Not.EqualTo(different.State));
            for (int i = 0; i < 10000; i++) Assert.That(first.NextUInt(), Is.EqualTo(second.NextUInt()));
            FixRandom restored = new FixRandom(first.State);
            for (int i = 0; i < 1000; i++) Assert.That(first.NextUInt(), Is.EqualTo(restored.NextUInt()));
        }
        [Test]
        public void RangeMapping_HandlesFullIntegerRangeAndFixBounds()
        {
            FixRandom random = new FixRandom(1234);
            for (int i = 0; i < 10000; i++)
            {
                Assert.That(random.NextInt(-10, 20), Is.InRange(-10, 19));
                Assert.That(random.NextInt(int.MinValue, int.MaxValue), Is.LessThan(int.MaxValue));
                Assert.That(random.NextFix01().Raw, Is.InRange(0, 65535));
            }
            Assert.That(random.NextInt(3, 4), Is.EqualTo(3));
            Assert.Throws<ArgumentOutOfRangeException>(() => random.NextInt(1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => random.NextInt(2, 1));
        }
        [Test]
        public void RepeatedRawAndTrigSequence_MatchesGoldenHash()
        {
            uint first = HashSequence(), second = HashSequence();
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first, Is.EqualTo(1347168327u));
        }
        private static uint HashSequence()
        {
            uint hash = 2166136261u;
            FixRandom random = new FixRandom(0);
            for (int i = 0; i < 4096; i++)
            {
                int raw = unchecked((int)random.NextUInt());
                Add(ref hash, raw);
                Add(ref hash, FixTrig.Sin(Fix.FromRaw(raw)).Raw);
                Add(ref hash, FixTrig.Cos(Fix.FromRaw(raw)).Raw);
            }
            return hash;
        }
        private static void Add(ref uint hash, int raw)
        {
            for (int shift = 0; shift < 32; shift += 8) hash = unchecked((hash ^ ((uint)raw >> shift & 255u)) * 16777619u);
        }
    }
}

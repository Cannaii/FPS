using System;
using AFPS.Math;
using AFPS.Simulation.Characters;
using AFPS.Simulation.Weapons;
using AFPS.Simulation.World;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class WeaponSimulationConfigTests
    {
        public enum Setting
        {
            Id,
            Mode,
            Magazine,
            Interval,
            Damage,
            Range,
            Speed,
            EyeHeight
        }

        private static WeaponSimulationConfig Projectile(Setting? changed = null) => new WeaponSimulationConfig(
            (ushort)(changed == Setting.Id ? 2 : 1),
            changed == Setting.Mode ? WeaponFireMode.Hitscan : WeaponFireMode.Projectile,
            changed == Setting.Magazine ? 31 : 30,
            changed == Setting.Interval ? 6u : 5u,
            Fix.FromRaw(1310720 + (changed == Setting.Damage ? 1 : 0)),
            Fix.FromRaw(13107200 + (changed == Setting.Range ? 1 : 0)),
            Fix.FromRaw(3932160 + (changed == Setting.Speed ? 1 : 0)),
            Fix.FromRaw(104858 + (changed == Setting.EyeHeight ? 1 : 0)));

        private static SimulationWorldContext Context(WeaponSimulationConfig weapon) => new SimulationWorldContext(
            new FixedWorld(null, null),
            new PlayerSimulationConfig(Fix.FromInt(6), Fix.FromInt(20), Fix.FromInt(20), Fix.FromInt(8)),
            SimulationStep.Step50Hz, weaponConfiguration: weapon);

        [TestCase(Setting.Id)]
        [TestCase(Setting.Mode)]
        [TestCase(Setting.Magazine)]
        [TestCase(Setting.Interval)]
        [TestCase(Setting.Damage)]
        [TestCase(Setting.Range)]
        [TestCase(Setting.Speed)]
        [TestCase(Setting.EyeHeight)]
        public void EveryEffectiveWeaponRuleChangesCompatibilityHash(Setting changed)
        {
            var baseline = Context(Projectile());
            var variant = Context(Projectile(changed));
            Assert.That(variant.Identity.WorldHash, Is.EqualTo(baseline.Identity.WorldHash));
            Assert.That(variant.Identity.ConfigurationHash, Is.Not.EqualTo(baseline.Identity.ConfigurationHash));
        }

        [Test]
        public void FixedConstructorPreservesExtremeRawAndFullTickRange()
        {
            var weapon = new WeaponSimulationConfig(ushort.MaxValue, WeaponFireMode.Projectile,
                int.MaxValue, uint.MaxValue, Fix.FromRaw(int.MaxValue - 1),
                Fix.MaxValue, Fix.Epsilon, Fix.Epsilon);
            Assert.That(weapon.IsValid, Is.True);
            Assert.That(weapon.DamageFixed.Raw, Is.EqualTo(int.MaxValue - 1));
            Assert.That(weapon.RangeFixed.Raw, Is.EqualTo(int.MaxValue));
            Assert.That(weapon.ProjectileSpeedFixed.Raw, Is.EqualTo(1));
            Assert.That(weapon.FireIntervalTicks, Is.EqualTo(uint.MaxValue));
            Assert.That(Context(weapon).WeaponConfiguration.DamageFixed, Is.EqualTo(weapon.DamageFixed));
        }

        [Test]
        public void FloatBoundaryUsesCanonicalRoundingAndRejectsSubRawPositiveRules()
        {
            var weapon = new WeaponSimulationConfig(1, WeaponFireMode.Hitscan, 30, 5,
                0.5f / 65536f, 200f, 0f, 1.6f);
            Assert.That(weapon.DamageFixed.Raw, Is.EqualTo(1));
            Assert.That(weapon.EyeHeightFixed.Raw, Is.EqualTo(104858));
            Assert.That(WeaponSimulationConfig.DefaultRifle.EyeHeightFixed, Is.EqualTo(weapon.EyeHeightFixed));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WeaponSimulationConfig(
                1, WeaponFireMode.Hitscan, 30, 5, 0.25f / 65536f, 200f, 0f, 1.6f));
            Assert.Throws<OverflowException>(() => new WeaponSimulationConfig(
                1, WeaponFireMode.Hitscan, 30, 5, 20f, float.MaxValue, 0f, 1.6f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WeaponSimulationConfig(
                1, WeaponFireMode.Hitscan, 30, 5, 20f, 200f, 0f, -float.Epsilon));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void NonFiniteDamageCannotCreateValidWeapon(float damage)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new WeaponSimulationConfig(
                1, WeaponFireMode.Hitscan, 30, 5, damage, 200f, 0f, 1.6f));
        }

        [Test]
        public void UnusedHitscanSpeedIsCanonicalAndDoesNotSplitIdentity()
        {
            var a = new WeaponSimulationConfig(1, WeaponFireMode.Hitscan, 30, 5,
                20f, 200f, float.NaN, 1.6f);
            var b = new WeaponSimulationConfig(1, WeaponFireMode.Hitscan, 30, 5,
                Fix.FromInt(20), Fix.FromInt(200), Fix.MaxValue, Fix.FromRaw(104858));
            Assert.That(a.ProjectileSpeedFixed, Is.EqualTo(Fix.Zero));
            Assert.That(b.ProjectileSpeedFixed, Is.EqualTo(Fix.Zero));
            Assert.That(Context(a).Identity, Is.EqualTo(Context(b).Identity));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        public void InvalidFixedRulesFailAtConstruction(int invalid)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new WeaponSimulationConfig(
                invalid == 0 ? (ushort)0 : (ushort)1,
                invalid == 1 ? (WeaponFireMode)0 : WeaponFireMode.Projectile,
                invalid == 2 ? 0 : 30, invalid == 3 ? 0u : 5u,
                invalid == 4 ? Fix.Zero : Fix.One, invalid == 5 ? Fix.Zero : Fix.One,
                invalid == 6 ? Fix.Zero : Fix.One, invalid == 7 ? -Fix.Epsilon : Fix.Zero));
        }

        [Test]
        public void DefaultStructCannotEnterWorldContext()
        {
            Assert.That(default(WeaponSimulationConfig).IsValid, Is.False);
            Assert.Throws<ArgumentException>(() => Context(default));
        }

        [Test]
        public void IntervalHighBitIsPartOfCanonicalFingerprint()
        {
            var a = WeaponSimulationConfig.DefaultRifle;
            var b = new WeaponSimulationConfig(a.WeaponId, a.FireMode, a.MagazineCapacity,
                0x80000005u, a.DamageFixed, a.RangeFixed, a.ProjectileSpeedFixed, a.EyeHeightFixed);
            Assert.That(Context(a).Identity.ConfigurationHash, Is.Not.EqualTo(Context(b).Identity.ConfigurationHash));
        }
    }
}

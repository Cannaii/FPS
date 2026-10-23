using AFPS.Math;
using AFPS.NetCode.Weapons;
using AFPS.Presentation.Weapons;
using AFPS.Simulation.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace AFPS.Tests.EditMode
{
    public sealed class ShotResultViewTests
    {
        [TestCase(1u, 80, true, false)]
        [TestCase(1u, 0, true, true)]
        [TestCase(2u, 0, false, false)]
        [TestCase(0u, 0, false, false)]
        public void Consume_OnlyLocalConfirmedHitShowsMarker(uint localEntity, int health, bool visible, bool killed)
        {
            var target = new GameObject("Shot result test");
            try
            {
                var view = target.AddComponent<ShotResultView>();
                view.Consume(Result(WeaponFireMode.Hitscan, true, health), localEntity);
                Assert.That(view.HitMarkerVisible, Is.EqualTo(visible));
                Assert.That(view.LastHitWasKill, Is.EqualTo(killed));
                view.Advance(1f);
                Assert.That(view.HitMarkerVisible, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void Consume_UsesAuthoritativeTracerEndpoints()
        {
            var target = new GameObject("Shot result test");
            try
            {
                var view = target.AddComponent<ShotResultView>();
                var result = Result(WeaponFireMode.Hitscan, false, 0);
                view.Consume(result, 1);
                var line = target.GetComponentInChildren<LineRenderer>();
                Assert.That(line, Is.Not.Null);
                Assert.That(line.GetPosition(0), Is.EqualTo(result.Shot.Origin));
                Assert.That(line.GetPosition(1), Is.EqualTo(result.HitPoint));
                Assert.That(view.HitMarkerVisible, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void Consume_ProjectileLaunchDoesNotDrawRangeBeamAndImpactShowsFeedback()
        {
            var target = new GameObject("Shot result test");
            try
            {
                var view = target.AddComponent<ShotResultView>();
                view.Consume(Result(WeaponFireMode.Projectile, false, 0), 1);
                Assert.That(target.GetComponentsInChildren<LineRenderer>().Length, Is.Zero);
                Assert.That(view.HitMarkerVisible, Is.False);
                view.Consume(Result(WeaponFireMode.Projectile, true, 0), 1);
                Assert.That(view.LocalHitConfirmationCount, Is.EqualTo(1));
                Assert.That(view.ConsumedResultCount, Is.EqualTo(2));
                Assert.That(view.HitMarkerVisible, Is.True);
                Assert.That(view.LastHitWasKill, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void Consume_BoundsVisualPoolAndClearsFeedbackBetweenSessions()
        {
            var target = new GameObject("Shot result test");
            try
            {
                var view = target.AddComponent<ShotResultView>();
                for (int i = 0; i < ShotResultView.MaximumLineCount + 20; i++)
                {
                    view.Consume(Result(WeaponFireMode.Hitscan, true, 80), 1);
                }
                var lines = target.GetComponentsInChildren<LineRenderer>();
                Assert.That(lines.Length, Is.EqualTo(ShotResultView.MaximumLineCount));
                view.Advance(1f);
                foreach (var line in lines)
                {
                    Assert.That(line.enabled, Is.False);
                }
                view.Consume(Result(WeaponFireMode.Hitscan, true, 0), 1);
                view.Clear();
                Assert.That(view.HitMarkerVisible, Is.False);
                Assert.That(view.LastHitWasKill, Is.False);
                Assert.That(view.ConsumedResultCount, Is.Zero);
                Assert.That(view.LocalHitConfirmationCount, Is.Zero);
                foreach (var line in lines)
                {
                    Assert.That(line.enabled, Is.False);
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        private static AuthoritativeShotResult Result(WeaponFireMode mode, bool hit, int health)
        {
            var shot = new AuthoritativeShot(1, 1, 100, 1, mode,
                new FixVec3(Fix.Zero, Fix.One, Fix.Zero), FixVec3.Forward,
                Fix.FromInt(20), Fix.FromInt(100), mode == WeaponFireMode.Projectile ? Fix.FromInt(10) : Fix.Zero);
            return new AuthoritativeShotResult(shot, 29, hit, hit ? 2u : 0u,
                Fix.FromInt(health), new FixVec3(Fix.Zero, Fix.One, Fix.FromInt(5)));
        }
    }
}

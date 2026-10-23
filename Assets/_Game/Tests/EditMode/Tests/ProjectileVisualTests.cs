using System.Reflection;
using AFPS.Math;
using AFPS.NetCode.Weapons;
using AFPS.Presentation.Weapons;
using AFPS.Simulation.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace AFPS.Tests.EditMode
{
    public sealed class ProjectileVisualTests
    {
        [Test]
        public void Confirmation_CorrectsExistingProxyAndImpactPreventsLateLaunchResurrection()
        {
            var root = new GameObject("Projectile confirmation test");
            try
            {
                var view = root.AddComponent<ShotResultView>();
                var predicted = Shot(1, 1);
                Assert.That(view.PredictProjectile(predicted), Is.True);
                view.Advance(0.1f);
                Assert.That(view.TryGetProjectilePosition(1, 1, out var position), Is.True);
                Assert.That(position.z, Is.EqualTo(1f).Within(0.001f));
                var corrected = new AuthoritativeShot(1, 1, 100, 1, WeaponFireMode.Projectile,
                    new FixVec3(Fix.FromInt(5), Fix.One, Fix.Zero), FixVec3.Right,
                    Fix.FromInt(20), Fix.FromInt(10), Fix.FromInt(10));
                view.Consume(Result(corrected, false), 1, 104d, 0.02f);
                Assert.That(view.ActiveProjectileCount, Is.EqualTo(1));
                Assert.That(view.CreatedProjectileVisualCount, Is.EqualTo(1));
                Assert.That(view.TryGetProjectilePosition(1, 1, out position), Is.True);
                Assert.That(position.x, Is.EqualTo(6f).Within(0.001f));
                Assert.That(position.z, Is.Zero);
                view.Consume(Result(corrected, true), 1);
                Assert.That(view.ActiveProjectileCount, Is.Zero);
                Assert.That(view.HitMarkerVisible, Is.True);
                view.Consume(Result(corrected, false), 1, 105d, 0.02f);
                Assert.That(view.ActiveProjectileCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Tracking_SeparatesDifferentEntitiesWithSameShotSequence()
        {
            var root = new GameObject("Projectile identity test");
            try
            {
                var view = root.AddComponent<ShotResultView>();
                Assert.That(view.PredictProjectile(Shot(1, 1)), Is.True);
                Assert.That(view.PredictProjectile(Shot(2, 1)), Is.True);
                view.Consume(Result(Shot(1, 1), true), 1);
                Assert.That(view.TryGetProjectilePosition(1, 1, out _), Is.False);
                Assert.That(view.TryGetProjectilePosition(2, 1, out _), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PredictionTimeout_AllowsLateAuthorityAndReusesRetiredProxy()
        {
            var root = new GameObject("Projectile timeout test");
            try
            {
                var view = root.AddComponent<ShotResultView>();
                var shot = new AuthoritativeShot(1, 1, 100, 1, WeaponFireMode.Projectile,
                    new FixVec3(Fix.Zero, Fix.One, Fix.Zero), FixVec3.Forward,
                    Fix.FromInt(20), Fix.FromInt(100), Fix.FromInt(10));
                view.PredictProjectile(shot);
                view.Advance(ShotResultView.PredictionTimeoutSeconds);
                Assert.That(view.ActiveProjectileCount, Is.Zero);
                view.Consume(Result(shot, false), 1, 110d, 0.02f);
                Assert.That(view.ActiveProjectileCount, Is.EqualTo(1));
                Assert.That(view.CreatedProjectileVisualCount, Is.EqualTo(1));
                Assert.That(view.TryGetProjectilePosition(1, 1, out var position), Is.True);
                Assert.That(position.z, Is.EqualTo(2.2f).Within(0.001f));
                view.Advance(1.2f);
                Assert.That(view.ActiveProjectileCount, Is.EqualTo(1), "已确认代理不受一秒预测超时限制。");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Confirmation_WithoutClockPreservesPredictedTravelTime()
        {
            var root = new GameObject("Projectile clock fallback test");
            try
            {
                var view = root.AddComponent<ShotResultView>();
                var shot = Shot(1, 1);
                view.PredictProjectile(shot);
                view.Advance(0.25f);
                view.Consume(Result(shot, false), 1);
                Assert.That(view.TryGetProjectilePosition(1, 1, out var position), Is.True);
                Assert.That(position.z, Is.EqualTo(2.5f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Lifecycle_RangeExpiryDelayedLaunchAndSessionClear()
        {
            var root = new GameObject("Projectile lifecycle test");
            try
            {
                var view = root.AddComponent<ShotResultView>();
                var shot = Shot(1, 1);
                view.Consume(Result(shot, false), 1, 200d, 0.02f);
                Assert.That(view.ActiveProjectileCount, Is.Zero);
                view.Consume(Result(shot, false), 1, 100d, 0.02f);
                Assert.That(view.ActiveProjectileCount, Is.Zero, "到期代理不能被迟到发射重复创建。");
                view.Clear();
                Assert.That(view.PredictProjectile(shot), Is.True, "新会话需要允许重新使用旧服务器的身份数值。");
                view.Clear();
                Assert.That(view.ActiveProjectileCount, Is.Zero);
                foreach (var proxy in root.GetComponentsInChildren<PredictedProjectileVisual>(true))
                {
                    Assert.That(proxy.gameObject.activeSelf, Is.False);
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Pool_BoundsBurstAllocationsAndReusesClearedObjects()
        {
            var root = new GameObject("Projectile pool test");
            try
            {
                var view = root.AddComponent<ShotResultView>();
                for (uint sequence = 1; sequence <= ShotResultView.MaximumProjectileCount + 20; sequence++)
                {
                    Assert.That(view.PredictProjectile(Shot(1, sequence)), Is.True);
                }
                Assert.That(view.ActiveProjectileCount, Is.EqualTo(ShotResultView.MaximumProjectileCount));
                Assert.That(view.CreatedProjectileVisualCount, Is.EqualTo(ShotResultView.MaximumProjectileCount));
                view.Clear();
                Assert.That(view.PredictProjectile(Shot(1, 1000)), Is.True);
                Assert.That(view.CreatedProjectileVisualCount, Is.EqualTo(ShotResultView.MaximumProjectileCount));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Prefab_CollidersAreDisabledAndHitscanDoesNotCreateProxy()
        {
            var root = new GameObject("Projectile prefab test");
            var prefabObject = new GameObject("Projectile source",
                typeof(SphereCollider), typeof(Rigidbody), typeof(PredictedProjectileVisual));
            try
            {
                var view = root.AddComponent<ShotResultView>();
                typeof(ShotResultView).GetField("projectileVisualPrefab", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(view, prefabObject.GetComponent<PredictedProjectileVisual>());
                var hitscan = new AuthoritativeShot(1, 2, 100, WeaponSimulationConfig.DefaultRifle,
                    FixVec3.Zero, FixVec3.Forward);
                Assert.That(view.PredictProjectile(hitscan), Is.False);
                Assert.That(view.PredictProjectile(Shot(1, 1)), Is.True);
                foreach (var collider in root.GetComponentsInChildren<Collider>(true))
                {
                    Assert.That(collider.enabled, Is.False);
                }
                foreach (var body in root.GetComponentsInChildren<Rigidbody>(true))
                {
                    Assert.That(body.isKinematic, Is.True);
                    Assert.That(body.useGravity, Is.False);
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(prefabObject);
            }
        }

        [Test]
        public void ConfirmedSlowProjectile_ExpiresAtVisualLifetimeLimit()
        {
            var root = new GameObject("Slow projectile lifetime test");
            try
            {
                var view = root.AddComponent<ShotResultView>();
                var shot = new AuthoritativeShot(1, 1, 100, 1, WeaponFireMode.Projectile,
                    FixVec3.Zero, FixVec3.Forward, Fix.One, Fix.FromInt(10), Fix.Epsilon);
                view.Consume(Result(shot, false), 1);
                view.Advance(PredictedProjectileVisual.MaximumLifetimeSeconds);
                Assert.That(view.ActiveProjectileCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [TestCase(100u, 100d, 0.02f)]
        [TestCase(100u, 104.5d, 0.11f)]
        [TestCase(uint.MaxValue - 1, 0d, 0.06f)]
        [TestCase(100u, 99d, 0.02f)]
        [TestCase(100u, double.NaN, 0f)]
        public void Clock_IncludesSpawnStepAndSupportsTickWrap(uint shotTick, double currentTick, float expectedSeconds)
        {
            Assert.That(ShotResultView.EstimateProjectileAge(shotTick, currentTick, 0.02f),
                Is.EqualTo(expectedSeconds).Within(0.00001f));
        }

        private static AuthoritativeShot Shot(uint entity, uint sequence) =>
            new AuthoritativeShot(entity, sequence, 100, 1, WeaponFireMode.Projectile,
                new FixVec3(Fix.Zero, Fix.One, Fix.Zero), FixVec3.Forward,
                Fix.FromInt(20), Fix.FromInt(10), Fix.FromInt(10));

        private static AuthoritativeShotResult Result(in AuthoritativeShot shot, bool hit) =>
            new AuthoritativeShotResult(shot, 29, hit, hit ? 2u : 0u, hit ? Fix.FromInt(80) : Fix.Zero,
                new FixVec3(Fix.Zero, Fix.One, Fix.FromInt(5)));
    }
}

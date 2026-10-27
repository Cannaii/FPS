using AFPS.Bootstrap;
using AFPS.Math;
using AFPS.NetCode.Weapons;
using AFPS.Presentation.Weapons;
using AFPS.Simulation.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace AFPS.Tests.EditMode
{
    public sealed class CombatPresentationFixTests
    {
        [Test]
        public void BundledFont_CoversChineseCombatAndMenuLabels()
        {
            Font font = ChineseUiFont.Source;
            Assert.That(font, Is.Not.Null);
            foreach (char character in "生命体力突进死亡换弹攀爬蹬墙钩索局域网创建房间进入游戏网络延迟重生胜利▲…")
            {
                Assert.That(font.HasCharacter(character), Is.True, character.ToString());
            }
        }

        [TestCase(WeaponFireMode.Hitscan, 0f, 1f, 0f, 0f, 1f)]
        [TestCase(WeaponFireMode.Hitscan, 1f, 0f, 0f, 1f, 0f)]
        [TestCase(WeaponFireMode.Hitscan, 0f, -1f, 0f, 0f, -1f)]
        [TestCase(WeaponFireMode.Hitscan, -1f, 0f, 0f, -1f, 0f)]
        [TestCase(WeaponFireMode.Projectile, 0f, 1f, 0f, 0f, 1f)]
        [TestCase(WeaponFireMode.Projectile, 1f, 0f, 0f, 1f, 0f)]
        [TestCase(WeaponFireMode.Projectile, 0f, -1f, 0f, 0f, -1f)]
        [TestCase(WeaponFireMode.Projectile, -1f, 0f, 0f, -1f, 0f)]
        [TestCase(WeaponFireMode.Projectile, 0f, 1f, 90f, -1f, 0f)]
        public void IncomingDamage_UsesAttackerSideAndTracksViewerYaw(
            WeaponFireMode mode, float x, float z, float yaw, float expectedX, float expectedY)
        {
            var root = new GameObject("Damage direction test");
            try
            {
                var view = root.AddComponent<ShotResultView>();
                var direction = new FixVec3(Fix.FromFloat(-x), Fix.Zero, Fix.FromFloat(-z));
                var shot = Shot(mode, direction);
                view.Consume(new AuthoritativeShotResult(shot, 4, true, 1, Fix.One, FixVec3.Zero), 1);
                Assert.That(view.TryGetDamageDirection(Vector3.zero, Quaternion.Euler(70f, yaw, 20f), out var indicator), Is.True);
                Assert.That(indicator.x, Is.EqualTo(expectedX).Within(0.0001f));
                Assert.That(indicator.y, Is.EqualTo(expectedY).Within(0.0001f));
                view.Advance(0.4f);
                Assert.That(view.TryGetDamageDirection(Vector3.zero, Quaternion.identity, out _), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void VerticalDamage_DoesNotInventForwardDirection_AndSplashUsesExplosionCenter()
        {
            var root = new GameObject("Vertical damage test");
            try
            {
                var view = root.AddComponent<ShotResultView>();
                var shot = Shot(WeaponFireMode.Projectile, -FixVec3.Up);
                view.Consume(new AuthoritativeShotResult(shot, 4, true, 1, Fix.One, FixVec3.Zero), 1);
                Assert.That(view.DamageFeedbackVisible, Is.True);
                Assert.That(view.TryGetDamageDirection(Vector3.zero, Quaternion.identity, out _), Is.False);
                view.Consume(new AuthoritativeShotResult(shot, 4, true, 1, Fix.One, FixVec3.Right * Fix.FromInt(2)), 1);
                Assert.That(view.TryGetDamageDirection(Vector3.zero, Quaternion.identity, out var direction), Is.True);
                Assert.That(direction, Is.EqualTo(Vector2.right));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [TestCase(WeaponFireMode.Hitscan)]
        [TestCase(WeaponFireMode.Projectile)]
        public void VisualStartsAtRememberedMuzzle_WithoutChangingAuthoritativeShot(WeaponFireMode mode)
        {
            var root = new GameObject("Muzzle separation test");
            try
            {
                var view = root.AddComponent<ShotResultView>();
                var shot = Shot(mode, FixVec3.Forward);
                Vector3 muzzle = new Vector3(0.3f, 0.8f, 0.7f);
                view.RememberLocalMuzzle(shot.EntityId, shot.ShotSequence, muzzle);
                var result = new AuthoritativeShotResult(shot, 4, false, 0, Fix.Zero, FixVec3.Forward * Fix.FromInt(20));
                if (mode == WeaponFireMode.Projectile)
                {
                    view.PredictProjectile(shot);
                    view.Consume(result, shot.EntityId);
                    Assert.That(view.TryGetProjectilePosition(shot.EntityId, shot.ShotSequence, out var point), Is.True);
                    Assert.That(point, Is.EqualTo(muzzle));
                    view.Advance(1f);
                    view.TryGetProjectilePosition(shot.EntityId, shot.ShotSequence, out point);
                    Assert.That(point, Is.EqualTo(shot.Origin + shot.Direction * shot.ProjectileSpeed));
                }
                else
                {
                    view.Consume(result, shot.EntityId);
                    var line = root.GetComponentInChildren<LineRenderer>();
                    Assert.That(line.GetPosition(0), Is.EqualTo(muzzle));
                    Assert.That(line.GetPosition(1), Is.EqualTo(result.HitPoint));
                }
                Assert.That(result.Shot.OriginFixed, Is.EqualTo(FixVec3.Zero));
                Assert.That(result.Shot.DirectionFixed, Is.EqualTo(FixVec3.Forward));
                view.Clear();
                view.Consume(result, shot.EntityId);
                if (mode == WeaponFireMode.Hitscan)
                {
                    foreach (var line in root.GetComponentsInChildren<LineRenderer>())
                    {
                        if (line.enabled)
                        {
                            Assert.That(line.GetPosition(0), Is.EqualTo(shot.Origin));
                        }
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static AuthoritativeShot Shot(WeaponFireMode mode, FixVec3 direction) =>
            new AuthoritativeShot(2, 1, 100, 1, mode, FixVec3.Zero, direction,
                Fix.FromInt(20), Fix.FromInt(100), mode == WeaponFireMode.Projectile ? Fix.FromInt(10) : Fix.Zero);

        [TestCase(60f)]
        [TestCase(100f)]
        public void MuzzleProjection_AlignsWorldTracerWithWeaponCamera(float fieldOfView)
        {
            var root = new GameObject("Muzzle projection test");
            try
            {
                var camera = root.AddComponent<Camera>();
                camera.fieldOfView = fieldOfView;
                root.transform.rotation = Quaternion.Euler(-23f, 79f, 12f);
                var weapon = new GameObject("Weapon view");
                weapon.transform.SetParent(root.transform, false);
                var view = weapon.AddComponent<FirstPersonWeaponView>();
                view.Configure(camera);
                view.SetWeapon(WeaponSimulationConfig.DefaultRifle);
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var weaponCamera = (Camera)typeof(FirstPersonWeaponView).GetField("weaponCamera", flags).GetValue(view);
                var pivot = (Transform)typeof(FirstPersonWeaponView).GetField("modelPivot", flags).GetValue(view);
                var muzzle = (Vector3)typeof(FirstPersonWeaponView).GetField("muzzlePoint", flags).GetValue(view);
                Assert.That(view.TryGetWorldMuzzle(out var point), Is.True);
                Vector3 expected = weaponCamera.WorldToViewportPoint(pivot.TransformPoint(muzzle));
                Vector3 actual = camera.WorldToViewportPoint(point);
                Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.0001f));
                Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.0001f));
                Assert.That(actual.z, Is.GreaterThan(camera.nearClipPlane));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}

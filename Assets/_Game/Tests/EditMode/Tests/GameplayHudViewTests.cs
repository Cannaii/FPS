using System.IO;
using System.Reflection;
using AFPS.Bootstrap;
using AFPS.Editor.UI;
using AFPS.Math;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Weapons;
using AFPS.Presentation.Weapons;
using AFPS.Simulation.Weapons;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace AFPS.Tests.EditMode
{
    public sealed class GameplayHudViewTests
    {
        [Test]
        public void ShippedPrefab_HasPersistentMaterialsAndDoesNotBlockInput()
        {
            var prefab = Resources.Load<GameplayHudView>(GameplayHudView.ResourcePath);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.TryValidate(out string error), Is.True, error);
            Assert.That(prefab.Canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            Assert.That(prefab.GetComponent<GraphicRaycaster>(), Is.Null);
            Assert.That(prefab.DamageFlash.enabled, Is.False);
            foreach (var graphic in prefab.GetComponentsInChildren<Graphic>(true))
            {
                Assert.That(graphic.raycastTarget, Is.False, graphic.name);
            }

            foreach (var text in prefab.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                Assert.That(AssetDatabase.GetAssetPath(text.font), Is.EqualTo(NetworkLobbyAssetBuilder.FontPath));
                Assert.That(AssetDatabase.GetAssetPath(text.fontSharedMaterial),
                    Is.EqualTo(GameplayHudAssetBuilder.MaterialPath));
            }

            Assert.That(prefab.ScoreText.transform.IsChildOf(prefab.ScorePanel.transform), Is.True);
        }

        [Test]
        public void Reconfigure_PreservesEditedLayoutAndDoesNotDuplicateCanvas()
        {
            var root = new GameObject("HUD binding test");
            try
            {
                var hud = root.AddComponent<GameplayHud>();
                hud.Configure(null, 0.02f);
                var view = root.GetComponentInChildren<GameplayHudView>(true);
                Assert.That(view, Is.Not.Null);
                Assert.That(view.name, Is.EqualTo("Gameplay HUD"));
                Assert.That(view.Canvas.enabled, Is.False);
                view.HealthText.fontSize = 31f;
                view.HealthText.rectTransform.anchoredPosition = new Vector2(12f, 15f);
                view.WeaponText.color = Color.cyan;
                hud.Configure(null, 0.02f);
                Assert.That(root.GetComponentsInChildren<Canvas>(true).Length, Is.EqualTo(1));
                Assert.That(view.HealthText.fontSize, Is.EqualTo(31f));
                Assert.That(view.HealthText.rectTransform.anchoredPosition, Is.EqualTo(new Vector2(12f, 15f)));
                Assert.That(view.WeaponText.color, Is.EqualTo(Color.cyan));
                view.Canvas.enabled = true;
                hud.enabled = false;
                // EditMode不会自动调度普通MonoBehaviour生命周期，显式验证其资源清理边界。
                Invoke(hud, "OnDisable");
                Assert.That(view.Canvas.enabled, Is.False);
                Invoke(hud, "OnDestroy");
                Object.DestroyImmediate(hud);
                Assert.That(view == null, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RealFeedback_DrivesBoundWidgetsAndExpires()
        {
            var root = new GameObject("HUD feedback test");
            GameObject cameraRoot = null;
            try
            {
                if (Camera.main == null)
                {
                    cameraRoot = new GameObject("HUD feedback camera");
                    cameraRoot.tag = "MainCamera";
                    cameraRoot.AddComponent<Camera>();
                }

                var shots = root.AddComponent<ShotPresentationController>();
                var hud = root.AddComponent<GameplayHud>();
                hud.Configure(null, 0.02f, shots);
                var view = root.GetComponentInChildren<GameplayHudView>(true);
                var shot = new AuthoritativeShot(2, 1, 100, 1, WeaponFireMode.Hitscan,
                    new FixVec3(Fix.Zero, Fix.One, Fix.Zero), FixVec3.Forward,
                    Fix.FromInt(20), Fix.FromInt(100), Fix.Zero);
                shots.Consume(new AuthoritativeShotResult(shot, 29, true, 1,
                    Fix.FromInt(80), FixVec3.Zero), 1);
                shots.Reject(new ShotRejection(1, 2, 3, 100, WeaponFireRejectReason.EmptyMagazine));
                Invoke(hud, "UpdateCombatFeedback");
                Assert.That(view.DamageFlash.enabled, Is.True);
                Assert.That(view.DamageDirectionText.text, Is.EqualTo("▲"));
                Assert.That(view.RejectionText.text, Does.Contain("弹匣已空"));
                Assert.That(shots.TryGetDamageDirection(Camera.main.transform.position,
                    Camera.main.transform.rotation, out Vector2 direction), Is.True);
                Assert.That(Vector2.Distance(view.DamageDirectionText.rectTransform.anchoredPosition,
                    direction * view.DamageDirectionRadius), Is.LessThan(0.001f));
                shots.Advance(1f);
                Invoke(hud, "UpdateCombatFeedback");
                Assert.That(view.DamageFlash.enabled, Is.False);
                Assert.That(view.DamageDirectionText.text, Is.Empty);
                Assert.That(view.RejectionText.text, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(root);
                if (cameraRoot != null)
                {
                    Object.DestroyImmediate(cameraRoot);
                }
            }
        }

        [Test]
        public void MissingBinding_ReportsWidgetName()
        {
            var view = Object.Instantiate(Resources.Load<GameplayHudView>(GameplayHudView.ResourcePath));
            try
            {
                var serialized = new SerializedObject(view);
                serialized.FindProperty("damageFlash").objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(view.TryValidate(out string error), Is.False);
                Assert.That(error, Does.Contain("damageFlash"));
            }
            finally
            {
                Object.DestroyImmediate(view.gameObject);
            }
        }

        [Test]
        public void Generator_PreservesExistingResourcesAndSharedFont()
        {
            byte[] prefab = File.ReadAllBytes(GameplayHudAssetBuilder.PrefabPath);
            byte[] material = File.ReadAllBytes(GameplayHudAssetBuilder.MaterialPath);
            byte[] font = File.ReadAllBytes(NetworkLobbyAssetBuilder.FontPath);
            GameplayHudAssetBuilder.Create();
            Assert.That(File.ReadAllBytes(GameplayHudAssetBuilder.PrefabPath), Is.EqualTo(prefab));
            Assert.That(File.ReadAllBytes(GameplayHudAssetBuilder.MaterialPath), Is.EqualTo(material));
            Assert.That(File.ReadAllBytes(NetworkLobbyAssetBuilder.FontPath), Is.EqualTo(font));
        }

        private static void Invoke(GameplayHud hud, string method)
        {
            typeof(GameplayHud).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(hud, null);
        }
    }
}

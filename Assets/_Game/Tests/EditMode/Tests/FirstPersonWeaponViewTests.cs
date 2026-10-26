using AFPS.Presentation.Weapons;
using AFPS.Simulation.Weapons;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class FirstPersonWeaponViewTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void DefaultScene_HasOneVisibleArmsMeshAfterWeaponAssembly(bool launcher)
    {
        var scene = EditorSceneManager.OpenScene("Assets/_Game/Scenes/MyLevel/SampleScene.unity", OpenSceneMode.Additive);
        GameObject viewRoot = null;
        try
        {
            Camera camera = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var candidate in root.GetComponentsInChildren<Camera>(true))
                {
                    if (candidate.CompareTag("MainCamera"))
                    {
                        camera = candidate;
                    }
                }
            }
            Assert.That(camera, Is.Not.Null);
            Assert.That(camera.GetComponentsInChildren<Renderer>(true), Is.Empty,
                "默认相机不能预挂旧手臂；武器视图会在运行时按当前武器创建带动画的模型。");
            viewRoot = new GameObject("Runtime weapon regression");
            viewRoot.transform.SetParent(camera.transform, false);
            var view = viewRoot.AddComponent<FirstPersonWeaponView>();
            view.Configure(camera);
            view.SetWeapon(WeaponSimulationConfig.DefaultRifle);
            if (launcher)
            {
                view.SetWeapon(WeaponCatalog.SecondaryFor(WeaponSimulationConfig.DefaultRifle));
            }
            int visibleArms = 0;
            foreach (var renderer in camera.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (renderer.enabled && renderer.name.IndexOf("arm", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    visibleArms++;
                    Assert.That(camera.cullingMask & (1 << renderer.gameObject.layer), Is.Zero,
                        "手臂只能由独立武器相机绘制，主相机不能重复绘制。");
                }
            }
            Assert.That(visibleArms, Is.EqualTo(1));
            Assert.That(view.HasOwnedModel, Is.True);
        }
        finally
        {
            if (viewRoot != null)
            {
                Object.DestroyImmediate(viewRoot);
            }
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void PlayPredictedShot_WithOptionalEffectsUnassigned_DoesNotThrow()
    {
        GameObject gameObject = new GameObject("FirstPersonWeaponViewTest");
        try
        {
            FirstPersonWeaponView view = gameObject.AddComponent<FirstPersonWeaponView>();

            Assert.DoesNotThrow(view.PlayPredictedShot);
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }
}

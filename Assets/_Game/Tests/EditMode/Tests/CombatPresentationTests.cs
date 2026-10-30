using System.Linq;
using AFPS.Math;
using AFPS.NetCode.Weapons;
using AFPS.Presentation.Camera;
using AFPS.Presentation.Weapons;
using AFPS.Simulation.Characters;
using AFPS.Simulation.Weapons;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AFPS.Tests.EditMode
{
    public sealed class CombatPresentationTests
    {
        private static void InvokeLifecycle(MonoBehaviour component, string method)
        {
            component.GetType().GetMethod(method,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(component, null);
        }

        [TestCase("Rifle")]
        [TestCase("Launcher")]
        public void OwnedModels_HaveSelfContainedAnimationAndAudio(string name)
        {
            string root = "Assets/_Game/Art/Resources/Combat/";
            GameObject model = CombatPresentationAssets.Load<GameObject>(name);
            Assert.That(model, Is.Not.Null);
            Assert.That(model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length, Is.GreaterThan(0));
            var controller = CombatPresentationAssets.Load<RuntimeAnimatorController>(name);
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.animationClips.Length, Is.EqualTo(4));
            foreach (AnimationClip clip in controller.animationClips)
            {
                Assert.That(AnimationUtility.GetAnimationEvents(clip), Is.Empty);
            }
            foreach (string dependency in AssetDatabase.GetDependencies(root + name + ".controller", true))
            {
                Assert.That(dependency.StartsWith("Assets/ThirdParty/"), Is.False, dependency);
            }
            Assert.That(CombatPresentationAssets.FireClip(name == "Launcher").samples, Is.GreaterThan(0));
            Assert.That(CombatPresentationAssets.ReloadClip(name == "Launcher").samples, Is.GreaterThan(0));
            Assert.That(CombatPresentationAssets.Load<Material>("Weapon").shader.name, Is.EqualTo("Standard"));
            var instance = Object.Instantiate(model);
            try
            {
                var animator = instance.GetComponent<Animator>();
                Assert.That(animator.avatar != null && animator.avatar.isValid, Is.True);
                animator.runtimeAnimatorController = controller;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Rebind();
                animator.Play("Idle", 0, 0.1f);
                animator.Update(0.01f);
                Transform[] bones = instance.GetComponentsInChildren<Transform>();
                Quaternion[] idle = bones.Select(bone => bone.localRotation).ToArray();
                animator.Play("Reload", 0, 0.4f);
                animator.Update(0.01f);
                Assert.That(bones.Where((bone, index) => Quaternion.Angle(idle[index], bone.localRotation) > 0.1f)
                    .Any(), Is.True, "换弹必须实际改变骨骼，而非仅切换状态名称。");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void RecoilCurve_RecoversAndDisableCancelsReloadWithoutChangingState()
        {
            var root = new GameObject("Curve test");
            try
            {
                var view = root.AddComponent<FirstPersonWeaponView>();
                InvokeLifecycle(view, "Awake");
                var state = new PlayerMovementState { PositionFixed = FixVec3.Forward, YawFixedDegrees = Fix.FromInt(45) };
                var original = state;
                view.PlayPredictedShot();
                view.Render(state, 0.025f);
                Assert.That(view.RecoilWeight, Is.GreaterThan(0.8f));
                Assert.That(root.transform.localRotation, Is.Not.EqualTo(Quaternion.identity));
                view.Render(state, 0.25f);
                Assert.That(view.RecoilWeight, Is.Zero);
                Assert.That(root.transform.localRotation, Is.EqualTo(Quaternion.identity));
                view.SetReloading(true);
                root.SetActive(false);
                InvokeLifecycle(view, "OnDisable");
                Assert.That(view.Reloading, Is.False);
                Assert.That(view.FlashRemaining, Is.Zero);
                Assert.That(state.RawEquals(original), Is.True);
            }
            finally
            {
                InvokeLifecycle(root.GetComponent<FirstPersonWeaponView>(), "OnDestroy");
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DefaultAssembly_SwitchStopsOldPoseAndRestoresCameraMask()
        {
            var cameraObject = new GameObject("Assembly camera");
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                int mask = camera.cullingMask;
                var root = new GameObject("Weapon root");
                root.transform.SetParent(camera.transform, false);
                var view = root.AddComponent<FirstPersonWeaponView>();
                view.Configure(camera);
                view.SetWeapon(WeaponSimulationConfig.DefaultRifle);
                Vector3 rifleScale = root.GetComponentInChildren<Animator>().transform.localScale;
                view.SetReloading(true);
                view.SetWeapon(WeaponCatalog.SecondaryFor(WeaponSimulationConfig.DefaultRifle));
                Assert.That(view.Reloading, Is.False);
                Assert.That(view.WeaponId, Is.EqualTo(2));
                Assert.That(cameraObject.GetComponentsInChildren<Camera>().Length, Is.EqualTo(2));
                Assert.That(camera.cullingMask & (1 << 31), Is.Zero);
                Assert.That(root.GetComponentsInChildren<Renderer>(true).All(value => value.gameObject.layer == 31), Is.True);
                camera.transform.rotation = Quaternion.Euler(57f, 123f, 0f);
                view.SetWeapon(WeaponSimulationConfig.DefaultRifle);
                Assert.That(Vector3.Distance(root.GetComponentInChildren<Animator>().transform.localScale, rifleScale),
                    Is.LessThan(0.0001f), "观察朝向不能改变模型归一化尺寸。");
                // EditMode 不自动执行普通 MonoBehaviour 的生命周期，显式驱动销毁回调。
                InvokeLifecycle(view, "OnDestroy");
                Object.DestroyImmediate(root);
                Assert.That(camera.cullingMask, Is.EqualTo(mask));
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void AuthorityEffects_DoNotReplayLocalShotAndDeduplicateSplash()
        {
            var root = new GameObject("Effects test");
            try
            {
                var view = root.AddComponent<ShotPresentationController>();
                var shot = new AuthoritativeShot(1, 1, 100, 2, WeaponFireMode.Projectile,
                    FixVec3.Zero, FixVec3.Forward, Fix.FromInt(100), Fix.FromInt(120), Fix.FromInt(30));
                var launch = new AuthoritativeShotResult(shot, 4, false, 0, Fix.Zero, FixVec3.Forward);
                view.Consume(launch, 1);
                Assert.That(view.RemoteMuzzleCount, Is.Zero);
                view.Clear();
                view.Consume(launch, 2);
                view.Consume(launch, 2);
                Assert.That(view.RemoteMuzzleCount, Is.EqualTo(1));
                view.Consume(new AuthoritativeShotResult(shot, 4, true, 2, Fix.Zero, FixVec3.Forward), 2);
                view.Consume(new AuthoritativeShotResult(shot, 4, true, 3, Fix.One, FixVec3.Forward), 2);
                Assert.That(view.ExplosionEffectCount, Is.EqualTo(1));
                Assert.That(view.RemoteMuzzleCount, Is.EqualTo(1));
                view.Clear();
                Assert.That(view.ExplosionEffectCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TerminalMatch_CancelsFlightWithoutErasingConfirmedFeedback()
        {
            var root = new GameObject("Terminal view test");
            try
            {
                var view = root.AddComponent<ShotPresentationController>();
                var shot = new AuthoritativeShot(1, 1, 100, 2, WeaponFireMode.Projectile,
                    FixVec3.Zero, FixVec3.Forward, Fix.One, Fix.FromInt(120), Fix.FromInt(30));
                Assert.That(view.PredictProjectile(shot), Is.True);
                view.CancelInFlight();
                Assert.That(view.ActiveProjectileCount, Is.Zero);
                Assert.That(view.PredictProjectile(shot), Is.False);
                view.Clear();
                Assert.That(view.PredictProjectile(shot), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CombatFov_RecoversWithoutRotatingAim()
        {
            var root = new GameObject("Combat FOV test");
            try
            {
                var camera = root.AddComponent<Camera>();
                camera.fieldOfView = 75f;
                var rig = root.AddComponent<FirstPersonCameraRig>();
                rig.Configure(null, root.transform);
                rig.Render(40f, -10f);
                Quaternion aim = root.transform.rotation;
                rig.RenderCombat(1f, 0f, 1f);
                Assert.That(camera.fieldOfView, Is.EqualTo(76.5f));
                Assert.That(root.transform.rotation, Is.EqualTo(aim));
                rig.RenderCombat(0f, 0f, 1f);
                Assert.That(camera.fieldOfView, Is.EqualTo(75f));
                rig.RenderCombat(0f, 6f, 1f);
                Assert.That(Vector3.Angle(root.transform.forward, aim * Vector3.forward), Is.LessThan(0.001f));
                Assert.That(Quaternion.Angle(root.transform.rotation, aim), Is.EqualTo(2f).Within(0.001f));
                rig.RenderCombat(0f, 0f, 1f);
                Assert.That(Quaternion.Angle(root.transform.rotation, aim), Is.LessThan(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}

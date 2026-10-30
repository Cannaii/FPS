using AFPS.Presentation.Camera;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AFPS.Tests.EditMode
{
    public sealed class FirstPersonCameraRigTests
    {
        private GameObject root;
        private Camera camera;
        private FirstPersonCameraRig rig;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Camera response test");
            camera = root.AddComponent<Camera>();
            camera.fieldOfView = 75f;
            rig = root.AddComponent<FirstPersonCameraRig>();
            rig.Configure(null, root.transform);
            rig.Render(123f, -37f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
        }

        [TestCase(9, false)]
        [TestCase(18, false)]
        [TestCase(43, false)]
        [TestCase(9, true)]
        [TestCase(18, true)]
        [TestCase(43, true)]
        public void EqualElapsedTime_ProducesSameFovAndRollAcrossFramePartitions(int frames, bool wallRun)
        {
            Vector3 forward = camera.transform.forward;
            rig.SetWallRun(wallRun, Vector3.right);
            rig.RenderCombat(1f, 6f, 0.3f);
            float expectedFov = camera.fieldOfView;
            Quaternion expectedRotation = camera.transform.rotation;
            rig.ResetCombat();
            rig.SetWallRun(wallRun, Vector3.right);
            for (int i = 0; i < frames; i++)
            {
                rig.Render(123f, -37f);
                rig.RenderCombat(1f, 6f, 0.3f / frames);
            }
            Assert.That(camera.fieldOfView, Is.EqualTo(expectedFov).Within(0.0001f));
            Assert.That(Vector3.Distance(camera.transform.up, expectedRotation * Vector3.up), Is.LessThan(0.00001f));
            Assert.That(Vector3.Distance(camera.transform.forward, forward), Is.LessThan(0.00001f));
        }

        [Test]
        public void Response_IsGradualBoundedAndCanReverseWithoutOvershoot()
        {
            Quaternion aim = camera.transform.rotation;
            rig.RenderCombat(1f, 100f, 0.01f);
            Assert.That(camera.fieldOfView, Is.InRange(75.01f, 76.49f));
            Assert.That(Quaternion.Angle(aim, camera.transform.rotation), Is.InRange(0.01f, 1.99f));
            rig.RenderCombat(1f, 100f, 1f);
            Assert.That(camera.fieldOfView, Is.EqualTo(76.5f));
            rig.RenderCombat(0f, -100f, 0.01f);
            Assert.That(camera.fieldOfView, Is.InRange(75f, 76.5f));
            Assert.That(Quaternion.Angle(aim, camera.transform.rotation), Is.LessThanOrEqualTo(2.001f));
            rig.RenderCombat(0f, 0f, 1f);
            Assert.That(camera.fieldOfView, Is.EqualTo(75f));
            Assert.That(Vector3.Distance(camera.transform.up, aim * Vector3.up), Is.LessThan(0.00001f));
        }

        [Test]
        public void InvalidTimingDoesNotPoisonCameraAndInvalidInputsRecoverToNeutral()
        {
            rig.RenderCombat(1f, 6f, 0.03f);
            float fov = camera.fieldOfView;
            Quaternion rotation = camera.transform.rotation;
            foreach (float delta in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
            {
                rig.RenderCombat(1f, 6f, delta);
                Assert.That(camera.fieldOfView, Is.EqualTo(fov));
                Assert.That(camera.transform.rotation, Is.EqualTo(rotation));
            }
            rig.RenderCombat(float.NaN, float.NegativeInfinity, 1f);
            Assert.That(camera.fieldOfView, Is.EqualTo(75f));
            Assert.That(Vector3.Distance(camera.transform.forward,
                Quaternion.Euler(-37f, 123f, 0f) * Vector3.forward), Is.LessThan(0.00001f));
            Quaternion neutral = camera.transform.rotation;
            rig.RenderCombat(0.00001f, 0.00001f, 0f);
            Assert.That(camera.fieldOfView, Is.EqualTo(75f));
            Assert.That(camera.transform.rotation, Is.EqualTo(neutral));
        }

        [Test]
        public void ReconfigureAndDisable_ResetImmediatelyWithoutCapturingBoostedBaseFov()
        {
            Quaternion aim = camera.transform.rotation;
            for (int i = 0; i < 3; i++)
            {
                rig.RenderCombat(1f, 6f, 1f);
                rig.Configure(null, root.transform);
                Assert.That(camera.fieldOfView, Is.EqualTo(75f));
                Assert.That(Vector3.Distance(camera.transform.up, aim * Vector3.up), Is.LessThan(0.00001f));
            }
            rig.RenderCombat(1f, 6f, 1f);
            // EditMode不运行普通Behaviour消息，显式调用同一个生命周期入口，避免SendMessage引擎断言。
            typeof(FirstPersonCameraRig).GetMethod("OnDisable",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(rig, null);
            Assert.That(camera.fieldOfView, Is.EqualTo(75f));
            Assert.That(Vector3.Distance(camera.transform.forward, aim * Vector3.forward), Is.LessThan(0.00001f));
            rig.RenderCombat(0f, 0f, 0.01f);
            Assert.That(camera.fieldOfView, Is.EqualTo(75f));
        }

        [TestCase(-1f)]
        [TestCase(1f)]
        public void WallRun_LeansAwayFromEitherWallAndAddsFovWithoutChangingAim(float normalX)
        {
            rig.Render(0f, 0f);
            rig.SetWallRun(true, Vector3.right * normalX);
            // 以很大的反向横移输入确认专属墙跑反馈不被普通横移倾斜抵消。
            rig.RenderCombat(0f, -normalX * 100f, 1f);
            Assert.That(camera.fieldOfView, Is.EqualTo(78f));
            Assert.That(Mathf.DeltaAngle(0f, camera.transform.eulerAngles.z), Is.EqualTo(-normalX * 8f).Within(0.01f));
            Assert.That(Vector3.Distance(camera.transform.forward, Vector3.forward), Is.LessThan(0.00001f));
            rig.SetWallRun(false, Vector3.right * normalX);
            rig.RenderCombat(0f, 0f, 0.01f);
            Assert.That(camera.fieldOfView, Is.InRange(75.01f, 77.99f));
            rig.RenderCombat(0f, 0f, 1f);
            Assert.That(camera.fieldOfView, Is.EqualTo(75f));
            Assert.That(Vector3.Distance(camera.transform.up, Vector3.up), Is.LessThan(0.00001f));
        }

        [Test]
        public void WallRun_InvalidNormalAndLifecycleResetCannotRetainTraversalFeedback()
        {
            rig.SetWallRun(true, Vector3.right);
            rig.RenderCombat(1f, 0f, 1f);
            Assert.That(camera.fieldOfView, Is.EqualTo(79.5f));
            rig.ResetCombat();
            rig.RenderCombat(0f, 0f, 1f);
            Assert.That(camera.fieldOfView, Is.EqualTo(75f));
            foreach (Vector3 normal in new[] { Vector3.zero, Vector3.up, new Vector3(float.NaN, 0f, 1f) })
            {
                rig.SetWallRun(true, normal);
                rig.RenderCombat(0f, 0f, 1f);
                Assert.That(camera.fieldOfView, Is.EqualTo(75f));
            }
        }

        [Test]
        public void NestedCamera_DoesNotAccumulatePreviousRollIntoAim()
        {
            var yaw = new GameObject("Yaw pivot");
            try
            {
                var pitch = new GameObject("Pitch pivot").transform;
                pitch.SetParent(yaw.transform, false);
                root.transform.SetParent(pitch, false);
                root.transform.localRotation = Quaternion.identity;
                var serialized = new SerializedObject(rig);
                serialized.FindProperty("yawPivot").objectReferenceValue = yaw.transform;
                serialized.FindProperty("pitchPivot").objectReferenceValue = pitch;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                for (int i = 0; i < 500; i++)
                {
                    rig.Render(123f, -37f);
                    rig.SetWallRun(true, Vector3.right);
                    rig.RenderCombat(0f, 6f, 0.02f);
                }
                Quaternion aim = Quaternion.Euler(0f, 123f, 0f) * Quaternion.Euler(-37f, 0f, 0f);
                Assert.That(Vector3.Distance(camera.transform.forward, aim * Vector3.forward), Is.LessThan(0.0001f));
                float expectedRoll = Mathf.Abs(Vector3.Dot(Vector3.right, aim * Vector3.right)) * 8f;
                Assert.That(Quaternion.Angle(camera.transform.rotation, aim), Is.EqualTo(expectedRoll).Within(0.01f));
                rig.ResetCombat();
                Assert.That(Vector3.Distance(camera.transform.up, aim * Vector3.up), Is.LessThan(0.0001f));
            }
            finally
            {
                root.transform.SetParent(null, false);
                Object.DestroyImmediate(yaw);
            }
        }
    }
}

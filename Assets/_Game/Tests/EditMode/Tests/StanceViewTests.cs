using AFPS.NetCode.SnapshotInterpolation;
using AFPS.Presentation.Camera;
using AFPS.Presentation.Characters;
using NUnit.Framework;
using UnityEngine;

namespace AFPS.Tests.EditMode
{
    public sealed class StanceViewTests
    {
        [Test]
        public void RemoteCrouch_KeepsFeetAndShortensBody()
        {
            var root = new GameObject("Stance view test");
            try
            {
                var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.transform.SetParent(root.transform, false);
                body.transform.localPosition = Vector3.up;
                var view = root.AddComponent<RemotePlayerView>();
                // EditMode不自动执行普通MonoBehaviour的Awake，显式复现运行时初始化。
                typeof(RemotePlayerView).GetMethod("Awake",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(view, null);
                var state = new RemotePlayerRenderState(10d, Vector3.zero, Quaternion.identity,
                    Vector3.zero, RemotePlayerSamplingStatus.Held, true, true);
                view.ApplyRenderState(state, crouchedHeightRatio: 0.6f);
                var bounds = body.GetComponent<Renderer>().bounds;
                Assert.That(bounds.min.y, Is.EqualTo(0f).Within(0.00001f));
                Assert.That(bounds.max.y, Is.EqualTo(1.2f).Within(0.00001f));
                state = new RemotePlayerRenderState(11d, Vector3.zero, Quaternion.identity,
                    Vector3.zero, RemotePlayerSamplingStatus.Held, true, false);
                view.ApplyRenderState(state);
                Assert.That(body.GetComponent<Renderer>().bounds.max.y, Is.EqualTo(2f).Within(0.00001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CameraCrouch_LowersEyeWithoutChangingAimOrFeet()
        {
            var feet = new GameObject("Camera feet test");
            var camera = new GameObject("Stance camera test");
            try
            {
                feet.transform.position = new Vector3(2f, 3f, 4f);
                camera.AddComponent<UnityEngine.Camera>();
                var rig = camera.AddComponent<FirstPersonCameraRig>();
                rig.Configure(feet.transform, camera.transform);
                rig.SetStance(0.6f);
                rig.Render(90f, 0f);
                Assert.That(camera.transform.position.y, Is.EqualTo(3.96f).Within(0.00001f));
                Assert.That(Vector3.Distance(camera.transform.forward, Vector3.right), Is.LessThan(0.00001f));
                Assert.That(feet.transform.position, Is.EqualTo(new Vector3(2f, 3f, 4f)));
                rig.SetStance(1f);
                rig.Render(90f, 0f);
                Assert.That(camera.transform.position.y, Is.EqualTo(4.6f).Within(0.00001f));
            }
            finally
            {
                Object.DestroyImmediate(camera);
                Object.DestroyImmediate(feet);
            }
        }
    }
}

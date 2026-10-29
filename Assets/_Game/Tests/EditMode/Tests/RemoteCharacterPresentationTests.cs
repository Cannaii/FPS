using System;
using System.Linq;
using AFPS.Editor.Art;
using AFPS.NetCode.Messages;
using AFPS.NetCode.SnapshotInterpolation;
using AFPS.Presentation.Characters;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AFPS.Tests.EditMode
{
    public sealed class RemoteCharacterPresentationTests
    {
        private GameObject actor;
        private RemotePlayerView view;

        [SetUp]
        public void SetUp()
        {
            var prefab = Resources.Load<RemotePlayerView>("Characters/RemotePlayer");
            Assert.That(prefab, Is.Not.Null);
            actor = Object.Instantiate(prefab.gameObject);
            view = actor.GetComponent<RemotePlayerView>();
            // EditMode不自动执行普通MonoBehaviour.Awake，显式完成与运行时相同的绑定。
            typeof(RemotePlayerView).GetMethod("Awake",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(view, null);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(actor);
        }

        private static RemotePlayerRenderState State(
            double tick = 10d,
            float x = 0f,
            bool crouched = false,
            RemotePlayerSamplingStatus status = RemotePlayerSamplingStatus.Interpolated)
        {
            return new RemotePlayerRenderState(tick, new Vector3(x, 0f, 0f),
                Quaternion.Euler(-40f, 70f, 0f), Vector3.right * 5f,
                status, isGrounded: true, isCrouched: crouched);
        }

        private Transform Bone(string name)
        {
            return actor.GetComponentsInChildren<Transform>().Single(item => item.name == name);
        }

        [Test]
        public void OwnedPrefab_HasNoPhysicsOrThirdPartyDependenciesAndCannotOverwriteEdits()
        {
            Assert.That(view.HasCharacterVisual, Is.True);
            Assert.That(actor.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(actor.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            foreach (string dependency in AssetDatabase.GetDependencies(RemoteCharacterAssetBuilder.PrefabPath, true))
            {
                Assert.That(dependency.StartsWith("Assets/ThirdParty/", StringComparison.Ordinal), Is.False, dependency);
            }
            Assert.Throws<InvalidOperationException>(RemoteCharacterAssetBuilder.Create);
        }

        [Test]
        public void AimPitch_DoesNotTiltFeetOrChangeSnapshot()
        {
            var state = State(x: 3f);
            view.ApplyRenderState(state);
            Assert.That(actor.transform.position, Is.EqualTo(state.Position));
            Assert.That(Vector3.Angle(actor.transform.up, Vector3.up), Is.LessThan(0.001f));
            Assert.That(Vector3.Angle(Bone("UpperBody").forward, state.Rotation * Vector3.forward), Is.LessThan(0.01f));
            Assert.That(state.Position, Is.EqualTo(new Vector3(3f, 0f, 0f)));
            Assert.That(state.Rotation, Is.EqualTo(Quaternion.Euler(-40f, 70f, 0f)));
        }

        [Test]
        public void Crouch_BendsKneesWithoutScalingHelmetAndStandingRestoresHeight()
        {
            view.ApplyRenderState(State());
            float standingHeight = Bone("Helmet").position.y;
            Vector3 helmetScale = Bone("Helmet").lossyScale;
            view.ApplyRenderState(State(11d, crouched: true));
            Assert.That(Bone("Helmet").position.y, Is.LessThan(standingHeight - 0.5f));
            Assert.That(Bone("Helmet").lossyScale, Is.EqualTo(helmetScale));
            Assert.That(Quaternion.Angle(Bone("LeftKnee").localRotation, Quaternion.identity), Is.GreaterThan(90f));
            view.ApplyRenderState(State(12d));
            Assert.That(Bone("Helmet").position.y, Is.EqualTo(standingHeight).Within(0.0001f));
        }

        [Test]
        public void Walking_IsIdempotentWithinOneTickAndTeleportResetsStride()
        {
            view.ApplyRenderState(State());
            var next = State(11d, 0.2f);
            view.ApplyRenderState(next);
            Quaternion walking = Bone("LeftThigh").localRotation;
            Assert.That(Quaternion.Angle(walking, Bone("RightThigh").localRotation), Is.GreaterThan(10f));
            view.ApplyRenderState(next);
            Assert.That(Bone("LeftThigh").localRotation, Is.EqualTo(walking));
            view.ApplyRenderState(State(12d, 30f, status: RemotePlayerSamplingStatus.Teleported));
            Assert.That(Bone("LeftThigh").localRotation, Is.EqualTo(Bone("RightThigh").localRotation));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void InactiveDeadOrStaleStream_StopsStrideAndRespawnRestoresVisual(int reason)
        {
            view.ApplyRenderState(State());
            view.ApplyRenderState(State(11d, 0.2f));
            view.ApplyRenderState(State(12d, 0.3f, status: reason == 2
                ? RemotePlayerSamplingStatus.ExtrapolationLimitReached : RemotePlayerSamplingStatus.Interpolated),
                isAlive: reason != 0, gameplayActive: reason != 1);
            Assert.That(Bone("LeftThigh").localRotation, Is.EqualTo(Bone("RightThigh").localRotation));
            Assert.That(actor.transform.position, Is.EqualTo(new Vector3(0.3f, 0f, 0f)));
            if (reason == 0)
            {
                Assert.That(
                    Quaternion.Angle(Bone("CharacterVisual").localRotation, Quaternion.identity),
                    Is.EqualTo(90f).Within(0.01f));
            }
            view.ApplyRenderState(State(13d, 0.3f));
            Assert.That(Bone("CharacterVisual").localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(actor.transform.localScale, Is.EqualTo(Vector3.one));
        }

        [Test]
        public void Registry_DefaultPathCreatesCharacterAndClearRemovesIt()
        {
            var root = new GameObject("Remote character registry test");
            try
            {
                var registry = root.AddComponent<RemotePlayerViewRegistry>();
                var replicas = new RemotePlayerMovementReplicas(8, 0.02f, 0d, 2d, 5f);
                var snapshot = new RemotePlayerMovementSnapshot(7, 10, Vector3.zero, Quaternion.identity, Vector3.zero);
                var packet = new byte[RemotePlayerMovementSnapshotCodec.PacketSize];
                Assert.That(RemotePlayerMovementSnapshotCodec.TrySerialize(snapshot, 1,
                    new ArraySegment<byte>(packet), out int written), Is.True);
                Assert.That(replicas.TryInsertSnapshot(new ArraySegment<byte>(packet, 0, written), out _), Is.True);
                registry.Render(replicas, 10d);
                var remote = root.GetComponentInChildren<RemotePlayerView>();
                Assert.That(remote, Is.Not.Null);
                Assert.That(remote.GetComponentInChildren<RemoteCharacterPoseView>().IsConfigured, Is.True);
                registry.Clear();
                Assert.That(root.GetComponentsInChildren<RemotePlayerView>(), Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}

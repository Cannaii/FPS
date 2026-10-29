using AFPS.Math;
using AFPS.NetCode.SnapshotInterpolation;
using AFPS.Presentation.Characters;
using AFPS.Presentation.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace AFPS.Tests.EditMode
{
    public sealed class FootstepPresentationTests
    {
        [Test]
        public void GroundedTimeline_ChangesOnlyAtSnapshotBoundaryIncludingWrap()
        {
            var timeline = new RemotePlayerMovementTimeline(1, 8, 0.02f, 0d, 2d, 5f);
            timeline.Insert(new RemotePlayerMovementSnapshot(1, uint.MaxValue, FixVec3.Zero,
                Fix.Zero, Fix.Zero, FixVec3.Forward, isGrounded: false));
            timeline.Insert(new RemotePlayerMovementSnapshot(1, 1, FixVec3.Forward,
                Fix.Zero, Fix.Zero, FixVec3.Forward, isGrounded: true));
            Assert.That(timeline.TrySample(4294967296d, out var before), Is.True);
            Assert.That(before.IsGrounded, Is.False);
            Assert.That(timeline.TrySample(4294967297d, out var landing), Is.True);
            Assert.That(landing.IsGrounded, Is.True);
            Assert.That(timeline.TrySample(4294967300d, out var frozen), Is.True);
            Assert.That(frozen.IsGrounded, Is.True);
            Assert.That(frozen.Status, Is.EqualTo(RemotePlayerSamplingStatus.ExtrapolationLimitReached));
        }

        [Test]
        public void Footsteps_CountTravelWithoutRepeatingOnHeldState()
        {
            var root = new GameObject("Footstep distance test");
            try
            {
                var view = root.AddComponent<FootstepAudioView>();
                Assert.That(CombatPresentationAssets.Load<AudioClip>("Footstep1"), Is.Not.Null);
                Assert.That(CombatPresentationAssets.Load<AudioClip>("Footstep2"), Is.Not.Null);
                Assert.That(CombatPresentationAssets.Load<Material>("Effects").shader.name, Is.EqualTo("Sprites/Default"));
                view.Render(Vector3.zero, true, true);
                view.Render(Vector3.right, true, true);
                Assert.That(view.StepCount, Is.Zero);
                view.Render(Vector3.right * 2f, true, true);
                Assert.That(view.StepCount, Is.EqualTo(1));
                for (int i = 0; i < 100; i++)
                {
                    view.Render(Vector3.right * 2f, true, true);
                }
                Assert.That(view.StepCount, Is.EqualTo(1));
                view.Render(Vector3.right * 6f, true, true);
                Assert.That(view.StepCount, Is.EqualTo(2), "一个长帧最多补播一次。");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AirDeathTeleportAndNewSession_ResetStride()
        {
            var root = new GameObject("Footstep reset test");
            try
            {
                var view = root.AddComponent<FootstepAudioView>();
                view.Render(Vector3.zero, true, true);
                view.Render(Vector3.right, true, true);
                view.Render(Vector3.right * 2f, false, true);
                view.Render(Vector3.right * 4f, false, true);
                view.Render(Vector3.right * 5f, true, true);
                view.Render(Vector3.right * 6f, true, true);
                Assert.That(view.StepCount, Is.Zero);
                view.Render(Vector3.right * 7f, true, true);
                Assert.That(view.StepCount, Is.EqualTo(1));
                view.Render(Vector3.right * 8f, true, false);
                view.Render(Vector3.right * 9f, true, true);
                view.Render(Vector3.right * 20f, true, true);
                view.Render(Vector3.right * 21f, true, true);
                Assert.That(view.StepCount, Is.EqualTo(1));
                view.Render(Vector3.right * 22f, true, true, true);
                Assert.That(view.StepCount, Is.EqualTo(1));
                view.Clear();
                view.Render(Vector3.right * 100f, true, true);
                Assert.That(view.StepCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RemoteView_UsesSnapshotGroundingAndStopsAtTerminalMatch()
        {
            var root = new GameObject("Remote footstep integration");
            try
            {
                var view = root.AddComponent<RemotePlayerView>();
                typeof(RemotePlayerView).GetMethod("Awake",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(view, null);
                view.ApplyRenderState(State(0f, true));
                view.ApplyRenderState(State(2f, false));
                Assert.That(view.FootstepCount, Is.Zero);
                view.ApplyRenderState(State(3f, true));
                view.ApplyRenderState(State(5f, true));
                Assert.That(view.FootstepCount, Is.EqualTo(1));
                view.ApplyRenderState(State(7f, true), true, false);
                view.ApplyRenderState(State(9f, true), false);
                Assert.That(view.FootstepCount, Is.EqualTo(1));
                Assert.That(root.GetComponentsInChildren<Collider>(), Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static RemotePlayerRenderState State(float x, bool grounded) =>
            new RemotePlayerRenderState(100d, Vector3.right * x, Quaternion.identity,
                Vector3.right * 6f, RemotePlayerSamplingStatus.Interpolated, grounded);
    }
}

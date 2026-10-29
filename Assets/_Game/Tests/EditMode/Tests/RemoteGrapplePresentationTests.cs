using AFPS.Math;
using AFPS.NetCode.SnapshotInterpolation;
using AFPS.Presentation.Characters;
using NUnit.Framework;
using UnityEngine;

namespace AFPS.Tests.EditMode
{
    public sealed class RemoteGrapplePresentationTests
    {
        [Test]
        public void ClearingReplicaViews_RemovesVisibleRopeAndOwnedObject()
        {
            var root = new GameObject("Remote rope clear test");
            try
            {
                var world = root.AddComponent<RemotePlayerViewRegistry>();
                var target = new GameObject("Remote actor");
                target.transform.SetParent(root.transform);
                var view = target.AddComponent<RemotePlayerView>();
                typeof(RemotePlayerView).GetMethod("Awake", System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.NonPublic).Invoke(view, null);
                view.ApplyRenderState(new RemotePlayerRenderState(10d, Vector3.zero, Quaternion.identity,
                    Vector3.zero, RemotePlayerSamplingStatus.Held, isGrappling: true, grapplePoint: Vector3.up * 10f));
                var views = (System.Collections.Generic.Dictionary<uint, RemotePlayerView>)typeof(RemotePlayerViewRegistry)
                    .GetField("views", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(world);
                views.Add(1, view);
                Assert.That(world.VisibleGrappleRopeCount, Is.EqualTo(1));
                world.Clear();
                Assert.That(world.VisibleGrappleRopeCount, Is.Zero);
                Assert.That(target == null, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static RemotePlayerMovementSnapshot Snapshot(uint tick, bool active, int anchorX = 0, bool teleport = false) =>
            new RemotePlayerMovementSnapshot(1, tick, FixVec3.Right * Fix.FromInt((int)(tick - 10)), Fix.Zero, Fix.Zero,
                FixVec3.Zero, isTeleport: teleport, isGrappling: active,
                grapplePoint: active ? new FixVec3(Fix.FromInt(anchorX), Fix.FromInt(5), Fix.FromInt(10)) : FixVec3.Zero);

        [Test]
        public void Timeline_HoldsAnchorUntilSnapshotTickThenDetachesOnTeleport()
        {
            var timeline = new RemotePlayerMovementTimeline(1, 8, 0.02f, 0d, 2d, 20f);
            timeline.Insert(Snapshot(10, true));
            timeline.Insert(Snapshot(12, true, 4));
            Assert.That(timeline.TrySample(11d, out var state), Is.True);
            Assert.That(state.Position.x, Is.EqualTo(1f));
            Assert.That(state.GrapplePoint.x, Is.EqualTo(0f));
            Assert.That(state.IsGrappling, Is.True);
            Assert.That(timeline.TrySample(12d, out state), Is.True);
            Assert.That(state.GrapplePoint.x, Is.EqualTo(4f));
            timeline.Insert(Snapshot(13, false, teleport: true));
            Assert.That(timeline.TrySample(12.5d, out state), Is.True);
            Assert.That(state.IsGrappling, Is.True);
            Assert.That(state.GrapplePoint.x, Is.EqualTo(4f));
            Assert.That(timeline.TrySample(13d, out state), Is.True);
            Assert.That(state.IsGrappling, Is.False);
            Assert.That(state.GrapplePoint, Is.EqualTo(Vector3.zero));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void RemoteView_HidesRopeOnDetachDeathInactiveMatchAndStaleStream(int reason)
        {
            var target = new GameObject("Remote grapple test");
            try
            {
                var view = target.AddComponent<RemotePlayerView>();
                typeof(RemotePlayerView).GetMethod("Awake", System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.NonPublic).Invoke(view, null);
                var active = new RemotePlayerRenderState(10d, new Vector3(3f, 0f, 0f), Quaternion.identity,
                    Vector3.zero, RemotePlayerSamplingStatus.Held, isGrappling: true, grapplePoint: new Vector3(4f, 5f, 10f));
                view.ApplyRenderState(active, grappleOriginHeight: 1.8f);
                Assert.That(view.HasGrappleRope, Is.True);
                var line = target.GetComponentInChildren<LineRenderer>();
                Assert.That(line.GetPosition(0).x, Is.EqualTo(3.15f).Within(0.00001f));
                Assert.That(line.GetPosition(0).y, Is.EqualTo(1.68f).Within(0.00001f));
                Assert.That(line.GetPosition(1), Is.EqualTo(active.GrapplePoint));
                var hidden = new RemotePlayerRenderState(11d, active.Position, active.Rotation, Vector3.zero,
                    reason == 3 ? RemotePlayerSamplingStatus.ExtrapolationLimitReached : RemotePlayerSamplingStatus.Held,
                    isGrappling: reason != 0, grapplePoint: active.GrapplePoint);
                view.ApplyRenderState(hidden, isAlive: reason != 1, gameplayActive: reason != 2);
                Assert.That(view.HasGrappleRope, Is.False);
                view.ApplyRenderState(active);
                Assert.That(view.HasGrappleRope, Is.True);
                view.HideGrappleRope();
                Assert.That(view.HasGrappleRope, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }
    }
}

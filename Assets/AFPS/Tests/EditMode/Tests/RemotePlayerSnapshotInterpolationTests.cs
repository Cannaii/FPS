using AFPS.NetCode.SnapshotInterpolation;
using NUnit.Framework;
using UnityEngine;

namespace AFPS.Tests.EditMode
{
    public sealed class RemotePlayerSnapshotInterpolationTests
    {
        [Test]
        public void Buffer_OrdersOutOfOrderSnapshotsAndReplacesDuplicateTick()
        {
            RemotePlayerSnapshotBuffer buffer = new RemotePlayerSnapshotBuffer(7, 4);

            Assert.That(buffer.Insert(Snapshot(7, 12, 12f)), Is.EqualTo(SnapshotBufferInsertResult.Added));
            Assert.That(buffer.Insert(Snapshot(7, 10, 10f)), Is.EqualTo(SnapshotBufferInsertResult.Added));
            Assert.That(buffer.Insert(Snapshot(7, 11, 11f)), Is.EqualTo(SnapshotBufferInsertResult.Added));
            Assert.That(buffer.Insert(Snapshot(7, 11, 111f)), Is.EqualTo(SnapshotBufferInsertResult.Replaced));

            Assert.That(buffer.GetAt(0).ServerTick, Is.EqualTo(10));
            Assert.That(buffer.GetAt(1).Position.x, Is.EqualTo(111f));
            Assert.That(buffer.GetAt(2).ServerTick, Is.EqualTo(12));
            Assert.That(buffer.Insert(Snapshot(8, 13, 13f)), Is.EqualTo(SnapshotBufferInsertResult.EntityMismatch));
        }

        [Test]
        public void Buffer_PreservesOrderingAcrossUIntTickWrap()
        {
            RemotePlayerSnapshotBuffer buffer = new RemotePlayerSnapshotBuffer(1, 4);
            buffer.Insert(Snapshot(1, uint.MaxValue, 1f));
            buffer.Insert(Snapshot(1, 1, 3f));
            buffer.Insert(Snapshot(1, 0, 2f));

            Assert.That(buffer.GetAt(0).ServerTick, Is.EqualTo(uint.MaxValue));
            Assert.That(buffer.GetAt(1).ServerTick, Is.Zero);
            Assert.That(buffer.GetAt(2).ServerTick, Is.EqualTo(1));
        }

        [Test]
        public void Buffer_WhenFullEvictsOldestAndRejectsOlderArrival()
        {
            RemotePlayerSnapshotBuffer buffer = new RemotePlayerSnapshotBuffer(1, 3);
            buffer.Insert(Snapshot(1, 10, 10f));
            buffer.Insert(Snapshot(1, 11, 11f));
            buffer.Insert(Snapshot(1, 12, 12f));

            Assert.That(buffer.Insert(Snapshot(1, 13, 13f)), Is.EqualTo(SnapshotBufferInsertResult.Added));
            Assert.That(buffer.GetAt(0).ServerTick, Is.EqualTo(11));
            Assert.That(buffer.Insert(Snapshot(1, 9, 9f)), Is.EqualTo(SnapshotBufferInsertResult.TooOld));
            Assert.That(buffer.Count, Is.EqualTo(3));
        }

        [Test]
        public void Timeline_UsesInterpolationDelayForPositionAndRotation()
        {
            RemotePlayerSnapshotTimeline timeline = new RemotePlayerSnapshotTimeline(1, 8, 0.02f, 2.0, 2.0, 20f);
            timeline.Insert(new RemotePlayerSnapshot(1, 10, Vector3.zero, Quaternion.identity, Vector3.right));
            timeline.Insert(new RemotePlayerSnapshot(1, 12, new Vector3(4f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f), Vector3.right));

            Assert.That(timeline.TrySample(13.0, out RemotePlayerRenderState state), Is.True);
            Assert.That(state.Status, Is.EqualTo(RemotePlayerSamplingStatus.Interpolated));
            Assert.That(state.Position.x, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(Quaternion.Angle(state.Rotation, Quaternion.Euler(0f, 45f, 0f)), Is.LessThan(0.01f));
        }

        [Test]
        public void Timeline_BoundsShortExtrapolationThenFreezes()
        {
            RemotePlayerSnapshotTimeline timeline = new RemotePlayerSnapshotTimeline(1, 8, 0.02f, 0.0, 2.0, 20f);
            timeline.Insert(new RemotePlayerSnapshot(1, 10, Vector3.zero, Quaternion.identity, new Vector3(10f, 0f, 0f)));

            timeline.TrySample(11.0, out RemotePlayerRenderState shortState);
            timeline.TrySample(20.0, out RemotePlayerRenderState frozenState);

            Assert.That(shortState.Status, Is.EqualTo(RemotePlayerSamplingStatus.Extrapolated));
            Assert.That(shortState.Position.x, Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(frozenState.Status, Is.EqualTo(RemotePlayerSamplingStatus.ExtrapolationLimitReached));
            Assert.That(frozenState.Position.x, Is.EqualTo(0.4f).Within(0.0001f));
        }

        [Test]
        public void Timeline_HoldsBeforeTeleportAndSnapsAtTeleportTick()
        {
            RemotePlayerSnapshotTimeline timeline = new RemotePlayerSnapshotTimeline(1, 8, 0.02f, 0.0, 2.0, 5f);
            timeline.Insert(Snapshot(1, 10, 0f));
            timeline.Insert(new RemotePlayerSnapshot(1, 12, new Vector3(100f, 0f, 0f), Quaternion.identity, Vector3.zero, true));

            timeline.TrySample(11.0, out RemotePlayerRenderState heldState);
            timeline.TrySample(12.0, out RemotePlayerRenderState teleportedState);

            Assert.That(heldState.Status, Is.EqualTo(RemotePlayerSamplingStatus.HeldBeforeTeleport));
            Assert.That(heldState.Position.x, Is.Zero);
            Assert.That(teleportedState.Status, Is.EqualTo(RemotePlayerSamplingStatus.Teleported));
            Assert.That(teleportedState.Position.x, Is.EqualTo(100f));
        }

        [Test]
        public void Timeline_InterpolatesAcrossMissingIntermediateSnapshot()
        {
            RemotePlayerSnapshotTimeline timeline = new RemotePlayerSnapshotTimeline(1, 8, 0.02f, 0.0, 2.0, 20f);
            timeline.Insert(Snapshot(1, 20, 0f));
            timeline.Insert(Snapshot(1, 24, 8f));

            timeline.TrySample(22.0, out RemotePlayerRenderState state);

            Assert.That(state.Status, Is.EqualTo(RemotePlayerSamplingStatus.Interpolated));
            Assert.That(state.Position.x, Is.EqualTo(4f).Within(0.0001f));
        }

        private static RemotePlayerSnapshot Snapshot(uint entityId, uint tick, float positionX)
        {
            return new RemotePlayerSnapshot(entityId, tick, new Vector3(positionX, 0f, 0f), Quaternion.identity, Vector3.zero);
        }
    }
}

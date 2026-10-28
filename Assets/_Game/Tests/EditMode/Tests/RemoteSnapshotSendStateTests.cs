using System;
using AFPS.Math;
using AFPS.NetCode.SnapshotInterpolation;
using AFPS.NetCode.StateReplication;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class RemoteSnapshotSendStateTests
    {
        [TestCase(50, 20, 20)]
        [TestCase(60, 20, 20)]
        [TestCase(50, 15, 15)]
        [TestCase(30, 20, 20)]
        [TestCase(50, 100, 50)]
        public void RationalCadence_HitsTargetWithoutIntegerRoundingLoss(int tickRate, int rate, int expected)
        {
            var state = new RemoteSnapshotSendState(tickRate, rate, Fix.FromInt(5));
            int count = 0;
            for (uint tick = 0; tick < tickRate; tick++)
            {
                if (state.IsDue(tick, FixVec3.Zero))
                {
                    state.RecordSuccessfulSend(Snapshot(tick), !state.HasSent);
                    count++;
                }
            }
            Assert.That(count, Is.EqualTo(expected));
        }

        [Test]
        public void FailureDoesNotAdvanceScheduleOrBaseline_AndLateSendDoesNotBurst()
        {
            var state = new RemoteSnapshotSendState(50, 20, Fix.FromInt(5));
            Assert.That(state.IsDue(100, FixVec3.Zero), Is.True);
            Assert.That(state.IsDue(101, FixVec3.Zero), Is.True);
            Assert.That(state.HasSent, Is.False);
            state.RecordSuccessfulSend(Snapshot(101), true);
            Assert.That(state.IsDue(102, FixVec3.Zero), Is.False);
            Assert.That(state.IsDue(103, FixVec3.Zero), Is.True);
            Assert.That(state.IsDue(104, FixVec3.Zero), Is.True);
            Assert.That(state.FullBaseline.ServerTick, Is.EqualTo(101));
            state.RecordSuccessfulSend(Snapshot(110), false);
            Assert.That(state.IsDue(111, FixVec3.Zero), Is.False);
            Assert.That(state.FullBaseline.ServerTick, Is.EqualTo(101));
            state.RecordSuccessfulSend(Snapshot(113), true);
            Assert.That(state.FullBaseline.ServerTick, Is.EqualTo(113));
        }

        [Test]
        public void LargeDisplacement_BypassesCadenceAtInclusiveBoundaryAndRebasesSchedule()
        {
            var state = new RemoteSnapshotSendState(50, 20, Fix.FromInt(5));
            state.RecordSuccessfulSend(Snapshot(10), true);
            var boundary = new FixVec3(Fix.FromInt(3), Fix.FromInt(4), Fix.Zero);
            var inside = new FixVec3(Fix.FromInt(3), Fix.FromInt(4) - Fix.Epsilon, Fix.Zero);
            Assert.That(state.IsDue(11, inside), Is.False);
            Assert.That(state.IsDue(11, boundary), Is.True);
            state.RecordSuccessfulSend(Snapshot(11, boundary), true);
            Assert.That(state.IsDue(12, boundary), Is.False);
            Assert.That(state.IsUrgent(boundary), Is.False);
        }

        [Test]
        public void WideCoordinates_DoNotOverflowDistance_AndEachReceiverOwnsItsHistory()
        {
            var first = new RemoteSnapshotSendState(50, 20, Fix.MaxValue);
            var second = new RemoteSnapshotSendState(50, 20, Fix.MaxValue);
            var low = new FixVec3(Fix.MinValue, Fix.MinValue, Fix.MinValue);
            var high = new FixVec3(Fix.MaxValue, Fix.MaxValue, Fix.MaxValue);
            first.RecordSuccessfulSend(Snapshot(1, low), true);
            second.RecordSuccessfulSend(Snapshot(1, high), true);
            Assert.That(first.IsUrgent(high), Is.True);
            Assert.That(second.IsUrgent(high), Is.False);
            Assert.That(second.IsUrgent(FixVec3.Zero), Is.True);
        }

        [Test]
        public void ClockWrapAndDuplicateCalls_PreserveOrder()
        {
            var state = new RemoteSnapshotSendState(50, 20, Fix.FromInt(5));
            state.RecordSuccessfulSend(Snapshot(uint.MaxValue - 1), true);
            Assert.That(state.IsDue(uint.MaxValue, FixVec3.Zero), Is.False);
            Assert.That(state.IsDue(0, FixVec3.Zero), Is.True);
            state.RecordSuccessfulSend(Snapshot(0), false);
            Assert.That(state.IsDue(0, FixVec3.Up * Fix.FromInt(10)), Is.False);
            Assert.That(state.IsDue(uint.MaxValue, FixVec3.Zero), Is.False);
            Assert.Throws<InvalidOperationException>(() => state.RecordSuccessfulSend(Snapshot(0), true));
        }

        [TestCase(0, 20, 5)]
        [TestCase(50, 0, 5)]
        [TestCase(50, 20, 0)]
        [TestCase(50, 20, -1)]
        public void InvalidPolicy_IsRejected(int tickRate, int rate, int distance)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new RemoteSnapshotSendState(tickRate, rate, Fix.FromInt(distance)));
        }

        private static RemotePlayerSnapshot Snapshot(uint tick, FixVec3 position = default) =>
            new RemotePlayerSnapshot(7, tick, position, Fix.Zero, Fix.Zero, FixVec3.Zero);
    }
}

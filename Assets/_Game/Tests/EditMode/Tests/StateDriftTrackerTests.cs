using System;
using System.Collections.Generic;
using AFPS.Math;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Metrics;
using AFPS.Simulation.Characters;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class StateDriftTrackerTests
    {
        [Test]
        public void ReportBeforeInput_WaitsForExactAuthoritativeSample()
        {
            var tracker = new ServerStateDriftTracker();
            var compared = Observe(tracker);
            Assert.That(tracker.TryReceive(Report(1), 1), Is.True);
            Assert.That(tracker.PendingCount, Is.EqualTo(1));
            Assert.That(compared, Is.Empty);
            tracker.Record(State(1), Input(1));
            Assert.That(tracker.PendingCount, Is.Zero);
            Assert.That(compared.Count, Is.EqualTo(1));
            Assert.That(compared[0].Status, Is.EqualTo(StateHashComparisonStatus.Matched));
        }

        [Test]
        public void SameInputOneRawStateDifference_LocatesEntityAndInputTick()
        {
            var tracker = new ServerStateDriftTracker();
            var compared = Observe(tracker);
            tracker.Record(State(1), Input(1));
            var predicted = State(1);
            predicted.PositionFixed += FixVec3.Right * Fix.Epsilon;
            Assert.That(tracker.TryReceive(new ClientStateHashReport(7, 1,
                PlayerStateHash.Compute(predicted), MovementInputHash.Compute(Input(1))), 1), Is.True);
            Assert.That(compared[0].Client.EntityId, Is.EqualTo(7));
            Assert.That(compared[0].Client.InputTick, Is.EqualTo(1));
            Assert.That(compared[0].Status, Is.EqualTo(StateHashComparisonStatus.StateMismatch));
            Assert.That(compared[0].ServerStateHash, Is.EqualTo(PlayerStateHash.Compute(State(1))));
        }

        [Test]
        public void DifferentAppliedInput_IsNotClassifiedAsStateDrift()
        {
            var tracker = new ServerStateDriftTracker();
            var compared = Observe(tracker);
            var requested = Input(1);
            requested.JumpPressed = true;
            tracker.Record(State(1), Input(1));
            Assert.That(tracker.TryReceive(new ClientStateHashReport(7, 1,
                123, MovementInputHash.Compute(requested)), 1), Is.True);
            Assert.That(compared[0].Status, Is.EqualTo(StateHashComparisonStatus.DifferentAppliedInput));
        }

        [Test]
        public void DuplicateSequenceOrTick_IsRejectedWithoutConsumingNewSequence()
        {
            var tracker = new ServerStateDriftTracker();
            tracker.Record(State(1), Input(1));
            Assert.That(tracker.TryReceive(Report(1), 1), Is.True);
            Assert.That(tracker.TryReceive(Report(2), 1), Is.False);
            Assert.That(tracker.TryReceive(Report(1), 2), Is.False);
            Assert.That(tracker.TryReceive(Report(2), 2), Is.True);
            Assert.That(tracker.AcceptedCount, Is.EqualTo(2));
            Assert.That(tracker.RejectedCount, Is.EqualTo(2));
        }

        [Test]
        public void PendingCapacity_IsBoundedAndReleasedOnSampleAndConnectionReset()
        {
            var tracker = new ServerStateDriftTracker();
            for (uint tick = 1; tick <= 8; tick++)
            {
                Assert.That(tracker.TryReceive(Report(tick), tick), Is.True);
            }
            Assert.That(tracker.TryReceive(Report(9), 9), Is.False);
            tracker.Record(State(1), Input(1));
            Assert.That(tracker.TryReceive(Report(9), 9), Is.True);
            Assert.That(tracker.PendingCount, Is.EqualTo(8));
            tracker.Reset(100);
            Assert.That(tracker.PendingCount, Is.Zero);
            Assert.That(tracker.DiscardedOnReset, Is.EqualTo(8));
            Assert.That(tracker.TryReceive(Report(101), 0), Is.True);
        }

        [Test]
        public void FutureWindow_RejectsBeyond64InputTicks()
        {
            var tracker = new ServerStateDriftTracker();
            Assert.That(tracker.TryReceive(Report(64), 1), Is.True);
            Assert.That(tracker.TryReceive(Report(65), 2), Is.False);
            Assert.That(tracker.PendingCount, Is.EqualTo(1));
        }

        [Test]
        public void OldHistoryBoundary_Rejects256AgeAndAccepts255Age()
        {
            var tracker = new ServerStateDriftTracker();
            var compared = Observe(tracker);
            for (uint tick = 1; tick <= 257; tick++)
            {
                tracker.Record(State(tick), Input(tick));
            }
            Assert.That(tracker.TryReceive(Report(1), 1), Is.False);
            Assert.That(tracker.TryReceive(Report(2), 1), Is.True);
            Assert.That(compared[0].Status, Is.EqualTo(StateHashComparisonStatus.Matched));
        }

        [Test]
        public void MissingSample_IsReportedAsUnavailableInsteadOfDrift()
        {
            var tracker = new ServerStateDriftTracker();
            var compared = Observe(tracker);
            tracker.TryReceive(Report(2), 1);
            tracker.Record(State(3), Input(3));
            Assert.That(compared.Count, Is.EqualTo(1));
            Assert.That(compared[0].Status, Is.EqualTo(StateHashComparisonStatus.HistoryUnavailable));
        }

        [Test]
        public void InputAndPacketSequenceWrap_AreComparedInOriginalOrder()
        {
            var tracker = new ServerStateDriftTracker();
            var compared = Observe(tracker);
            tracker.Reset(uint.MaxValue - 1);
            Assert.That(tracker.TryReceive(Report(uint.MaxValue), uint.MaxValue), Is.True);
            tracker.Record(State(uint.MaxValue), Input(uint.MaxValue));
            Assert.That(tracker.TryReceive(Report(0), 0), Is.True);
            tracker.Record(State(0), Input(0));
            Assert.That(compared.Count, Is.EqualTo(2));
            Assert.That(compared.TrueForAll(value => value.Status == StateHashComparisonStatus.Matched), Is.True);
        }

        [Test]
        public void InvalidSamples_CannotOverwritePreviouslyRecordedInputTick()
        {
            var tracker = new ServerStateDriftTracker();
            Assert.Throws<ArgumentException>(() => tracker.Record(State(1), Input(2)));
            tracker.Record(State(1), Input(1));
            Assert.Throws<ArgumentException>(() => tracker.Record(State(1), Input(1)));
            Assert.Throws<ArgumentException>(() => tracker.Record(State(0), Input(0)));
            Assert.That(tracker.TryReceive(new ClientStateHashReport(0, 2, 0, 0), 1), Is.False);
        }

        private static List<StateHashComparison> Observe(ServerStateDriftTracker tracker)
        {
            var list = new List<StateHashComparison>();
            tracker.Compared += list.Add;
            return list;
        }

        private static PlayerState State(uint tick) => new PlayerState { Tick = tick, IsGrounded = true };
        private static PlayerInputCommand Input(uint tick) => new PlayerInputCommand { Tick = tick };
        private static ClientStateHashReport Report(uint tick) => new ClientStateHashReport(7, tick,
            PlayerStateHash.Compute(State(tick)), MovementInputHash.Compute(Input(tick)));
    }
}

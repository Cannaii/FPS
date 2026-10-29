using System;
using AFPS.NetCode.Match;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class LobbyStartGateTests
    {
        private static uint Epoch(LobbyStartGate gate, uint entity)
        {
            var snapshot = gate.Snapshot();
            for (int index = 0; index < snapshot.PlayerCount; index++)
            {
                if (snapshot.GetPlayer(index).EntityId == entity)
                {
                    return snapshot.GetPlayer(index).MembershipEpoch;
                }
            }
            Assert.Fail("缺少测试成员。");
            return 0;
        }

        private static LobbyCommandResult Ready(LobbyStartGate gate, uint entity, uint sequence = 1, bool ready = true)
        {
            return gate.Apply(entity, new LobbyReadyCommand(gate.RoundEpoch, Epoch(gate, entity), sequence, ready));
        }

        private static LobbyStartGate TwoReady(uint duration = 150)
        {
            var gate = new LobbyStartGate(duration);
            Assert.That(gate.Connect(1), Is.True);
            Assert.That(gate.Connect(2), Is.True);
            Assert.That(Ready(gate, 1), Is.EqualTo(LobbyCommandResult.Applied));
            Assert.That(Ready(gate, 2), Is.EqualTo(LobbyCommandResult.Applied));
            return gate;
        }

        [TestCase(0U)]
        [TestCase(0x80000000U)]
        [TestCase(uint.MaxValue)]
        public void DurationMustFitUnambiguousTickWindow(uint duration)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new LobbyStartGate(duration));
        }

        [Test]
        public void RequiresTwoOnlinePlayersAndEveryPlayerReady()
        {
            var gate = new LobbyStartGate(150);
            gate.Connect(1);
            Ready(gate, 1);
            gate.Advance(100);
            Assert.That(gate.Phase, Is.EqualTo(LobbyPhase.Waiting));
            gate.Connect(2);
            gate.Advance(101);
            Assert.That(gate.Phase, Is.EqualTo(LobbyPhase.Waiting));
            Ready(gate, 2);
            gate.Advance(102);
            Assert.That(gate.Phase, Is.EqualTo(LobbyPhase.Countdown));
            Assert.That(gate.Snapshot().RemainingTicks, Is.EqualTo(150));
            gate.Advance(251);
            Assert.That(gate.Snapshot().RemainingTicks, Is.EqualTo(1));
            gate.Advance(252);
            Assert.That(gate.Phase, Is.EqualTo(LobbyPhase.Playing));
            Assert.That(gate.PlayingStartedTick, Is.EqualTo(252));
            gate.Advance(300);
            Assert.That(gate.PlayingStartedTick, Is.EqualTo(252));
            Assert.That(Ready(gate, 1, 2, false), Is.EqualTo(LobbyCommandResult.Rejected));
        }

        [Test]
        public void CancelAndReReadyStartFullCountdownWithoutDuplicateExtendingIt()
        {
            var gate = TwoReady();
            gate.Advance(100);
            gate.Advance(249);
            Assert.That(Ready(gate, 2, 2, false), Is.EqualTo(LobbyCommandResult.Applied));
            Assert.That(gate.Phase, Is.EqualTo(LobbyPhase.Waiting));
            gate.Advance(400);
            Ready(gate, 2, 3);
            gate.Advance(401);
            gate.Advance(500);
            Assert.That(Ready(gate, 2, 3), Is.EqualTo(LobbyCommandResult.Duplicate));
            Assert.That(Ready(gate, 2, 4), Is.EqualTo(LobbyCommandResult.Applied));
            Assert.That(gate.Snapshot().GetPlayer(1).ReadySequence, Is.EqualTo(4));
            gate.Advance(550);
            Assert.That(gate.Snapshot().RemainingTicks, Is.EqualTo(1));
            gate.Advance(551);
            Assert.That(gate.Phase, Is.EqualTo(LobbyPhase.Playing));
        }

        [Test]
        public void JoinAndDepartureInvalidateCountdownButOfflineRetainedMembersDoNotCount()
        {
            var gate = TwoReady();
            gate.Advance(10);
            gate.Connect(3);
            Assert.That(gate.Phase, Is.EqualTo(LobbyPhase.Waiting));
            gate.Advance(20);
            Assert.That(gate.Phase, Is.EqualTo(LobbyPhase.Waiting));
            Ready(gate, 3);
            gate.Advance(21);
            gate.Disconnect(3);
            Assert.That(gate.Phase, Is.EqualTo(LobbyPhase.Waiting));
            gate.Advance(30);
            Assert.That(gate.Phase, Is.EqualTo(LobbyPhase.Countdown));
            Assert.That(gate.Snapshot().RemainingTicks, Is.EqualTo(150));
            Assert.That(gate.Snapshot().PlayerCount, Is.EqualTo(3));
            gate.Remove(3);
            Assert.That(gate.Phase, Is.EqualTo(LobbyPhase.Countdown));
            gate.Disconnect(1);
            gate.Advance(1000);
            Assert.That(gate.Phase, Is.EqualTo(LobbyPhase.Waiting));
        }

        [Test]
        public void RecoveryChangesMembershipRejectsOldPacketsAndRequiresFreshReadyAfterConfirmation()
        {
            var gate = TwoReady();
            uint previousEpoch = Epoch(gate, 1);
            gate.Advance(10);
            gate.Disconnect(1);
            gate.Connect(1, recovering: true);
            uint currentEpoch = Epoch(gate, 1);
            Assert.That(currentEpoch, Is.Not.EqualTo(previousEpoch));
            Assert.That(Ready(gate, 1), Is.EqualTo(LobbyCommandResult.Rejected));
            Assert.That(gate.ConfirmRecovery(1, previousEpoch), Is.False);
            Assert.That(gate.ConfirmRecovery(1, currentEpoch), Is.True);
            gate.Advance(100);
            Assert.That(gate.Phase, Is.EqualTo(LobbyPhase.Waiting));
            Assert.That(gate.Apply(1, new LobbyReadyCommand(1, previousEpoch, 1000, true)),
                Is.EqualTo(LobbyCommandResult.Rejected));
            Ready(gate, 1);
            gate.Advance(101);
            Assert.That(gate.Snapshot().RemainingTicks, Is.EqualTo(150));
        }

        [Test]
        public void SequenceWrapDuplicatesAndConflictingReplaysAreHandledWithoutChangingReady()
        {
            var gate = new LobbyStartGate(1);
            gate.Connect(1);
            Assert.That(Ready(gate, 1, uint.MaxValue), Is.EqualTo(LobbyCommandResult.Applied));
            Assert.That(Ready(gate, 1, uint.MaxValue, false), Is.EqualTo(LobbyCommandResult.Rejected));
            Assert.That(Ready(gate, 1, 1, false), Is.EqualTo(LobbyCommandResult.Applied));
            Assert.That(Ready(gate, 1, uint.MaxValue), Is.EqualTo(LobbyCommandResult.Rejected));
            Assert.That(Ready(gate, 1, 0), Is.EqualTo(LobbyCommandResult.Rejected));
            Assert.That(gate.Snapshot().GetPlayer(0).Ready, Is.False);
        }

        [Test]
        public void TickWrapAndOldTicksCannotFinishOrRewindCountdown()
        {
            var gate = TwoReady(5);
            gate.Advance(uint.MaxValue - 2);
            Assert.That(gate.Advance(uint.MaxValue - 3), Is.False);
            gate.Advance(1);
            Assert.That(gate.Snapshot().RemainingTicks, Is.EqualTo(1));
            gate.Advance(2);
            Assert.That(gate.Phase, Is.EqualTo(LobbyPhase.Playing));
            Assert.That(gate.PlayingStartedTick, Is.EqualTo(2));
            Assert.That(gate.Advance(uint.MaxValue), Is.False);
        }

        [Test]
        public void PlayingAllowsLateJoinAndOfflineTimeWithoutRestartUntilOwnerFinishesAndResets()
        {
            var gate = TwoReady(1);
            gate.Advance(0);
            Assert.That(gate.BeginNextRound(), Is.False);
            gate.Advance(1);
            gate.Disconnect(1);
            gate.Disconnect(2);
            gate.Advance(100);
            gate.Connect(3);
            Assert.That(gate.Phase, Is.EqualTo(LobbyPhase.Playing));
            Assert.That(gate.BeginNextRound(), Is.False);
            Assert.That(gate.Finish(), Is.True);
            Assert.That(Ready(gate, 3), Is.EqualTo(LobbyCommandResult.Rejected));
            uint oldRound = gate.RoundEpoch;
            uint membership = Epoch(gate, 3);
            Assert.That(gate.BeginNextRound(), Is.True);
            Assert.That(gate.RoundEpoch, Is.Not.EqualTo(oldRound));
            Assert.That(gate.Apply(3, new LobbyReadyCommand(oldRound, membership, 100, true)),
                Is.EqualTo(LobbyCommandResult.Rejected));
            Assert.That(gate.Snapshot().GetPlayer(2).Ready, Is.False);
        }

        [Test]
        public void MembershipCapacityIncludesRetainedPlayersAndSnapshotsAreSortedAndIndependent()
        {
            var gate = new LobbyStartGate(150);
            Assert.That(gate.Connect(0), Is.False);
            for (uint entity = 64; entity > 0; entity--)
            {
                Assert.That(gate.Connect(entity), Is.True);
            }
            Assert.That(gate.Connect(1), Is.False);
            Assert.That(gate.Connect(65), Is.False);
            var snapshot = gate.Snapshot();
            Assert.That(snapshot.GetPlayer(0).EntityId, Is.EqualTo(1));
            Ready(gate, 1);
            Assert.That(snapshot.GetPlayer(0).Ready, Is.False);
            Assert.That(snapshot.GetPlayer(0).ReadySequence, Is.Zero);
            Assert.That(gate.Snapshot().GetPlayer(0).ReadySequence, Is.EqualTo(1));
            uint oldEpoch = Epoch(gate, 1);
            gate.Disconnect(1);
            Assert.That(gate.Connect(65), Is.False);
            Assert.That(gate.Remove(1), Is.True);
            Assert.That(gate.Connect(1), Is.True);
            Assert.That(Epoch(gate, 1), Is.Not.EqualTo(oldEpoch));
            Assert.That(gate.Apply(1, new LobbyReadyCommand(1, oldEpoch, 10, true)),
                Is.EqualTo(LobbyCommandResult.Rejected));
        }
    }
}

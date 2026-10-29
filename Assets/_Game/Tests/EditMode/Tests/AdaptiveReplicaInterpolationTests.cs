using System.Collections.Generic;
using AFPS.Math;
using AFPS.NetCode.SnapshotInterpolation;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class AdaptiveReplicaInterpolationTests
    {
        [Test]
        public void DelayedJitterySnapshots_UseBufferedInterpolationInsteadOfExtrapolationLimit()
        {
            var clock = new AdaptiveInterpolationClock(50, 20, 2d);
            var adaptive = Create(clock);
            var fixedDelay = Create(null);
            var pending = new List<KeyValuePair<uint, RemotePlayerMovementSnapshot>>();
            int adaptiveLimits = 0;
            int fixedLimits = 0;
            int interpolated = 0;
            uint sent = 0;
            for (uint tick = 0; tick < 1000; tick++)
            {
                adaptive.UpdateInterpolationQuality(tick * 20000UL, true, 160d, 20d);
                if (tick % 5 == 0 || tick % 5 == 2)
                {
                    // 20Hz快照，下行延迟3~5 Tick（60~100ms）；同帧到达时逆序交付，覆盖过期包拒绝。
                    pending.Add(new KeyValuePair<uint, RemotePlayerMovementSnapshot>(tick + 3 + sent % 3, Snapshot(tick)));
                    sent++;
                }
                for (int index = pending.Count - 1; index >= 0; index--)
                {
                    if (pending[index].Key <= tick)
                    {
                        var snapshot = pending[index].Value;
                        adaptive.TryInsertSnapshot(snapshot, snapshot.ServerTick + 1, out _);
                        fixedDelay.TryInsertSnapshot(snapshot, snapshot.ServerTick + 1, out _);
                        pending.RemoveAt(index);
                    }
                }
                if (tick > 200)
                {
                    Assert.That(adaptive.TrySample(7, tick, out var adaptiveState), Is.True);
                    Assert.That(fixedDelay.TrySample(7, tick, out var fixedState), Is.True);
                    if (adaptiveState.Status == RemotePlayerSamplingStatus.ExtrapolationLimitReached)
                    {
                        adaptiveLimits++;
                    }
                    if (fixedState.Status == RemotePlayerSamplingStatus.ExtrapolationLimitReached)
                    {
                        fixedLimits++;
                    }
                    if (adaptiveState.Status == RemotePlayerSamplingStatus.Interpolated)
                    {
                        interpolated++;
                    }
                }
            }
            Assert.That(adaptive.InterpolationDelayTicks, Is.EqualTo(9d));
            Assert.That(fixedLimits, Is.GreaterThan(100));
            Assert.That(adaptiveLimits, Is.LessThan(fixedLimits / 10));
            Assert.That(interpolated, Is.GreaterThan(700));
        }

        [Test]
        public void SharedClockAndTeleportBoundary_RemainConsistentAndClearRestartsEpoch()
        {
            var set = Create(new AdaptiveInterpolationClock(50, 20, 2d));
            set.TryInsertSnapshot(Snapshot(100), 1, out _);
            var teleport = new RemotePlayerMovementSnapshot(7, 105, FixVec3.Right * Fix.FromInt(10),
                Fix.Zero, Fix.Zero, FixVec3.Zero, isTeleport: true);
            set.TryInsertSnapshot(teleport, 2, out _);
            var peer = new RemotePlayerMovementSnapshot(8, 105, FixVec3.Zero, Fix.Zero, Fix.Zero, FixVec3.Zero);
            set.TryInsertSnapshot(peer, 2, out _);
            Assert.That(set.TrySample(7, 107d, out var before), Is.True);
            Assert.That(before.Status, Is.EqualTo(RemotePlayerSamplingStatus.HeldBeforeTeleport));
            Assert.That(set.TrySample(7, 108d, out var after), Is.True);
            Assert.That(after.Status, Is.EqualTo(RemotePlayerSamplingStatus.Teleported));
            set.TrySample(8, 108d, out var shared);
            Assert.That(shared.ServerTickTime, Is.EqualTo(after.ServerTickTime));
            set.TrySample(7, 107d, out var correction);
            Assert.That(correction.ServerTickTime, Is.EqualTo(after.ServerTickTime));
            set.Clear();
            Assert.That(set.Count, Is.Zero);
            Assert.That(set.InterpolationDelayTicks, Is.EqualTo(3d));
            set.TryInsertSnapshot(Snapshot(10), 1, out _);
            set.TrySample(7, 13d, out var restarted);
            Assert.That(restarted.ServerTickTime, Is.EqualTo(10d));
        }

        private static RemotePlayerMovementReplicas Create(AdaptiveInterpolationClock clock) =>
            new RemotePlayerMovementReplicas(32, 0.02f, 2d, 2d, 5f, clock);

        private static RemotePlayerMovementSnapshot Snapshot(uint tick) =>
            new RemotePlayerMovementSnapshot(7, tick, FixVec3.Right * Fix.FromRatio((int)tick, 100),
                Fix.Zero, Fix.Zero, FixVec3.Right * Fix.Half);
    }
}

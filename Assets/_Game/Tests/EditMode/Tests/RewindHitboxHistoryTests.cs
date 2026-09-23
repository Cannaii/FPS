using AFPS.NetCode.LagCompensation;
using AFPS.NetCode.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace AFPS.Tests.EditMode
{
    public sealed class RewindHitboxHistoryTests
    {
        [Test]
        public void Query_ReturnsClosestFrameAtOrBeforeRequestedTick()
        {
            RewindHitboxHistory history = new RewindHitboxHistory(8);
            history.Record(100, Target(100f));
            history.Record(102, Target(102f));
            history.Record(104, Target(104f));

            Assert.That(history.TryGetAtOrBefore(103, 105, 10, out ServerCombatTarget target, out uint tick), Is.True);
            Assert.That(tick, Is.EqualTo(102));
            Assert.That(target.Position.x, Is.EqualTo(102f));
        }

        [Test]
        public void Query_RejectsOutsideWindowAndSupportsTickWrap()
        {
            RewindHitboxHistory history = new RewindHitboxHistory(4);
            history.Record(uint.MaxValue, Target(1f));
            history.Record(0, Target(2f));
            Assert.That(history.TryGetAtOrBefore(uint.MaxValue, 1, 3, out _, out uint tick), Is.True);
            Assert.That(tick, Is.EqualTo(uint.MaxValue));
            Assert.That(history.TryGetAtOrBefore(90, 105, 10, out _, out _), Is.False);
            Assert.That(history.TryGetAtOrBefore(106, 105, 10, out _, out _), Is.False);
        }

        [Test]
        public void Record_OverwritesOldestFrameAtCapacity()
        {
            RewindHitboxHistory history = new RewindHitboxHistory(2);
            history.Record(1, Target(1f));
            history.Record(2, Target(2f));
            history.Record(3, Target(3f));
            Assert.That(history.TryGetAtOrBefore(1, 3, 3, out _, out _), Is.False);
        }

        private static ServerCombatTarget Target(float x) => new ServerCombatTarget(2, new Vector3(x, 0f, 0f), 0.5f, 2f, 100f);
    }
}

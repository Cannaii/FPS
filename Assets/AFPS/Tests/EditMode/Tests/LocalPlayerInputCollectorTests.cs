using AFPS.Input;
using NUnit.Framework;
using UnityEngine;

namespace AFPS.Tests.EditMode
{
    public sealed class LocalPlayerInputCollectorTests
    {
        [Test]
        public void AutomationInput_NormalizesMovementAnglesAndConsumesJumpOnce()
        {
            GameObject gameObject = new GameObject("LocalPlayerInputCollectorTests");
            try
            {
                LocalPlayerInputCollector collector = gameObject.AddComponent<LocalPlayerInputCollector>();
                collector.SetAutomationInput(1f, 1f, -10f, 120f, true, true);

                var first = collector.ConsumeCommand(42);
                Assert.That(first.Tick, Is.EqualTo(42));
                Assert.That(new Vector2(first.MoveX, first.MoveY).magnitude, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(first.LookYaw, Is.EqualTo(350f).Within(0.0001f));
                Assert.That(first.LookPitch, Is.EqualTo(89f).Within(0.0001f));
                Assert.That(first.JumpPressed, Is.True);
                Assert.That(first.FirePressed, Is.True);
                Assert.That(first.ShotSequence, Is.EqualTo(1));

                var second = collector.ConsumeCommand(43);
                Assert.That(second.JumpPressed, Is.False);
                Assert.That(second.FirePressed, Is.False);
                Assert.That(second.ShotSequence, Is.Zero);

                collector.SetAutomationInput(0f, 0f, 0f, 0f, false, true);
                var nextShot = collector.ConsumeCommand(44);
                Assert.That(nextShot.ShotSequence, Is.EqualTo(2));

                collector.ClearAutomationInput();
                var cleared = collector.ConsumeCommand(45);
                Assert.That(cleared.MoveX, Is.Zero);
                Assert.That(cleared.MoveY, Is.Zero);
                Assert.That(cleared.JumpPressed, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}

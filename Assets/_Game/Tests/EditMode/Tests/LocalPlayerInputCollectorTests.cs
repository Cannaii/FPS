using AFPS.Input;
using NUnit.Framework;
using UnityEngine;

namespace AFPS.Tests.EditMode
{
    public sealed class LocalPlayerInputCollectorTests
    {
        [Test]
        public void Automation_GrappleHeldPersistsAcrossTicksAndClearsOnReset()
        {
            var target = new GameObject("Grapple input test");
            try
            {
                var collector = target.AddComponent<LocalPlayerInputCollector>();
                collector.SetAutomationInput(0f, 0f, 0f, 0f, false, grappleHeld: true);
                Assert.That(collector.ConsumeCommand(uint.MaxValue).GrappleHeld, Is.True);
                Assert.That(collector.ConsumeCommand(0).GrappleHeld, Is.True);
                collector.ClearAutomationInput();
                Assert.That(collector.ConsumeCommand(1).GrappleHeld, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void Automation_CrouchRemainsHeldAcrossTicksAndClearsOnReset()
        {
            var target = new GameObject("Crouch input test");
            try
            {
                var collector = target.AddComponent<LocalPlayerInputCollector>();
                collector.SetAutomationInput(0f, 0f, 0f, 0f, false, crouchHeld: true);
                Assert.That(collector.ConsumeCommand(uint.MaxValue).CrouchHeld, Is.True);
                Assert.That(collector.ConsumeCommand(0).CrouchHeld, Is.True);
                collector.ClearAutomationInput();
                Assert.That(collector.ConsumeCommand(1).CrouchHeld, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void Automation_WallBounceEdgeIsBufferedOnceAndClearedOnReset()
        {
            var target = new GameObject("Wall bounce input test");
            try
            {
                var collector = target.AddComponent<LocalPlayerInputCollector>();
                collector.SetAutomationInput(0f, 0f, 0f, 0f, false, wallBouncePressed: true);
                collector.SetAutomationInput(0f, 0f, 0f, 0f, false);
                Assert.That(collector.ConsumeCommand(uint.MaxValue).WallBouncePressed, Is.True);
                Assert.That(collector.ConsumeCommand(0).WallBouncePressed, Is.False);
                collector.SetAutomationInput(0f, 0f, 0f, 0f, false, wallBouncePressed: true);
                collector.ClearAutomationInput();
                Assert.That(collector.ConsumeCommand(1).WallBouncePressed, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void Automation_DashEdgeIsBufferedUntilConsumedAndClearedOnReset()
        {
            var target = new GameObject("Dash input test");
            try
            {
                var collector = target.AddComponent<LocalPlayerInputCollector>();
                collector.SetAutomationInput(0f, 1f, 0f, 0f, false, dashPressed: true);
                collector.SetAutomationInput(0f, 1f, 90f, 0f, false);
                Assert.That(collector.ConsumeCommand(uint.MaxValue).DashPressed, Is.True);
                Assert.That(collector.ConsumeCommand(0).DashPressed, Is.False);
                collector.SetAutomationInput(0f, 1f, 0f, 0f, false, dashPressed: true);
                collector.ClearAutomationInput();
                Assert.That(collector.ConsumeCommand(1).DashPressed, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void Automation_SprintIsHeldAcrossTicksAndClearedWithInput()
        {
            var target = new GameObject("Sprint input test");
            try
            {
                var collector = target.AddComponent<LocalPlayerInputCollector>();
                collector.SetAutomationInput(0f, 1f, 0f, 0f, false, sprintHeld: true);
                Assert.That(collector.ConsumeCommand(uint.MaxValue).SprintHeld, Is.True);
                Assert.That(collector.ConsumeCommand(0).SprintHeld, Is.True);
                collector.ClearAutomationInput();
                Assert.That(collector.ConsumeCommand(1).SprintHeld, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void Automation_HeldFireUsesConfiguredTickCadenceAndReloadIsConsumedOnce()
        {
            GameObject target = new GameObject("HeldFireTests");
            try
            {
                LocalPlayerInputCollector collector = target.AddComponent<LocalPlayerInputCollector>();
                collector.ConfigureFireInterval(3);
                collector.SetAutomationInput(0, 0, 0, 0, false, fireHeld: true, reloadPressed: true);
                var first = collector.ConsumeCommand(uint.MaxValue - 1);
                Assert.That(first.FirePressed, Is.True);
                Assert.That(first.ReloadPressed, Is.True);
                Assert.That(collector.ConsumeCommand(uint.MaxValue).FirePressed, Is.False);
                var wrapped = collector.ConsumeCommand(0);
                Assert.That(wrapped.FirePressed, Is.False);
                Assert.That(wrapped.ReloadPressed, Is.False);
                var next = collector.ConsumeCommand(1);
                Assert.That(next.FirePressed, Is.True);
                Assert.That(next.ShotSequence, Is.EqualTo(2));
                collector.ClearAutomationInput();
                Assert.That(collector.ConsumeCommand(4).FirePressed, Is.False);
                Assert.Throws<System.ArgumentOutOfRangeException>(() => collector.ConfigureFireInterval(0));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }
        [Test]
        public void AutomationInput_StoresIndependentGridAxesAndConsumesEdgesOnce()
        {
            GameObject gameObject = new GameObject("LocalPlayerInputCollectorTests");
            try
            {
                LocalPlayerInputCollector collector = gameObject.AddComponent<LocalPlayerInputCollector>();
                collector.SetAutomationInput(1f, 1f, -10f, 120f, true, true);
                var first = collector.ConsumeCommand(42);
                Assert.That(first.Tick, Is.EqualTo(42));
                Assert.That(first.MoveXFixed.Raw, Is.EqualTo(65536));
                Assert.That(first.MoveYFixed.Raw, Is.EqualTo(65536));
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

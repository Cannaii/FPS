using AFPS.Simulation.Characters;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class MovementInputHashTests
    {
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        [TestCase(8)]
        [TestCase(9)]
        [TestCase(10)]
        public void EveryMovementFieldChangesFingerprint(int field)
        {
            var original = new PlayerInputCommand { Tick = 17 };
            var changed = original;
            switch (field)
            {
                case 0:
                    changed.Tick++;
                    break;
                case 1:
                    changed.MoveX = -1;
                    break;
                case 2:
                    changed.MoveY = 1;
                    break;
                case 3:
                    changed.LookYaw = 0.01f;
                    break;
                case 4:
                    changed.LookPitch = -0.01f;
                    break;
                case 5:
                    changed.JumpPressed = true;
                    break;
                case 6:
                    changed.SprintHeld = true;
                    break;
                case 7:
                    changed.DashPressed = true;
                    break;
                case 8:
                    changed.WallBouncePressed = true;
                    break;
                case 9:
                    changed.CrouchHeld = true;
                    break;
                case 10:
                    changed.GrappleHeld = true;
                    break;
            }
            Assert.That(MovementInputHash.Compute(changed), Is.Not.EqualTo(MovementInputHash.Compute(original)));
        }

        [Test]
        public void CombatOnlyFields_AreExcludedFromMovementFingerprint()
        {
            var original = new PlayerInputCommand { Tick = 17, MoveX = -0.5f, LookPitch = -12 };
            var changed = original;
            changed.FirePressed = true;
            changed.ReloadPressed = true;
            changed.SwitchWeaponPressed = true;
            changed.ShotSequence = uint.MaxValue;
            changed.ShotServerTick = 123;
            Assert.That(MovementInputHash.Compute(changed), Is.EqualTo(MovementInputHash.Compute(original)));
        }
    }
}

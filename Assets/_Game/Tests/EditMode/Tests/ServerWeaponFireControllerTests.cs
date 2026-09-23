using AFPS.NetCode.Weapons;
using AFPS.Simulation.Characters;
using AFPS.Simulation.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace AFPS.Tests.EditMode
{
    public sealed class ServerWeaponFireControllerTests
    {
        [Test]
        public void Process_AcceptsNewShotAndBuildsDirectionFromAuthoritativeState()
        {
            WeaponSimulationConfig config = new WeaponSimulationConfig(3, WeaponFireMode.Hitscan, 2, 5, 25f, 150f, 0f, 1.5f);
            ServerWeaponFireController controller = new ServerWeaponFireController(config);
            PlayerInputCommand command = new PlayerInputCommand { FirePressed = true, ShotSequence = 10 };
            PlayerState state = new PlayerState { Position = new Vector3(2f, 3f, 4f), Yaw = 90f, Pitch = 0f };

            WeaponFireResult result = controller.Process(100, 7, command, state);

            Assert.That(result.Accepted, Is.True);
            Assert.That(result.RemainingAmmo, Is.EqualTo(1));
            Assert.That(result.Shot.EntityId, Is.EqualTo(7));
            Assert.That(result.Shot.ShotSequence, Is.EqualTo(10));
            Assert.That(result.Shot.Origin, Is.EqualTo(new Vector3(2f, 4.5f, 4f)));
            Assert.That(Vector3.Angle(result.Shot.Direction, Vector3.right), Is.LessThan(0.001f));
        }

        [Test]
        public void Process_RejectsRateLimitDuplicateEmptyAndUnavailableWithoutSpendingExtraAmmo()
        {
            WeaponSimulationConfig config = new WeaponSimulationConfig(1, WeaponFireMode.Hitscan, 1, 5, 20f, 100f, 0f, 1.6f);
            ServerWeaponFireController controller = new ServerWeaponFireController(config);
            PlayerState state = new PlayerState { Yaw = 0f, Pitch = 0f };

            Assert.That(controller.Process(10, 1, Fire(1), state).Accepted, Is.True);
            Assert.That(controller.Process(11, 1, Fire(1), state).RejectReason, Is.EqualTo(WeaponFireRejectReason.DuplicateOrOldSequence));
            Assert.That(controller.Process(11, 1, Fire(2), state).RejectReason, Is.EqualTo(WeaponFireRejectReason.FireRateLimited));
            Assert.That(controller.Process(15, 1, Fire(3), state).RejectReason, Is.EqualTo(WeaponFireRejectReason.EmptyMagazine));
            Assert.That(controller.Ammo, Is.Zero);

            controller.Reload();
            controller.SetEquipped(false);
            Assert.That(controller.Process(20, 1, Fire(4), state).RejectReason, Is.EqualTo(WeaponFireRejectReason.WeaponUnavailable));
            Assert.That(controller.Ammo, Is.EqualTo(1));
        }

        [Test]
        public void Process_HandlesTickAndShotSequenceWraparound()
        {
            WeaponSimulationConfig config = new WeaponSimulationConfig(1, WeaponFireMode.Hitscan, 3, 2, 20f, 100f, 0f, 1.6f);
            ServerWeaponFireController controller = new ServerWeaponFireController(config);
            PlayerState state = new PlayerState { Yaw = 0f, Pitch = 0f };

            Assert.That(controller.Process(uint.MaxValue - 1, 1, Fire(uint.MaxValue), state).Accepted, Is.True);
            Assert.That(controller.Process(0, 1, Fire(1), state).Accepted, Is.True);
        }

        private static PlayerInputCommand Fire(uint sequence) => new PlayerInputCommand { FirePressed = true, ShotSequence = sequence };
    }
}

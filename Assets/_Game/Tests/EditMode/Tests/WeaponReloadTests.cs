using System;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Weapons;
using AFPS.Simulation.Characters;
using AFPS.Simulation.Weapons;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class WeaponReloadTests
    {
        [Test]
        public void Reload_CompletesAtBoundaryAndRepeatedRequestsDoNotRestart()
        {
            ServerWeaponFireController controller = new ServerWeaponFireController(WeaponSimulationConfig.DefaultRifle);
            PlayerState state = default;
            Assert.That(controller.Process(10, 1, Fire(1), state).Accepted, Is.True);
            controller.Process(11, 1, new PlayerInputCommand { ReloadPressed = true }, state);
            Assert.That(controller.IsReloading, Is.True);
            Assert.That(controller.Ammo, Is.EqualTo(29));
            controller.Process(80, 1, new PlayerInputCommand { ReloadPressed = true }, state);
            Assert.That(controller.Process(110, 1, Fire(2), state).RejectReason, Is.EqualTo(WeaponFireRejectReason.Reloading));
            Assert.That(controller.Ammo, Is.EqualTo(29));
            Assert.That(controller.Process(111, 1, Fire(3), state).Accepted, Is.True);
            Assert.That(controller.IsReloading, Is.False);
            Assert.That(controller.Ammo, Is.EqualTo(29));
        }

        [Test]
        public void Reload_HandlesWorldTickWrapAndFinishesWithoutFireInput()
        {
            ServerWeaponFireController controller = new ServerWeaponFireController(WeaponSimulationConfig.DefaultRifle);
            controller.Process(uint.MaxValue - 20, 1, Fire(1), default);
            controller.Process(uint.MaxValue - 10, 1, new PlayerInputCommand { ReloadPressed = true }, default);
            controller.Process(88, 1, default, default);
            Assert.That(controller.IsReloading, Is.True);
            controller.Process(89, 1, default, default);
            Assert.That(controller.IsReloading, Is.False);
            Assert.That(controller.Ammo, Is.EqualTo(30));
        }

        [Test]
        public void Reload_IgnoresFullMagazineAndCancelsWhenUnequipped()
        {
            ServerWeaponFireController controller = new ServerWeaponFireController(WeaponSimulationConfig.DefaultRifle);
            controller.Process(0, 1, new PlayerInputCommand { ReloadPressed = true }, default);
            Assert.That(controller.IsReloading, Is.False);
            controller.Process(1, 1, Fire(1), default);
            controller.Process(2, 1, new PlayerInputCommand { ReloadPressed = true }, default);
            controller.SetEquipped(false);
            Assert.That(controller.IsReloading, Is.False);
            controller.Process(500, 1, new PlayerInputCommand { ReloadPressed = true }, default);
            Assert.That(controller.IsReloading, Is.False);
            Assert.That(controller.Ammo, Is.EqualTo(29));
            controller.SetEquipped(true);
            Assert.That(controller.Process(501, 1, Fire(2), default).Accepted, Is.True);
        }

        [Test]
        public void Reload_TakesPriorityOverSimultaneousShotAndDoesNotReuseShotSequence()
        {
            ServerWeaponFireController controller = new ServerWeaponFireController(WeaponSimulationConfig.DefaultRifle);
            controller.Process(0, 1, Fire(1), default);
            PlayerInputCommand command = Fire(2);
            command.ReloadPressed = true;
            Assert.That(controller.Process(5, 1, command, default).RejectReason, Is.EqualTo(WeaponFireRejectReason.Reloading));
            Assert.That(controller.Process(105, 1, command, default).RejectReason, Is.EqualTo(WeaponFireRejectReason.DuplicateOrOldSequence));
            Assert.That(controller.Ammo, Is.EqualTo(30));
        }

        [Test]
        public void Reload_InputRoundTripsAndFallbackClearsOneShotEvents()
        {
            PlayerInputCommand command = new PlayerInputCommand { Tick = 1, ReloadPressed = true };
            byte[] packet = new byte[InputCommandBatchCodec.GetPacketSize(1)];
            Assert.That(InputCommandBatchCodec.TrySerialize(new InputCommandBatch(
                new ArraySegment<PlayerInputCommand>(new[] { command })), 1, new ArraySegment<byte>(packet), out _), Is.True);
            PlayerInputCommand[] decoded = new PlayerInputCommand[1];
            Assert.That(InputCommandBatchCodec.TryDeserialize(new ArraySegment<byte>(packet), decoded, 0, out _, out _), Is.True);
            Assert.That(decoded[0].ReloadPressed, Is.True);
            Assert.That(decoded[0].CreateFallback(2, true).ReloadPressed, Is.False);
            packet[19] = 8; // 第一个命令的按钮位；未定义 bit3 必须拒绝。
            Assert.That(InputCommandBatchCodec.TryDeserialize(new ArraySegment<byte>(packet), decoded, 0, out _, out _), Is.False);
        }

        private static PlayerInputCommand Fire(uint sequence) => new PlayerInputCommand
        {
            FirePressed = true,
            ShotSequence = sequence
        };
    }
}

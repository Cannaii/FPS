using System;
using AFPS.Input;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Weapons;
using AFPS.Simulation.Characters;
using NUnit.Framework;
using UnityEngine;

namespace AFPS.Tests.EditMode
{
    public sealed class WeaponLoadoutTests
    {
        [Test]
        public void SwitchInput_RoundTripsAndFallbackDoesNotRepeatSwitch()
        {
            var input = new PlayerInputCommand { Tick = 1, SwitchWeaponPressed = true };
            byte[] bytes = new byte[InputCommandBatchCodec.GetPacketSize(1)];
            Assert.That(InputCommandBatchCodec.TrySerialize(
                new InputCommandBatch(new ArraySegment<PlayerInputCommand>(new[] { input })), 1,
                new ArraySegment<byte>(bytes), out _), Is.True);
            var storage = new PlayerInputCommand[1];
            Assert.That(InputCommandBatchCodec.TryDeserialize(new ArraySegment<byte>(bytes),
                storage, 0, out _, out _), Is.True);
            Assert.That(storage[0].SwitchWeaponPressed, Is.True);
            Assert.That(input.CreateFallback(2, true).SwitchWeaponPressed, Is.False);
        }

        [Test]
        public void Prediction_BlocksWhileSwitchPendingAndWhileEquipping()
        {
            var prediction = new ClientWeaponPrediction();
            prediction.Apply(new AuthoritativeWeaponState(1, 100, 0, 30, 30, 0, true));
            prediction.Observe(new PlayerInputCommand { Tick = 1, SwitchWeaponPressed = true });
            Assert.That(prediction.CanFire, Is.False);
            Assert.That(prediction.CanReload, Is.False);
            prediction.Apply(new AuthoritativeWeaponState(1, 101, 1, 5, 5, 0, true, 2, 10));
            Assert.That(prediction.IsSwitchPending, Is.False);
            Assert.That(prediction.CanFire, Is.False);
            prediction.Apply(new AuthoritativeWeaponState(1, 111, 2, 5, 5, 0, true, 2));
            Assert.That(prediction.CanFire, Is.True);
        }

        [Test]
        public void HeldFire_DoesNotConsumeCadenceWhileWeaponIsUnavailable()
        {
            var root = new GameObject("Weapon cadence test");
            try
            {
                var collector = root.AddComponent<LocalPlayerInputCollector>();
                collector.ConfigureFireInterval(35);
                collector.SetAutomationInput(0, 0, 0, 0, false, fireHeld: true);
                Assert.That(collector.ConsumeCommand(1, false).FirePressed, Is.False);
                Assert.That(collector.ConsumeCommand(2, true).FirePressed, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void WeaponStateCodec_PreservesWeaponIdentityAndEquipTime()
        {
            var state = new AuthoritativeWeaponState(1, 100, 20, 4, 5, 0, true, 2, 10);
            byte[] bytes = new byte[AuthoritativeWeaponStateCodec.PacketSize];
            Assert.That(AuthoritativeWeaponStateCodec.TrySerialize(state, 1, new ArraySegment<byte>(bytes), out _), Is.True);
            Assert.That(AuthoritativeWeaponStateCodec.TryDeserialize(new ArraySegment<byte>(bytes), out _, out var result), Is.True);
            Assert.That(result.WeaponId, Is.EqualTo(2));
            Assert.That(result.EquipTicksRemaining, Is.EqualTo(10));
        }
    }
}

using System;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Weapons;
using AFPS.Simulation.Characters;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class WeaponStateTests
    {
        [TestCase(0u)]
        [TestCase(uint.MaxValue)]
        public void Codec_PreservesFullFieldsAndOffset(uint tick)
        {
            var state = new AuthoritativeWeaponState(7, tick, tick, int.MaxValue - 1, int.MaxValue, 100, true);
            byte[] bytes = new byte[AuthoritativeWeaponStateCodec.PacketSize + 6];
            var packet = new ArraySegment<byte>(bytes, 3, AuthoritativeWeaponStateCodec.PacketSize);
            Assert.That(AuthoritativeWeaponStateCodec.TrySerialize(state, 99, packet, out int count), Is.True);
            Assert.That(count, Is.EqualTo(43));
            Assert.That(bytes[3 + 12], Is.EqualTo(7)); // 独立小端实体字段。
            Assert.That(bytes[3 + 12 + 15], Is.EqualTo(127)); // int.MaxValue-1 的最高字节。
            Assert.That(AuthoritativeWeaponStateCodec.TryDeserialize(packet, out var header, out var decoded), Is.True);
            Assert.That(header.Sequence, Is.EqualTo(99));
            Assert.That(decoded.ServerTick, Is.EqualTo(tick));
            Assert.That(decoded.AckInputTick, Is.EqualTo(tick));
            Assert.That(decoded.Ammo, Is.EqualTo(state.Ammo));
            Assert.That(decoded.Capacity, Is.EqualTo(state.Capacity));
            Assert.That(decoded.ReloadTicksRemaining, Is.EqualTo(100));
            Assert.That(decoded.IsEquipped, Is.True);
        }

        [TestCase(12, -1)]
        [TestCase(16, 0)]
        [TestCase(20, 101)]
        [TestCase(24, 2)]
        public void Codec_RejectsMalformedFieldsWithoutPartialOutput(int offset, int value)
        {
            byte[] bytes = new byte[AuthoritativeWeaponStateCodec.PacketSize];
            Assert.That(AuthoritativeWeaponStateCodec.TrySerialize(State(0, 30), 1, new ArraySegment<byte>(bytes), out _), Is.True);
            int size = offset == 24 ? 1 : 4;
            for (int index = 0; index < size; index++)
            {
                bytes[12 + offset + index] = unchecked((byte)((uint)value >> (index * 8)));
            }
            Assert.That(AuthoritativeWeaponStateCodec.TryDeserialize(new ArraySegment<byte>(bytes), out _, out var decoded), Is.False);
            Assert.That(decoded.EntityId, Is.Zero);
        }

        [Test]
        public void Prediction_ReconcilesAcceptedRejectedAndDelayedAcknowledgements()
        {
            var prediction = new ClientWeaponPrediction();
            Assert.That(prediction.CanFire, Is.False);
            prediction.Apply(State(0, 30));
            prediction.Observe(new PlayerInputCommand { Tick = 1, FirePressed = true });
            Assert.That(prediction.Ammo, Is.EqualTo(29));
            prediction.Apply(State(1, 29));
            Assert.That(prediction.Ammo, Is.EqualTo(29));
            prediction.Observe(new PlayerInputCommand { Tick = 2, FirePressed = true });
            prediction.Apply(State(1, 29));
            Assert.That(prediction.Ammo, Is.EqualTo(28));
            // 第二次开火被服务器拒绝；确认 Tick 仍前进，因此撤销对应的本地预扣弹。
            prediction.Apply(State(2, 29));
            Assert.That(prediction.Ammo, Is.EqualTo(29));
            Assert.That(prediction.Apply(State(1, 30)), Is.False);
            Assert.That(prediction.Ammo, Is.EqualTo(29));
        }

        [Test]
        public void Prediction_ReloadConfirmationCancellationAndWrap()
        {
            var prediction = new ClientWeaponPrediction();
            prediction.Apply(State(uint.MaxValue - 1, 29));
            prediction.Observe(new PlayerInputCommand { Tick = uint.MaxValue, ReloadPressed = true });
            Assert.That(prediction.IsReloading, Is.True);
            Assert.That(prediction.CanFire, Is.False);
            prediction.Apply(new AuthoritativeWeaponState(1, 1, uint.MaxValue, 29, 30, 100, true));
            Assert.That(prediction.IsReloading, Is.True);
            prediction.Apply(new AuthoritativeWeaponState(1, 2, 0, 29, 30, 0, false));
            Assert.That(prediction.IsReloading, Is.False);
            Assert.That(prediction.CanFire, Is.False);
            prediction.Apply(State(1, 30));
            Assert.That(prediction.CanFire, Is.True);
            Assert.That(prediction.CanReload, Is.False);
            prediction.Clear();
            Assert.That(prediction.HasState, Is.False);
        }

        [Test]
        public void Prediction_BoundsPendingMemoryAndRecoversAfterAcknowledgement()
        {
            var prediction = new ClientWeaponPrediction();
            prediction.Apply(new AuthoritativeWeaponState(1, 0, 0, 1000, 1000, 0, true));
            for (uint tick = 1; tick <= 300; tick++)
            {
                prediction.Observe(new PlayerInputCommand { Tick = tick, FirePressed = true });
            }
            Assert.That(prediction.CanFire, Is.False);
            prediction.Apply(new AuthoritativeWeaponState(1, 300, 300, 700, 1000, 0, true));
            Assert.That(prediction.CanFire, Is.True);
            Assert.That(prediction.Ammo, Is.EqualTo(700));
        }

        private static AuthoritativeWeaponState State(uint acknowledged, int ammo) =>
            new AuthoritativeWeaponState(1, acknowledged, acknowledged, ammo, 30, 0, true);
    }
}

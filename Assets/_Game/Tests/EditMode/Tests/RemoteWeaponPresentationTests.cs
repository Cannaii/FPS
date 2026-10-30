using System;
using AFPS.Math;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.Weapons;
using AFPS.Presentation.Characters;
using AFPS.Presentation.Weapons;
using AFPS.Simulation.Weapons;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AFPS.Tests.EditMode
{
    public sealed class RemoteWeaponPresentationTests
    {
        private static RemoteWeaponState State(uint tick = 100, ushort weapon = 1,
            uint reload = 0, uint equip = 0, bool equipped = true)
        {
            return new RemoteWeaponState(2, tick, weapon, reload, equip, equipped);
        }

        [Test]
        public void Codec_OffsetRoundTripAndEveryTruncationOrTrailingByte()
        {
            var buffer = new byte[RemoteWeaponStateCodec.PacketSize + 9];
            var segment = new ArraySegment<byte>(buffer, 4, RemoteWeaponStateCodec.PacketSize);
            Assert.That(RemoteWeaponStateCodec.TrySerialize(State(reload: 100), uint.MaxValue,
                segment, out int bytes), Is.True);
            Assert.That(bytes, Is.EqualTo(RemoteWeaponStateCodec.PacketSize));
            Assert.That(RemoteWeaponStateCodec.TryDeserialize(segment, out var header, out var state), Is.True);
            Assert.That(header.Sequence, Is.EqualTo(uint.MaxValue));
            Assert.That(state.SamePresentation(State(reload: 100)), Is.True);
            for (int size = 0; size < bytes; size++)
            {
                Assert.That(RemoteWeaponStateCodec.TryDeserialize(
                    new ArraySegment<byte>(buffer, 4, size), out _, out _), Is.False, size.ToString());
            }
            Assert.That(RemoteWeaponStateCodec.TryDeserialize(
                new ArraySegment<byte>(buffer, 4, bytes + 1), out _, out _), Is.False);
            buffer[4 + bytes - 1] = 2;
            Assert.That(RemoteWeaponStateCodec.TryDeserialize(segment, out _, out _), Is.False);
        }

        [Test]
        public void Codec_RejectsInvalidTimersEntityWeaponAndOldProtocol()
        {
            var buffer = new byte[RemoteWeaponStateCodec.PacketSize];
            var segment = new ArraySegment<byte>(buffer);
            var invalid = new[]
            {
                default(RemoteWeaponState), State(weapon: 0), State(reload: 101),
                State(equip: WeaponCatalog.EquipDurationTicks + 1), State(reload: 1, equip: 1),
                State(reload: 1, equipped: false)
            };
            foreach (var state in invalid)
            {
                Assert.That(RemoteWeaponStateCodec.TrySerialize(state, 1, segment, out int size), Is.False);
                Assert.That(size, Is.Zero);
            }
            Assert.That(RemoteWeaponStateCodec.TrySerialize(State(), 1, segment, out _), Is.True);
            buffer[4] = 28;
            Assert.That(RemoteWeaponStateCodec.TryDeserialize(segment, out _, out _), Is.False);
        }

        [Test]
        public void Timeline_DoesNotPresentFutureSwitchAndExpiresLostStream()
        {
            var replicas = new RemoteWeaponReplicas();
            Assert.That(replicas.TryInsert(State(), 1), Is.True);
            Assert.That(replicas.TryInsert(State(110, 2, equip: 10), 2), Is.True);
            Assert.That(replicas.TrySample(2, 99.9d, out _), Is.False);
            Assert.That(replicas.TrySample(2, 109.9d, out var before), Is.True);
            Assert.That(before.WeaponId, Is.EqualTo(1));
            Assert.That(replicas.TrySample(2, 110d, out var after), Is.True);
            Assert.That(after.WeaponId, Is.EqualTo(2));
            Assert.That(replicas.TrySample(2, 160d, out _), Is.True);
            Assert.That(replicas.TrySample(2, 160.1d, out _), Is.False);
            Assert.That(replicas.TrySample(2, double.NaN, out _), Is.False);
            replicas.Remove(2);
            Assert.That(replicas.TrySample(2, 110d, out _), Is.False);
        }

        [Test]
        public void Timeline_RejectsReorderingAndSupportsSequenceAndTickWrap()
        {
            var replicas = new RemoteWeaponReplicas();
            Assert.That(replicas.TryInsert(State(uint.MaxValue), uint.MaxValue), Is.True);
            Assert.That(replicas.TryInsert(State(0, 2), 0), Is.True);
            Assert.That(replicas.TryInsert(State(uint.MaxValue), 1), Is.False);
            Assert.That(replicas.TryInsert(State(1), uint.MaxValue), Is.False);
            Assert.That(replicas.TrySample(2, (double)uint.MaxValue + 1, out var state), Is.True);
            Assert.That(state.WeaponId, Is.EqualTo(2));
            replicas.Clear();
            Assert.That(replicas.Count, Is.Zero);
        }

        [Test]
        public void Timeline_HistoryAndEntityCountsAreBounded()
        {
            var replicas = new RemoteWeaponReplicas();
            for (uint i = 0; i < 40; i++)
            {
                Assert.That(replicas.TryInsert(State(i), i), Is.True);
            }
            Assert.That(replicas.TrySample(2, 7d, out _), Is.False);
            for (uint entity = 3; entity <= 65; entity++)
            {
                Assert.That(replicas.TryInsert(new RemoteWeaponState(entity, 40, 1, 0, 0, true), 1), Is.True);
            }
            Assert.That(replicas.Count, Is.EqualTo(64));
            Assert.That(replicas.TryInsert(new RemoteWeaponState(66, 40, 1, 0, 0, true), 1), Is.False);
        }

        [Test]
        public void View_SwitchReloadExpiryAndHideRestoreAuthoredTransforms()
        {
            var actor = Object.Instantiate(Resources.Load<RemotePlayerView>("Characters/RemotePlayer").gameObject);
            try
            {
                var view = actor.GetComponentInChildren<RemoteWeaponView>(true);
                Assert.That(view, Is.Not.Null);
                Assert.That(view.IsConfigured, Is.True);
                Transform arm = view.transform.Find("LeftArm");
                Quaternion rest = arm.localRotation;
                view.Render(State(), false, 100d);
                Assert.That(view.IsLauncherVisible, Is.False);
                view.Render(State(110, 2, reload: 100), true, 160d);
                Assert.That(view.IsLauncherVisible, Is.True);
                Assert.That(view.IsReloading, Is.True);
                Assert.That(Quaternion.Angle(rest, arm.localRotation), Is.GreaterThan(20f));
                view.Render(State(110, 2, reload: 100), true, 210d);
                Assert.That(view.IsReloading, Is.False);
                Assert.That(Quaternion.Angle(rest, arm.localRotation), Is.LessThan(0.01f));
                view.Hide();
                Assert.That(view.IsLauncherVisible, Is.False);
                Assert.That(view.WeaponId, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(actor);
            }
        }

        [Test]
        public void View_ShotWaitsForDisplayTickAndDuplicateDoesNotReplay()
        {
            var actor = Object.Instantiate(Resources.Load<RemotePlayerView>("Characters/RemotePlayer").gameObject);
            try
            {
                var view = actor.GetComponentInChildren<RemoteWeaponView>(true);
                var shot = new AuthoritativeShot(2, 1, 105, WeaponSimulationConfig.DefaultRifle,
                    FixVec3.Zero, FixVec3.Forward);
                view.Render(State(), false, 100d);
                Assert.That(view.PlayShot(shot), Is.True);
                view.Render(State(), false, 104.9d);
                Assert.That(view.PresentedShotCount, Is.Zero);
                view.Render(State(), false, 105d);
                Assert.That(view.PresentedShotCount, Is.EqualTo(1));
                Assert.That(view.PlayShot(shot), Is.False);
                view.Hide();
                Assert.That(view.PlayShot(shot), Is.False);
                view.Render(State(), false, 106d);
                Assert.That(view.PresentedShotCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(actor);
            }
        }
    }
}

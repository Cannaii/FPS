using System.Reflection;
using AFPS.Bootstrap;
using AFPS.Math;
using AFPS.NetCode.SnapshotInterpolation;
using AFPS.NetCode.Weapons;
using AFPS.Presentation.Characters;
using AFPS.Presentation.Weapons;
using AFPS.Simulation.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace AFPS.Tests.EditMode
{
    public sealed class PlayerAudioAndShotTimingTests
    {
        [Test]
        public void Jump_AudioFollowsImpulseAndDoesNotReplayHeldSamples()
        {
            var root = new GameObject("Jump audio test");
            try
            {
                var audio = root.AddComponent<FootstepAudioView>();
                audio.Render(Vector3.zero, true, true);
                audio.Render(Vector3.up * 0.1f, false, true, false, 8f);
                Assert.That(audio.JumpCount, Is.EqualTo(1));
                audio.Render(Vector3.up * 0.2f, false, true, false, 7f);
                audio.Render(Vector3.up * 0.2f, false, true, false, 7f);
                Assert.That(audio.JumpCount, Is.EqualTo(1));
                audio.Render(Vector3.up * 0.1f, false, true, false, -2f);
                audio.Render(Vector3.up * 0.3f, false, true, false, 8f);
                Assert.That(audio.JumpCount, Is.EqualTo(2));
                audio.Render(Vector3.up * 9f, false, true, true, 8f);
                Assert.That(audio.JumpCount, Is.EqualTo(2));
                audio.Clear();
                audio.Render(Vector3.zero, true, true);
                audio.Render(Vector3.down, false, true, false, -3f);
                Assert.That(audio.JumpCount, Is.Zero);
                audio.Render(Vector3.up, false, true, false, 8f, false);
                Assert.That(audio.JumpCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RemoteReload_PlaysOncePerEndTickAndDoesNotReplayOnJoin()
        {
            var root = Object.Instantiate(Resources.Load<RemotePlayerView>("Characters/RemotePlayer").gameObject);
            try
            {
                var view = root.GetComponentInChildren<RemoteWeaponView>(true);
                view.Render(new RemoteWeaponState(2, 100, 1, 0, 0, true), false, 100);
                view.Render(new RemoteWeaponState(2, 101, 1, 100, 0, true), false, 101);
                Assert.That(view.PresentedReloadSoundCount, Is.EqualTo(1));
                view.Render(new RemoteWeaponState(2, 102, 1, 99, 0, true), false, 103);
                Assert.That(view.PresentedReloadSoundCount, Is.EqualTo(1));
                view.Render(new RemoteWeaponState(2, 201, 1, 0, 0, true), false, 201);
                view.Render(new RemoteWeaponState(2, 202, 1, 100, 0, true), false, 202);
                Assert.That(view.PresentedReloadSoundCount, Is.EqualTo(2));
                view.Hide();
                view.Render(new RemoteWeaponState(2, 203, 1, 99, 0, true), false, 203);
                Assert.That(view.PresentedReloadSoundCount, Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ShotRequest_UsesDisplayedTickAndHitsMovingTargetSeenOnScreen()
        {
            var root = new GameObject("Displayed shot time test");
            try
            {
                var replicas = new RemotePlayerMovementReplicas(8, 0.02f, 2d, 2d, 5f);
                for (uint tick = 100; tick <= 105; tick += 5)
                {
                    var position = new FixVec3(Fix.FromFloat((tick - 100) * 0.4f), Fix.Zero, Fix.FromInt(5));
                    var snapshot = new RemotePlayerMovementSnapshot(2, tick, position, Fix.Zero, Fix.Zero,
                        new FixVec3(Fix.FromInt(20), Fix.Zero, Fix.Zero), isGrounded: true);
                    Assert.That(replicas.TryInsertSnapshot(snapshot, tick, out _), Is.True);
                }
                var registry = root.AddComponent<RemotePlayerViewRegistry>();
                registry.Render(replicas, 105d);
                Assert.That(registry.TryGetDisplayedServerTick(out uint displayed), Is.True);
                Assert.That(displayed, Is.EqualTo(103));
                var controller = root.AddComponent<UnityNetworkGameplayController>();
                typeof(UnityNetworkGameplayController).GetField("remotePlayerWorldView",
                    BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, registry);
                uint selected = (uint)typeof(UnityNetworkGameplayController).GetMethod("SelectShotRewindTick",
                    BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, new object[] { 105u });
                Assert.That(selected, Is.EqualTo(displayed));
                var shot = new AuthoritativeShot(1, 1, 105, WeaponSimulationConfig.DefaultRifle,
                    new FixVec3(Fix.FromFloat(1.2f), Fix.One, Fix.Zero), FixVec3.Forward, selected);
                var shown = new ServerCombatTarget(2, new FixVec3(Fix.FromFloat(1.2f), Fix.Zero, Fix.FromInt(5)),
                    Fix.Half, Fix.FromInt(2), Fix.FromInt(100));
                var latest = new ServerCombatTarget(2, new FixVec3(Fix.FromInt(2), Fix.Zero, Fix.FromInt(5)),
                    Fix.Half, Fix.FromInt(2), Fix.FromInt(100));
                Assert.That(ServerHitscanResolver.TryResolveFixed(shot, new[] { shown }, out _, out _), Is.True);
                Assert.That(ServerHitscanResolver.TryResolveFixed(shot, new[] { latest }, out _, out _), Is.False);
                registry.Clear();
                Assert.That(registry.TryGetDisplayedServerTick(out _), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}

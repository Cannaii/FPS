using System;
using AFPS.Math;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.Weapons;
using AFPS.Presentation.Weapons;
using AFPS.Simulation.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace AFPS.Tests.EditMode
{
    public sealed class CombatFeedbackTests
    {
        [TestCase(WeaponFireRejectReason.InvalidSequence)]
        [TestCase(WeaponFireRejectReason.Reloading)]
        public void RejectionCodec_PreservesOffsetAndTickExtremes(WeaponFireRejectReason reason)
        {
            var value = new ShotRejection(uint.MaxValue, uint.MaxValue, 0, uint.MaxValue, reason);
            var bytes = new byte[ShotRejectionCodec.PacketSize + 6];
            var packet = new ArraySegment<byte>(bytes, 3, ShotRejectionCodec.PacketSize);
            Assert.That(ShotRejectionCodec.TrySerialize(value, uint.MaxValue, packet, out int written), Is.True);
            Assert.That(written, Is.EqualTo(29));
            Assert.That(ShotRejectionCodec.TryDeserialize(packet, out var header, out var decoded), Is.True);
            Assert.That(header.Sequence, Is.EqualTo(uint.MaxValue));
            Assert.That(decoded.InputTick, Is.Zero);
            Assert.That(decoded.ServerTick, Is.EqualTo(uint.MaxValue));
            Assert.That(decoded.Reason, Is.EqualTo(reason));
            bytes[3 + PacketHeader.Size + 16] = 255;
            Assert.That(ShotRejectionCodec.TryDeserialize(packet, out _, out decoded), Is.False);
            Assert.That(decoded.EntityId, Is.Zero);
            Assert.That(ShotRejectionCodec.TrySerialize(default, 0, packet, out _), Is.False);
        }

        [Test]
        public void Rejection_ImmediatelyRetiresMatchingPredictionAndExpiresUi()
        {
            var root = new GameObject("Rejection view test");
            try
            {
                var view = root.AddComponent<ShotPresentationController>();
                view.PredictProjectile(Shot(2, 3));
                view.PredictProjectile(Shot(2, 4));
                view.Reject(new ShotRejection(2, 3, 1, 100, WeaponFireRejectReason.FireRateLimited));
                Assert.That(view.TryGetProjectilePosition(2, 3, out _), Is.False);
                Assert.That(view.TryGetProjectilePosition(2, 4, out _), Is.True);
                Assert.That(view.RejectionFeedbackVisible, Is.True);
                Assert.That(view.LastRejectionReason, Is.EqualTo(WeaponFireRejectReason.FireRateLimited));
                view.Advance(1f);
                Assert.That(view.RejectionFeedbackVisible, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Damage_ShowsOnlyForLocalVictimAndClearsWithSession()
        {
            var root = new GameObject("Damage view test");
            try
            {
                var view = root.AddComponent<ShotPresentationController>();
                var shot = Shot(2, 1);
                var result = new AuthoritativeShotResult(shot, 29, true, 1, Fix.FromInt(80), FixVec3.Zero);
                view.Consume(result, 3);
                Assert.That(view.DamageFeedbackVisible, Is.False);
                view.Consume(result, 1);
                Assert.That(view.DamageFeedbackVisible, Is.True);
                Assert.That(view.LastDamageSource, Is.EqualTo(result.HitPoint));
                Assert.That(view.HitMarkerVisible, Is.False);
                Assert.That(view.ReceivedDamageCount, Is.EqualTo(1));
                view.Clear();
                Assert.That(view.DamageFeedbackVisible, Is.False);
                Assert.That(view.ReceivedDamageCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SelfDamage_DoesNotAwardHitOrKillMarker()
        {
            var root = new GameObject("Self damage test");
            try
            {
                var view = root.AddComponent<ShotPresentationController>();
                view.Consume(new AuthoritativeShotResult(Shot(1, 1), 4, true, 1, Fix.Zero, FixVec3.Zero), 1);
                Assert.That(view.HitMarkerVisible, Is.False);
                Assert.That(view.LastHitWasKill, Is.False);
                Assert.That(view.DamageFeedbackVisible, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MultipleTargets_PreserveKillMarkerOnlyWithinCurrentFeedbackWindow()
        {
            var root = new GameObject("Multi target marker test");
            try
            {
                var view = root.AddComponent<ShotPresentationController>();
                var shot = Shot(1, 1);
                var kill = new AuthoritativeShotResult(shot, 4, true, 2, Fix.Zero, FixVec3.Zero);
                var hit = new AuthoritativeShotResult(shot, 4, true, 3, Fix.FromInt(40), FixVec3.Zero);
                view.Consume(kill, 1);
                view.Consume(hit, 1);
                Assert.That(view.LastHitWasKill, Is.True);
                view.Advance(1f);
                view.Consume(hit, 1);
                Assert.That(view.LastHitWasKill, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static AuthoritativeShot Shot(uint entity, uint sequence) =>
            new AuthoritativeShot(entity, sequence, 100, 1, WeaponFireMode.Projectile,
                new FixVec3(Fix.FromInt(4), Fix.One, Fix.Zero), FixVec3.Forward,
                Fix.FromInt(20), Fix.FromInt(100), Fix.FromInt(10));
    }
}

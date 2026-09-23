using System;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Weapons;
using AFPS.Simulation.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace AFPS.Tests.EditMode
{
    public sealed class AuthoritativeShotResultCodecTests
    {
        [Test]
        public void Result_RoundTripsAuthoritativeShotHitAmmoAndHealth()
        {
            AuthoritativeShot shot = new AuthoritativeShot(3, 44, 900, 2, WeaponFireMode.Hitscan, new Vector3(1.234f, 2.345f, -3.456f), new Vector3(0.2f, -0.1f, 0.9746794f).normalized, 25f, 200f, 0f);
            AuthoritativeShotResult source = new AuthoritativeShotResult(shot, 17, true, 8, 75f, new Vector3(10.111f, 1.222f, -5.333f));
            byte[] packet = new byte[AuthoritativeShotResultCodec.PacketSize];

            Assert.That(AuthoritativeShotResultCodec.TrySerialize(source, 7, new ArraySegment<byte>(packet), out int written), Is.True);
            Assert.That(written, Is.EqualTo(packet.Length));
            Assert.That(AuthoritativeShotResultCodec.TryDeserialize(new ArraySegment<byte>(packet), out var header, out AuthoritativeShotResult decoded), Is.True);
            Assert.That(header.Sequence, Is.EqualTo(7));
            Assert.That(decoded.Shot.EntityId, Is.EqualTo(3));
            Assert.That(decoded.Shot.ShotSequence, Is.EqualTo(44));
            Assert.That(decoded.RemainingAmmo, Is.EqualTo(17));
            Assert.That(decoded.DidHit, Is.True);
            Assert.That(decoded.TargetEntityId, Is.EqualTo(8));
            Assert.That(decoded.TargetHealth, Is.EqualTo(75f).Within(0.01f));
            Assert.That(Vector3.Distance(decoded.HitPoint, source.HitPoint), Is.LessThan(0.002f));
            Assert.That(Vector3.Angle(decoded.Shot.Direction, source.Shot.Direction), Is.LessThan(0.01f));
        }

        [Test]
        public void Result_RejectsInconsistentHitTarget()
        {
            AuthoritativeShot shot = new AuthoritativeShot(1, 1, 1, 1, WeaponFireMode.Hitscan, Vector3.zero, Vector3.forward, 20f, 100f, 0f);
            AuthoritativeShotResult invalid = new AuthoritativeShotResult(shot, 29, false, 2, 100f, Vector3.forward * 100f);
            Assert.That(AuthoritativeShotResultCodec.TrySerialize(invalid, 1, new ArraySegment<byte>(new byte[AuthoritativeShotResultCodec.PacketSize]), out _), Is.False);
        }
    }
}

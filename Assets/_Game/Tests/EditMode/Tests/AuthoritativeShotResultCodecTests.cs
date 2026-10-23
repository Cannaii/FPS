using System;
using AFPS.Math;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.Weapons;
using AFPS.Simulation.Weapons;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class AuthoritativeShotResultCodecTests
    {
        [TestCase(0u)]
        [TestCase(uint.MaxValue)]
        public void Result_RoundTripsRawExtremesAndRewind(uint rewind)
        {
            AuthoritativeShot shot = new AuthoritativeShot(3, 44, 900, 2, WeaponFireMode.Projectile,
                new FixVec3(Fix.MinValue, Fix.MaxValue, Fix.Epsilon), FixVec3.Forward,
                Fix.Epsilon, Fix.MaxValue, Fix.Epsilon, rewind, preserveZeroRewindTick: true);
            AuthoritativeShotResult source = new AuthoritativeShotResult(shot, int.MaxValue, true, 8,
                Fix.MaxValue, new FixVec3(Fix.MaxValue, Fix.MinValue, Fix.FromRaw(655360001)));
            byte[] bytes = new byte[AuthoritativeShotResultCodec.PacketSize + 6];
            ArraySegment<byte> packet = new ArraySegment<byte>(bytes, 3, AuthoritativeShotResultCodec.PacketSize);
            Assert.That(AuthoritativeShotResultCodec.TrySerialize(source, 7, packet, out int count), Is.True);
            Assert.That(count, Is.EqualTo(92));
            Assert.That(bytes[3 + 4], Is.EqualTo(8));
            Assert.That(bytes[3 + PacketHeader.Size + 32], Is.EqualTo(0));
            Assert.That(bytes[3 + PacketHeader.Size + 35], Is.EqualTo(128)); // int.MinValue 小端最高字节。
            Assert.That(AuthoritativeShotResultCodec.TryDeserialize(packet, out PacketHeader header, out var decoded), Is.True);
            Assert.That(header.Sequence, Is.EqualTo(7));
            Assert.That(decoded.Shot.OriginFixed, Is.EqualTo(shot.OriginFixed));
            Assert.That(decoded.Shot.DirectionFixed, Is.EqualTo(shot.DirectionFixed));
            Assert.That(decoded.Shot.DamageFixed, Is.EqualTo(shot.DamageFixed));
            Assert.That(decoded.Shot.RangeFixed, Is.EqualTo(shot.RangeFixed));
            Assert.That(decoded.Shot.ProjectileSpeedFixed, Is.EqualTo(shot.ProjectileSpeedFixed));
            Assert.That(decoded.Shot.RewindTick, Is.EqualTo(rewind));
            Assert.That(decoded.HitPointFixed, Is.EqualTo(source.HitPointFixed));
            Assert.That(decoded.TargetHealthFixed, Is.EqualTo(source.TargetHealthFixed));
            Assert.That(decoded.RemainingAmmo, Is.EqualTo(int.MaxValue));
        }

        [TestCase(19, 2)]
        [TestCase(18, 127)]
        [TestCase(20, -1)]
        [TestCase(28, -1)]
        [TestCase(44, int.MinValue)]
        [TestCase(52, 0)]
        [TestCase(68, 0)]
        [TestCase(72, -1)]
        [TestCase(76, 1)]
        public void Result_RejectsInvalidFieldsWithoutPartialOutput(int offset, int value)
        {
            byte[] packet = Packet();
            int length = offset == 18 || offset == 19 ? 1 : 4;
            for (int index = 0; index < length; index++)
            {
                packet[PacketHeader.Size + offset + index] = unchecked((byte)((uint)value >> (index * 8)));
            }
            Assert.That(AuthoritativeShotResultCodec.TryDeserialize(new ArraySegment<byte>(packet), out _, out var result), Is.False);
            Assert.That(result.Shot.EntityId, Is.Zero);
        }

        [Test]
        public void Result_RejectsOldVersionTruncationAndShortDestination()
        {
            byte[] packet = Packet();
            packet[4] = 4;
            Assert.That(AuthoritativeShotResultCodec.TryDeserialize(new ArraySegment<byte>(packet), out _, out _), Is.False);
            packet[4] = 8;
            Assert.That(AuthoritativeShotResultCodec.TryDeserialize(new ArraySegment<byte>(packet, 0, packet.Length - 1), out _, out _), Is.False);
            Assert.That(AuthoritativeShotResultCodec.TrySerialize(Source(), 1,
                new ArraySegment<byte>(new byte[packet.Length - 1]), out int written), Is.False);
            Assert.That(written, Is.Zero);
        }

        [Test]
        public void Result_RejectsInconsistentHitTarget()
        {
            AuthoritativeShotResult source = Source();
            AuthoritativeShotResult invalid = new AuthoritativeShotResult(source.Shot, 29, false, 2, Fix.One, FixVec3.Zero);
            Assert.That(AuthoritativeShotResultCodec.TrySerialize(invalid, 1,
                new ArraySegment<byte>(new byte[AuthoritativeShotResultCodec.PacketSize]), out _), Is.False);
        }

        private static AuthoritativeShotResult Source()
        {
            AuthoritativeShot shot = new AuthoritativeShot(1, 1, 1, 1, WeaponFireMode.Hitscan,
                FixVec3.Zero, FixVec3.Forward, Fix.One, Fix.One, Fix.Zero);
            return new AuthoritativeShotResult(shot, 29, false, 0, Fix.Zero, FixVec3.Forward);
        }

        private static byte[] Packet()
        {
            byte[] packet = new byte[AuthoritativeShotResultCodec.PacketSize];
            Assert.That(AuthoritativeShotResultCodec.TrySerialize(Source(), 1, new ArraySegment<byte>(packet), out _), Is.True);
            return packet;
        }
    }
}

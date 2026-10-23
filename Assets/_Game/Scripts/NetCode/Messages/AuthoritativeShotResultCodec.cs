using System;
using AFPS.Math;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.Serialization;
using AFPS.NetCode.Weapons;
using AFPS.Simulation.Weapons;

namespace AFPS.NetCode.Messages
{
    /// <summary>v5：全部数值直接传 Q16.16 Raw，不再量化毫米/厘米或重归一化方向。</summary>
    public static class AuthoritativeShotResultCodec
    {
        // 小端：身份/两种 Tick 16B，武器/模式/标志 4B，弹药/目标/血量 12B，三向量 36B，三标量 12B。
        public const int PayloadSize = 80;
        public const int PacketSize = PacketHeader.Size + PayloadSize;

        public static bool TrySerialize(
            in AuthoritativeShotResult result,
            uint packetSequence,
            ArraySegment<byte> destination,
            out int bytesWritten)
        {
            bytesWritten = 0;
            AuthoritativeShot shot = result.Shot;
            if (!IsValid(result) || destination.Count < PacketSize)
            {
                return false;
            }
            PacketHeader header = new PacketHeader(NetworkMessageType.AuthoritativeShotResult, PayloadSize, packetSequence);
            if (!PacketHeaderCodec.TryWrite(header, destination))
            {
                return false;
            }
            PacketBufferWriter writer = new PacketBufferWriter(new ArraySegment<byte>(
                destination.Array, destination.Offset + PacketHeader.Size, PayloadSize));
            bool success = writer.TryWriteUInt32(shot.EntityId)
                && writer.TryWriteUInt32(shot.ShotSequence)
                && writer.TryWriteUInt32(shot.ServerTick)
                && writer.TryWriteUInt32(shot.RewindTick)
                && writer.TryWriteUInt16(shot.WeaponId)
                && writer.TryWriteByte((byte)shot.FireMode)
                && writer.TryWriteByte(result.DidHit ? (byte)1 : (byte)0)
                && writer.TryWriteInt32(result.RemainingAmmo)
                && writer.TryWriteUInt32(result.TargetEntityId)
                && writer.TryWriteInt32(result.TargetHealthFixed.Raw)
                && writer.TryWriteInt32(shot.OriginFixed.X.Raw)
                && writer.TryWriteInt32(shot.OriginFixed.Y.Raw)
                && writer.TryWriteInt32(shot.OriginFixed.Z.Raw)
                && writer.TryWriteInt32(shot.DirectionFixed.X.Raw)
                && writer.TryWriteInt32(shot.DirectionFixed.Y.Raw)
                && writer.TryWriteInt32(shot.DirectionFixed.Z.Raw)
                && writer.TryWriteInt32(result.HitPointFixed.X.Raw)
                && writer.TryWriteInt32(result.HitPointFixed.Y.Raw)
                && writer.TryWriteInt32(result.HitPointFixed.Z.Raw)
                && writer.TryWriteInt32(shot.DamageFixed.Raw)
                && writer.TryWriteInt32(shot.RangeFixed.Raw)
                && writer.TryWriteInt32(shot.ProjectileSpeedFixed.Raw);
            if (!success || writer.BytesWritten != PayloadSize)
            {
                return false;
            }
            bytesWritten = PacketSize;
            return true;
        }

        public static bool TryDeserialize(
            ArraySegment<byte> packet,
            out PacketHeader header,
            out AuthoritativeShotResult result)
        {
            header = default;
            result = default;
            if (!PacketHeaderCodec.TryRead(packet, out header)
                || header.MessageType != NetworkMessageType.AuthoritativeShotResult
                || header.PayloadLength != PayloadSize)
            {
                return false;
            }
            PacketBufferReader reader = new PacketBufferReader(new ArraySegment<byte>(
                packet.Array, packet.Offset + PacketHeader.Size, PayloadSize));
            if (!reader.TryReadUInt32(out uint entity)
                || !reader.TryReadUInt32(out uint sequence)
                || !reader.TryReadUInt32(out uint tick)
                || !reader.TryReadUInt32(out uint rewind)
                || !reader.TryReadUInt16(out ushort weapon)
                || !reader.TryReadByte(out byte mode)
                || !reader.TryReadByte(out byte flags)
                || !reader.TryReadInt32(out int ammo)
                || !reader.TryReadUInt32(out uint target)
                || !reader.TryReadInt32(out int health)
                || !reader.TryReadInt32(out int originX)
                || !reader.TryReadInt32(out int originY)
                || !reader.TryReadInt32(out int originZ)
                || !reader.TryReadInt32(out int directionX)
                || !reader.TryReadInt32(out int directionY)
                || !reader.TryReadInt32(out int directionZ)
                || !reader.TryReadInt32(out int pointX)
                || !reader.TryReadInt32(out int pointY)
                || !reader.TryReadInt32(out int pointZ)
                || !reader.TryReadInt32(out int damage)
                || !reader.TryReadInt32(out int range)
                || !reader.TryReadInt32(out int speed))
            {
                return false;
            }
            if (reader.BytesRemaining != 0 || flags > 1)
            {
                return false;
            }
            // 网络 Tick 零是合法回绕值，不能套用本地请求的“零表示当前帧”缺省规则。
            AuthoritativeShot shot = new AuthoritativeShot(entity, sequence, tick, weapon, (WeaponFireMode)mode,
                Vector(originX, originY, originZ), Vector(directionX, directionY, directionZ),
                Fix.FromRaw(damage), Fix.FromRaw(range), Fix.FromRaw(speed), rewind, preserveZeroRewindTick: true);
            AuthoritativeShotResult decoded = new AuthoritativeShotResult(
                shot, ammo, flags == 1, target, Fix.FromRaw(health), Vector(pointX, pointY, pointZ));
            if (!IsValid(decoded))
            {
                return false;
            }
            result = decoded;
            return true;
        }

        private static FixVec3 Vector(int x, int y, int z) =>
            new FixVec3(Fix.FromRaw(x), Fix.FromRaw(y), Fix.FromRaw(z));

        private static bool IsValid(in AuthoritativeShotResult result)
        {
            AuthoritativeShot shot = result.Shot;
            if (shot.EntityId == 0 || shot.ShotSequence == 0 || shot.WeaponId == 0
                || result.RemainingAmmo < 0 || result.TargetHealthFixed.Raw < 0
                || result.DidHit != (result.TargetEntityId != 0)
                || shot.DamageFixed.Raw <= 0 || shot.RangeFixed.Raw <= 0
                || (shot.FireMode != WeaponFireMode.Hitscan && shot.FireMode != WeaponFireMode.Projectile)
                || (shot.FireMode == WeaponFireMode.Hitscan && shot.ProjectileSpeedFixed.Raw != 0)
                || (shot.FireMode == WeaponFireMode.Projectile && shot.ProjectileSpeedFixed.Raw <= 0))
            {
                return false;
            }
            FixVec3 d = shot.DirectionFixed;
            // 先限制分量再求平方和，恶意 int 极值不能溢出 long；允许 LUT 的整数归一化误差。
            if (System.Math.Abs((long)d.X.Raw) > Fix.Scale
                || System.Math.Abs((long)d.Y.Raw) > Fix.Scale
                || System.Math.Abs((long)d.Z.Raw) > Fix.Scale)
            {
                return false;
            }
            long squared = (long)d.X.Raw * d.X.Raw + (long)d.Y.Raw * d.Y.Raw + (long)d.Z.Raw * d.Z.Raw;
            long unitSquared = Fix.Scale * Fix.Scale;
            return squared >= unitSquared - unitSquared / 100 && squared <= unitSquared + unitSquared / 100;
        }
    }
}
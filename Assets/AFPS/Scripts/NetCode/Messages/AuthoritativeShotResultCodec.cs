using System;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.Serialization;
using AFPS.NetCode.Weapons;
using AFPS.Simulation.Weapons;
using UnityEngine;

namespace AFPS.NetCode.Messages
{
    /// <summary>编码服务器已接受射击的弹药与命中裁决。</summary>
    public static class AuthoritativeShotResultCodec
    {
        public const int PayloadSize = 64;
        public const int PacketSize = PacketHeader.Size + PayloadSize;
        private const byte HitMask = 1 << 0;
        private const float ScalarResolution = 0.01f;
        private const float PositionResolution = 0.001f;
        private const float DirectionResolution = 1f / short.MaxValue;

        public static bool TrySerialize(in AuthoritativeShotResult result, uint packetSequence, ArraySegment<byte> destination, out int bytesWritten)
        {
            bytesWritten = 0;
            AuthoritativeShot shot = result.Shot;
            if (shot.EntityId == 0 || shot.ShotSequence == 0 || shot.WeaponId == 0 || result.RemainingAmmo < 0 || result.RemainingAmmo > ushort.MaxValue || result.TargetHealth < 0f || result.TargetHealth > ushort.MaxValue * ScalarResolution || destination.Count < PacketSize || !TryQuantizeVector(shot.Origin, PositionResolution, out int ox, out int oy, out int oz) || !TryQuantizeDirection(shot.Direction, out short dx, out short dy, out short dz) || !TryQuantizeVector(result.HitPoint, PositionResolution, out int hx, out int hy, out int hz) || !TryQuantizePositive(shot.Damage, ScalarResolution, ushort.MaxValue, out uint damage) || !TryQuantizePositive(shot.Range, PositionResolution, int.MaxValue, out uint range) || !TryQuantizeNonNegative(shot.ProjectileSpeed, PositionResolution, int.MaxValue, out uint projectileSpeed))
            {
                return false;
            }

            if ((shot.FireMode != WeaponFireMode.Hitscan && shot.FireMode != WeaponFireMode.Projectile) || (result.DidHit && result.TargetEntityId == 0) || (!result.DidHit && result.TargetEntityId != 0))
            {
                return false;
            }

            ushort health = (ushort)Math.Round(result.TargetHealth / ScalarResolution);
            PacketHeader header = new PacketHeader(NetworkMessageType.AuthoritativeShotResult, PayloadSize, packetSequence);
            if (!PacketHeaderCodec.TryWrite(header, destination))
            {
                return false;
            }

            PacketBufferWriter writer = new PacketBufferWriter(new ArraySegment<byte>(destination.Array, destination.Offset + PacketHeader.Size, PayloadSize));
            bool success = writer.TryWriteUInt32(shot.EntityId) && writer.TryWriteUInt32(shot.ShotSequence) && writer.TryWriteUInt32(shot.ServerTick) && writer.TryWriteUInt16(shot.WeaponId) && writer.TryWriteByte((byte)shot.FireMode) && writer.TryWriteUInt16((ushort)result.RemainingAmmo) && writer.TryWriteByte(result.DidHit ? HitMask : (byte)0) && writer.TryWriteUInt32(result.TargetEntityId) && writer.TryWriteUInt16(health) && writer.TryWriteInt32(ox) && writer.TryWriteInt32(oy) && writer.TryWriteInt32(oz) && writer.TryWriteInt16(dx) && writer.TryWriteInt16(dy) && writer.TryWriteInt16(dz) && writer.TryWriteInt32(hx) && writer.TryWriteInt32(hy) && writer.TryWriteInt32(hz) && writer.TryWriteUInt16((ushort)damage) && writer.TryWriteInt32((int)range) && writer.TryWriteInt32((int)projectileSpeed);
            if (!success || writer.BytesWritten != PayloadSize)
            {
                return false;
            }

            bytesWritten = PacketSize;
            return true;
        }

        public static bool TryDeserialize(ArraySegment<byte> packet, out PacketHeader header, out AuthoritativeShotResult result)
        {
            header = default;
            result = default;
            if (!PacketHeaderCodec.TryRead(packet, out header) || header.MessageType != NetworkMessageType.AuthoritativeShotResult || header.PayloadLength != PayloadSize)
            {
                return false;
            }

            PacketBufferReader reader = new PacketBufferReader(new ArraySegment<byte>(packet.Array, packet.Offset + PacketHeader.Size, PayloadSize));
            if (!reader.TryReadUInt32(out uint entityId) || !reader.TryReadUInt32(out uint shotSequence) || !reader.TryReadUInt32(out uint serverTick) || !reader.TryReadUInt16(out ushort weaponId) || !reader.TryReadByte(out byte fireModeRaw) || !reader.TryReadUInt16(out ushort ammo) || !reader.TryReadByte(out byte flags) || !reader.TryReadUInt32(out uint targetId) || !reader.TryReadUInt16(out ushort health) || !reader.TryReadInt32(out int ox) || !reader.TryReadInt32(out int oy) || !reader.TryReadInt32(out int oz) || !reader.TryReadInt16(out short dx) || !reader.TryReadInt16(out short dy) || !reader.TryReadInt16(out short dz) || !reader.TryReadInt32(out int hx) || !reader.TryReadInt32(out int hy) || !reader.TryReadInt32(out int hz) || !reader.TryReadUInt16(out ushort damage) || !reader.TryReadInt32(out int range) || !reader.TryReadInt32(out int projectileSpeed))
            {
                return false;
            }

            WeaponFireMode fireMode = (WeaponFireMode)fireModeRaw;
            bool didHit = (flags & HitMask) != 0;
            Vector3 direction = new Vector3(dx * DirectionResolution, dy * DirectionResolution, dz * DirectionResolution);
            if (reader.BytesRemaining != 0 || entityId == 0 || shotSequence == 0 || weaponId == 0 || (fireMode != WeaponFireMode.Hitscan && fireMode != WeaponFireMode.Projectile) || (flags & ~HitMask) != 0 || didHit != (targetId != 0) || range <= 0 || projectileSpeed < 0 || direction.sqrMagnitude < 0.99f || direction.sqrMagnitude > 1.01f)
            {
                return false;
            }

            direction.Normalize();
            Vector3 origin = new Vector3(ox, oy, oz) * PositionResolution;
            Vector3 hitPoint = new Vector3(hx, hy, hz) * PositionResolution;
            AuthoritativeShot shot = new AuthoritativeShot(entityId, shotSequence, serverTick, weaponId, fireMode, origin, direction, damage * ScalarResolution, range * PositionResolution, projectileSpeed * PositionResolution);
            result = new AuthoritativeShotResult(shot, ammo, didHit, targetId, health * ScalarResolution, hitPoint);
            return true;
        }

        private static bool TryQuantizeVector(Vector3 value, float resolution, out int x, out int y, out int z)
        {
            return TryQuantizeSigned(value.x, resolution, out x) & TryQuantizeSigned(value.y, resolution, out y) & TryQuantizeSigned(value.z, resolution, out z);
        }

        private static bool TryQuantizeDirection(Vector3 direction, out short x, out short y, out short z)
        {
            x = y = z = 0;
            if (float.IsNaN(direction.x) || float.IsNaN(direction.y) || float.IsNaN(direction.z) || float.IsInfinity(direction.x) || float.IsInfinity(direction.y) || float.IsInfinity(direction.z) || direction.sqrMagnitude < 0.99f || direction.sqrMagnitude > 1.01f)
            {
                return false;
            }

            direction.Normalize();
            x = (short)Math.Round(Mathf.Clamp(direction.x, -1f, 1f) * short.MaxValue);
            y = (short)Math.Round(Mathf.Clamp(direction.y, -1f, 1f) * short.MaxValue);
            z = (short)Math.Round(Mathf.Clamp(direction.z, -1f, 1f) * short.MaxValue);
            return true;
        }

        private static bool TryQuantizeSigned(float value, float resolution, out int quantized)
        {
            double rounded = Math.Round(value / resolution);
            bool valid = !float.IsNaN(value) && !float.IsInfinity(value) && rounded >= int.MinValue && rounded <= int.MaxValue;
            quantized = valid ? (int)rounded : 0;
            return valid;
        }

        private static bool TryQuantizePositive(float value, float resolution, int maximum, out uint quantized)
        {
            bool valid = TryQuantizeNonNegative(value, resolution, maximum, out quantized) && quantized > 0;
            return valid;
        }

        private static bool TryQuantizeNonNegative(float value, float resolution, int maximum, out uint quantized)
        {
            double rounded = Math.Round(value / resolution);
            bool valid = !float.IsNaN(value) && !float.IsInfinity(value) && rounded >= 0 && rounded <= maximum;
            quantized = valid ? (uint)rounded : 0;
            return valid;
        }
    }
}

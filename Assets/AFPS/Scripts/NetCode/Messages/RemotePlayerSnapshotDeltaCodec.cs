using System;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.Serialization;
using AFPS.NetCode.SnapshotInterpolation;
using UnityEngine;

namespace AFPS.NetCode.Messages
{
    /// <summary>相对最近周期性全量基线，仅编码发生变化的远端状态字段。</summary>
    public static class RemotePlayerSnapshotDeltaCodec
    {
        public const int FixedPayloadSize = 13;
        public const int MaximumPacketSize = PacketHeader.Size + FixedPayloadSize + 23;
        private const byte PositionMask = 1 << 0;
        private const byte VelocityMask = 1 << 1;
        private const byte RotationMask = 1 << 2;
        private const byte TeleportMask = 1 << 3;
        private const byte KnownMask = PositionMask | VelocityMask | RotationMask | TeleportMask;

        public static bool TrySerialize(in RemotePlayerSnapshot current, in RemotePlayerSnapshot baseline, uint sequence, ArraySegment<byte> destination, out int bytesWritten)
        {
            bytesWritten = 0;
            if (current.EntityId == 0 || current.EntityId != baseline.EntityId || destination.Count < MaximumPacketSize) return false;
            if (!TryQuantizeVector(current.Position, AuthoritativePlayerStateCodec.PositionResolution, int.MinValue, int.MaxValue, out int cpx, out int cpy, out int cpz) || !TryQuantizeVector(baseline.Position, AuthoritativePlayerStateCodec.PositionResolution, int.MinValue, int.MaxValue, out int bpx, out int bpy, out int bpz) || !TryQuantizeVector(current.Velocity, AuthoritativePlayerStateCodec.VelocityResolution, short.MinValue, short.MaxValue, out int cvx, out int cvy, out int cvz) || !TryQuantizeVector(baseline.Velocity, AuthoritativePlayerStateCodec.VelocityResolution, short.MinValue, short.MaxValue, out int bvx, out int bvy, out int bvz)) return false;
            QuantizeRotation(current.Rotation, out ushort currentYaw, out short currentPitch);
            QuantizeRotation(baseline.Rotation, out ushort baselineYaw, out short baselinePitch);
            byte mask = 0;
            if (cpx != bpx || cpy != bpy || cpz != bpz) mask |= PositionMask;
            if (cvx != bvx || cvy != bvy || cvz != bvz) mask |= VelocityMask;
            if (currentYaw != baselineYaw || currentPitch != baselinePitch) mask |= RotationMask;
            if (current.IsTeleport != baseline.IsTeleport) mask |= TeleportMask;
            int payloadSize = FixedPayloadSize + ((mask & PositionMask) != 0 ? 12 : 0) + ((mask & VelocityMask) != 0 ? 6 : 0) + ((mask & RotationMask) != 0 ? 4 : 0) + ((mask & TeleportMask) != 0 ? 1 : 0);
            if (!PacketHeaderCodec.TryWrite(new PacketHeader(NetworkMessageType.RemotePlayerSnapshotDelta, (ushort)payloadSize, sequence), destination)) return false;
            PacketBufferWriter writer = new PacketBufferWriter(new ArraySegment<byte>(destination.Array, destination.Offset + PacketHeader.Size, payloadSize));
            if (!writer.TryWriteUInt32(current.EntityId) || !writer.TryWriteUInt32(current.ServerTick) || !writer.TryWriteUInt32(baseline.ServerTick) || !writer.TryWriteByte(mask)) return false;
            if ((mask & PositionMask) != 0 && (!WritePosition(ref writer, current.Position))) return false;
            if ((mask & VelocityMask) != 0 && (!WriteVelocity(ref writer, current.Velocity))) return false;
            if ((mask & RotationMask) != 0 && (!WriteRotation(ref writer, current.Rotation))) return false;
            if ((mask & TeleportMask) != 0 && !writer.TryWriteByte(current.IsTeleport ? (byte)1 : (byte)0)) return false;
            bytesWritten = PacketHeader.Size + writer.BytesWritten;
            return writer.BytesWritten == payloadSize;
        }

        public static bool TryDeserialize(ArraySegment<byte> packet, in RemotePlayerSnapshot baseline, out PacketHeader header, out RemotePlayerSnapshot snapshot)
        {
            header = default;
            snapshot = default;
            if (!PacketHeaderCodec.TryRead(packet, out header) || header.MessageType != NetworkMessageType.RemotePlayerSnapshotDelta || header.PayloadLength < FixedPayloadSize) return false;
            PacketBufferReader reader = new PacketBufferReader(new ArraySegment<byte>(packet.Array, packet.Offset + PacketHeader.Size, header.PayloadLength));
            if (!reader.TryReadUInt32(out uint entityId) || !reader.TryReadUInt32(out uint serverTick) || !reader.TryReadUInt32(out uint baselineTick) || !reader.TryReadByte(out byte mask) || entityId != baseline.EntityId || baselineTick != baseline.ServerTick || (mask & ~KnownMask) != 0) return false;
            Vector3 position = baseline.Position;
            Vector3 velocity = baseline.Velocity;
            Quaternion rotation = baseline.Rotation;
            bool teleport = baseline.IsTeleport;
            if ((mask & PositionMask) != 0 && !ReadPosition(ref reader, out position)) return false;
            if ((mask & VelocityMask) != 0 && !ReadVelocity(ref reader, out velocity)) return false;
            if ((mask & RotationMask) != 0 && !ReadRotation(ref reader, out rotation)) return false;
            if ((mask & TeleportMask) != 0)
            {
                if (!reader.TryReadByte(out byte rawTeleport) || rawTeleport > 1) return false;
                teleport = rawTeleport != 0;
            }
            if (reader.BytesRemaining != 0) return false;
            snapshot = new RemotePlayerSnapshot(entityId, serverTick, position, rotation, velocity, teleport);
            return true;
        }

        private static bool WritePosition(ref PacketBufferWriter writer, Vector3 value) => TryQuantize(value.x, AuthoritativePlayerStateCodec.PositionResolution, int.MinValue, int.MaxValue, out int x) && TryQuantize(value.y, AuthoritativePlayerStateCodec.PositionResolution, int.MinValue, int.MaxValue, out int y) && TryQuantize(value.z, AuthoritativePlayerStateCodec.PositionResolution, int.MinValue, int.MaxValue, out int z) && writer.TryWriteInt32(x) && writer.TryWriteInt32(y) && writer.TryWriteInt32(z);
        private static bool WriteVelocity(ref PacketBufferWriter writer, Vector3 value) => TryQuantize(value.x, AuthoritativePlayerStateCodec.VelocityResolution, short.MinValue, short.MaxValue, out int x) && TryQuantize(value.y, AuthoritativePlayerStateCodec.VelocityResolution, short.MinValue, short.MaxValue, out int y) && TryQuantize(value.z, AuthoritativePlayerStateCodec.VelocityResolution, short.MinValue, short.MaxValue, out int z) && writer.TryWriteInt16((short)x) && writer.TryWriteInt16((short)y) && writer.TryWriteInt16((short)z);
        private static bool WriteRotation(ref PacketBufferWriter writer, Quaternion value) { QuantizeRotation(value, out ushort yaw, out short pitch); return writer.TryWriteUInt16(yaw) && writer.TryWriteInt16(pitch); }
        private static bool ReadPosition(ref PacketBufferReader reader, out Vector3 value) { value = default; if (!reader.TryReadInt32(out int x) || !reader.TryReadInt32(out int y) || !reader.TryReadInt32(out int z)) return false; value = new Vector3(x, y, z) * AuthoritativePlayerStateCodec.PositionResolution; return true; }
        private static bool ReadVelocity(ref PacketBufferReader reader, out Vector3 value) { value = default; if (!reader.TryReadInt16(out short x) || !reader.TryReadInt16(out short y) || !reader.TryReadInt16(out short z)) return false; value = new Vector3(x, y, z) * AuthoritativePlayerStateCodec.VelocityResolution; return true; }
        private static bool ReadRotation(ref PacketBufferReader reader, out Quaternion value) { value = default; if (!reader.TryReadUInt16(out ushort yaw) || !reader.TryReadInt16(out short pitch)) return false; value = Quaternion.Euler(pitch * AuthoritativePlayerStateCodec.LookAngleResolution, yaw * AuthoritativePlayerStateCodec.LookAngleResolution, 0f); return true; }
        private static bool TryQuantize(float value, float resolution, int minimum, int maximum, out int quantized) { double rounded = Math.Round(value / resolution); bool valid = !float.IsNaN(value) && !float.IsInfinity(value) && rounded >= minimum && rounded <= maximum; quantized = valid ? (int)rounded : 0; return valid; }
        private static bool TryQuantizeVector(Vector3 value, float resolution, int minimum, int maximum, out int x, out int y, out int z) => TryQuantize(value.x, resolution, minimum, maximum, out x) & TryQuantize(value.y, resolution, minimum, maximum, out y) & TryQuantize(value.z, resolution, minimum, maximum, out z);
        private static void QuantizeRotation(Quaternion value, out ushort yaw, out short pitch) { Vector3 e = value.eulerAngles; float signedPitch = e.x > 180f ? e.x - 360f : e.x; yaw = (ushort)Math.Round((e.y % 360f) / AuthoritativePlayerStateCodec.LookAngleResolution); pitch = (short)Math.Round(Mathf.Clamp(signedPitch, -89.9f, 89.9f) / AuthoritativePlayerStateCodec.LookAngleResolution); }
    }
}

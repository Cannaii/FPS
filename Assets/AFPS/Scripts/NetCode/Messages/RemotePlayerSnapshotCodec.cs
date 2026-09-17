using System;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.Serialization;
using AFPS.NetCode.SnapshotInterpolation;
using UnityEngine;

namespace AFPS.NetCode.Messages
{
    /// <summary>
    /// 将服务器世界中的远端玩家快照编码为固定布局数据包。
    /// 位置、速度和观察角使用与本地权威状态一致的量化精度。
    /// </summary>
    public static class RemotePlayerSnapshotCodec
    {
        public const int PayloadSize = 31;
        public const int PacketSize = PacketHeader.Size + PayloadSize;
        private const byte TeleportMask = 1 << 0;

        public static bool TrySerialize(in RemotePlayerSnapshot snapshot, uint sequence, ArraySegment<byte> destination, out int bytesWritten)
        {
            bytesWritten = 0;
            if (snapshot.EntityId == 0 || destination.Count < PacketSize || !TryQuantizePosition(snapshot.Position.x, out int px) || !TryQuantizePosition(snapshot.Position.y, out int py) || !TryQuantizePosition(snapshot.Position.z, out int pz) || !TryQuantizeVelocity(snapshot.Velocity.x, out short vx) || !TryQuantizeVelocity(snapshot.Velocity.y, out short vy) || !TryQuantizeVelocity(snapshot.Velocity.z, out short vz))
            {
                return false;
            }

            Vector3 euler = snapshot.Rotation.eulerAngles;
            float pitch = euler.x > 180f ? euler.x - 360f : euler.x;
            ushort yaw = QuantizeYaw(euler.y);
            short pitchValue = QuantizePitch(pitch);
            PacketHeader header = new PacketHeader(NetworkMessageType.RemotePlayerSnapshot, PayloadSize, sequence);
            if (!PacketHeaderCodec.TryWrite(header, destination))
            {
                return false;
            }

            PacketBufferWriter writer = new PacketBufferWriter(new ArraySegment<byte>(destination.Array, destination.Offset + PacketHeader.Size, PayloadSize));
            byte flags = snapshot.IsTeleport ? TeleportMask : (byte)0;
            bool success = writer.TryWriteUInt32(snapshot.EntityId) && writer.TryWriteUInt32(snapshot.ServerTick) && writer.TryWriteInt32(px) && writer.TryWriteInt32(py) && writer.TryWriteInt32(pz) && writer.TryWriteInt16(vx) && writer.TryWriteInt16(vy) && writer.TryWriteInt16(vz) && writer.TryWriteUInt16(yaw) && writer.TryWriteInt16(pitchValue) && writer.TryWriteByte(flags);
            if (!success)
            {
                return false;
            }

            bytesWritten = PacketSize;
            return true;
        }

        public static bool TryDeserialize(ArraySegment<byte> packet, out PacketHeader header, out RemotePlayerSnapshot snapshot)
        {
            header = default;
            snapshot = default;
            if (!PacketHeaderCodec.TryRead(packet, out header) || header.MessageType != NetworkMessageType.RemotePlayerSnapshot || header.PayloadLength != PayloadSize)
            {
                return false;
            }

            PacketBufferReader reader = new PacketBufferReader(new ArraySegment<byte>(packet.Array, packet.Offset + PacketHeader.Size, PayloadSize));
            if (!reader.TryReadUInt32(out uint entityId) || !reader.TryReadUInt32(out uint serverTick) || !reader.TryReadInt32(out int px) || !reader.TryReadInt32(out int py) || !reader.TryReadInt32(out int pz) || !reader.TryReadInt16(out short vx) || !reader.TryReadInt16(out short vy) || !reader.TryReadInt16(out short vz) || !reader.TryReadUInt16(out ushort yaw) || !reader.TryReadInt16(out short pitch) || !reader.TryReadByte(out byte flags))
            {
                return false;
            }

            if (entityId == 0 || reader.BytesRemaining != 0 || (flags & ~TeleportMask) != 0)
            {
                return false;
            }

            Vector3 position = new Vector3(px * AuthoritativePlayerStateCodec.PositionResolution, py * AuthoritativePlayerStateCodec.PositionResolution, pz * AuthoritativePlayerStateCodec.PositionResolution);
            Vector3 velocity = new Vector3(vx * AuthoritativePlayerStateCodec.VelocityResolution, vy * AuthoritativePlayerStateCodec.VelocityResolution, vz * AuthoritativePlayerStateCodec.VelocityResolution);
            Quaternion rotation = Quaternion.Euler(pitch * AuthoritativePlayerStateCodec.LookAngleResolution, yaw * AuthoritativePlayerStateCodec.LookAngleResolution, 0f);
            snapshot = new RemotePlayerSnapshot(entityId, serverTick, position, rotation, velocity, (flags & TeleportMask) != 0);
            return true;
        }

        private static bool TryQuantizePosition(float value, out int quantized)
        {
            double rounded = Math.Round(value / AuthoritativePlayerStateCodec.PositionResolution);
            bool valid = !float.IsNaN(value) && !float.IsInfinity(value) && rounded >= int.MinValue && rounded <= int.MaxValue;
            quantized = valid ? (int)rounded : 0;
            return valid;
        }

        private static bool TryQuantizeVelocity(float value, out short quantized)
        {
            double rounded = Math.Round(value / AuthoritativePlayerStateCodec.VelocityResolution);
            bool valid = !float.IsNaN(value) && !float.IsInfinity(value) && rounded >= short.MinValue && rounded <= short.MaxValue;
            quantized = valid ? (short)rounded : (short)0;
            return valid;
        }

        private static ushort QuantizeYaw(float value)
        {
            value %= 360f;
            if (value < 0f)
            {
                value += 360f;
            }

            return (ushort)Math.Round(value / AuthoritativePlayerStateCodec.LookAngleResolution);
        }

        private static short QuantizePitch(float value) => (short)Math.Round(Mathf.Clamp(value, -89.9f, 89.9f) / AuthoritativePlayerStateCodec.LookAngleResolution);
    }
}

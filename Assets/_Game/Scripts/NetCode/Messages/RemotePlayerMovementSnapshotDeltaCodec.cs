using System;
using AFPS.Math;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.Serialization;
using AFPS.NetCode.SnapshotInterpolation;

namespace AFPS.NetCode.Messages
{
    /// <summary>相对最近全量基线，按 Raw 比较并传输变化字段；不会损失一格内的变化。</summary>
    public static class RemotePlayerMovementSnapshotDeltaCodec
    {
        public const int FixedPayloadSize = 13;
        public const int MaximumPacketSize = PacketHeader.Size + FixedPayloadSize + 45;
        private const byte PositionMask = 1 << 0;
        private const byte VelocityMask = 1 << 1;
        private const byte RotationMask = 1 << 2;
        private const byte StateMask = 1 << 3;
        private const byte GrapplePointMask = 1 << 4;
        private const byte KnownMask = PositionMask | VelocityMask | RotationMask | StateMask | GrapplePointMask;

        public static bool TrySerialize(
            in RemotePlayerMovementSnapshot current,
            in RemotePlayerMovementSnapshot baseline,
            uint sequence,
            ArraySegment<byte> destination,
            out int bytesWritten)
        {
            bytesWritten = 0;
            if (!current.IsValid || !baseline.IsValid || current.EntityId != baseline.EntityId)
            {
                return false;
            }

            byte mask = 0;
            if (current.PositionFixed != baseline.PositionFixed)
            {
                mask |= PositionMask;
            }
            if (current.VelocityFixed != baseline.VelocityFixed)
            {
                mask |= VelocityMask;
            }
            if (current.YawFixedDegrees != baseline.YawFixedDegrees
                || current.PitchFixedDegrees != baseline.PitchFixedDegrees)
            {
                mask |= RotationMask;
            }
            if (current.StateFlags != baseline.StateFlags)
            {
                mask |= StateMask;
            }
            if (current.GrapplePointFixed != baseline.GrapplePointFixed)
            {
                mask |= GrapplePointMask;
            }

            int payloadSize = FixedPayloadSize
                + ((mask & PositionMask) != 0 ? 12 : 0)
                + ((mask & VelocityMask) != 0 ? 12 : 0)
                + ((mask & RotationMask) != 0 ? 8 : 0)
                + ((mask & StateMask) != 0 ? 1 : 0)
                + ((mask & GrapplePointMask) != 0 ? 12 : 0);
            if (destination.Count < PacketHeader.Size + payloadSize
                || !PacketHeaderCodec.TryWrite(new PacketHeader(
                    NetworkMessageType.RemotePlayerSnapshotDelta, (ushort)payloadSize, sequence), destination))
            {
                return false;
            }

            var writer = new PacketBufferWriter(new ArraySegment<byte>(
                destination.Array, destination.Offset + PacketHeader.Size, payloadSize));
            if (!writer.TryWriteUInt32(current.EntityId)
                || !writer.TryWriteUInt32(current.ServerTick)
                || !writer.TryWriteUInt32(baseline.ServerTick)
                || !writer.TryWriteByte(mask))
            {
                return false;
            }

            // 不写差值，直接写变化组的 Raw；极值之间切换也不会因做减法而溢出。
            if ((mask & PositionMask) != 0
                && !RemotePlayerMovementSnapshotCodec.WriteVector(ref writer, current.PositionFixed))
            {
                return false;
            }
            if ((mask & VelocityMask) != 0
                && !RemotePlayerMovementSnapshotCodec.WriteVector(ref writer, current.VelocityFixed))
            {
                return false;
            }
            if ((mask & RotationMask) != 0
                && (!writer.TryWriteInt32(current.YawFixedDegrees.Raw)
                    || !writer.TryWriteInt32(current.PitchFixedDegrees.Raw)))
            {
                return false;
            }
            if ((mask & StateMask) != 0
                && !writer.TryWriteByte(current.StateFlags))
            {
                return false;
            }
            if ((mask & GrapplePointMask) != 0
                && !RemotePlayerMovementSnapshotCodec.WriteVector(ref writer, current.GrapplePointFixed))
            {
                return false;
            }

            bytesWritten = PacketHeader.Size + writer.BytesWritten;
            return writer.BytesWritten == payloadSize;
        }

        public static bool TryDeserialize(
            ArraySegment<byte> packet,
            in RemotePlayerMovementSnapshot baseline,
            out PacketHeader header,
            out RemotePlayerMovementSnapshot snapshot)
        {
            header = default;
            snapshot = default;
            if (!baseline.IsValid || !PacketHeaderCodec.TryRead(packet, out header)
                || header.MessageType != NetworkMessageType.RemotePlayerSnapshotDelta
                || header.PayloadLength < FixedPayloadSize
                || header.PayloadLength > MaximumPacketSize - PacketHeader.Size)
            {
                return false;
            }

            var reader = new PacketBufferReader(new ArraySegment<byte>(
                packet.Array, packet.Offset + PacketHeader.Size, header.PayloadLength));
            if (!reader.TryReadUInt32(out uint entityId)
                || !reader.TryReadUInt32(out uint serverTick)
                || !reader.TryReadUInt32(out uint baselineTick)
                || !reader.TryReadByte(out byte mask)
                || entityId != baseline.EntityId || baselineTick != baseline.ServerTick
                || (mask & ~KnownMask) != 0)
            {
                return false;
            }

            FixVec3 position = baseline.PositionFixed;
            FixVec3 velocity = baseline.VelocityFixed;
            int yaw = baseline.YawFixedDegrees.Raw;
            int pitch = baseline.PitchFixedDegrees.Raw;
            bool teleport = baseline.IsTeleport;
            bool grounded = baseline.IsGrounded;
            bool crouched = baseline.IsCrouched;
            bool grappling = baseline.IsGrappling;
            var grapplePoint = baseline.GrapplePointFixed;
            if ((mask & PositionMask) != 0
                && !RemotePlayerMovementSnapshotCodec.ReadVector(ref reader, out position))
            {
                return false;
            }
            if ((mask & VelocityMask) != 0
                && !RemotePlayerMovementSnapshotCodec.ReadVector(ref reader, out velocity))
            {
                return false;
            }
            if ((mask & RotationMask) != 0
                && (!reader.TryReadInt32(out yaw) || !reader.TryReadInt32(out pitch)))
            {
                return false;
            }
            if ((mask & StateMask) != 0)
            {
                if (!reader.TryReadByte(out byte flags) || flags > 15)
                {
                    return false;
                }
                teleport = (flags & 1) != 0;
                grounded = (flags & 2) != 0;
                crouched = (flags & 4) != 0;
                grappling = (flags & 8) != 0;
            }
            if ((mask & GrapplePointMask) != 0
                && !RemotePlayerMovementSnapshotCodec.ReadVector(ref reader, out grapplePoint))
            {
                return false;
            }

            var candidate = new RemotePlayerMovementSnapshot(entityId, serverTick, position,
                Fix.FromRaw(yaw), Fix.FromRaw(pitch), velocity, teleport, grounded, crouched, grappling, grapplePoint);
            // 解码在本地候选上完成；非法尾部或角域不能留下一个有效前缀给调用方。
            if (reader.BytesRemaining != 0 || !candidate.IsValid)
            {
                return false;
            }

            snapshot = candidate;
            return true;
        }
    }
}

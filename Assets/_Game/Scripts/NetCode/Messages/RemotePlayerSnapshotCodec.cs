using System;
using AFPS.Math;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.Serialization;
using AFPS.NetCode.SnapshotInterpolation;

namespace AFPS.NetCode.Messages
{
    /// <summary>全量快照：小端 Entity/Tick + 位置/速度/角度 Raw + 传送/落地标志，无浮点量化。</summary>
    public static class RemotePlayerSnapshotCodec
    {
        public const int PayloadSize = 41;
        public const int PacketSize = PacketHeader.Size + PayloadSize;

        public static bool TrySerialize(
            in RemotePlayerSnapshot snapshot,
            uint sequence,
            ArraySegment<byte> destination,
            out int bytesWritten)
        {
            bytesWritten = 0;
            if (!snapshot.IsValid || destination.Count < PacketSize)
            {
                return false;
            }

            var header = new PacketHeader(NetworkMessageType.RemotePlayerSnapshot, PayloadSize, sequence);
            if (!PacketHeaderCodec.TryWrite(header, destination))
            {
                return false;
            }

            var writer = new PacketBufferWriter(new ArraySegment<byte>(
                destination.Array, destination.Offset + PacketHeader.Size, PayloadSize));
            bool success = writer.TryWriteUInt32(snapshot.EntityId)
                && writer.TryWriteUInt32(snapshot.ServerTick)
                && WriteVector(ref writer, snapshot.PositionFixed)
                && WriteVector(ref writer, snapshot.VelocityFixed)
                && writer.TryWriteInt32(snapshot.YawFixedDegrees.Raw)
                && writer.TryWriteInt32(snapshot.PitchFixedDegrees.Raw)
                && writer.TryWriteByte(snapshot.StateFlags);
            bytesWritten = success ? PacketSize : 0;
            return success;
        }

        public static bool TryDeserialize(
            ArraySegment<byte> packet,
            out PacketHeader header,
            out RemotePlayerSnapshot snapshot)
        {
            header = default;
            snapshot = default;
            if (!PacketHeaderCodec.TryRead(packet, out header)
                || header.MessageType != NetworkMessageType.RemotePlayerSnapshot
                || header.PayloadLength != PayloadSize)
            {
                return false;
            }

            var reader = new PacketBufferReader(new ArraySegment<byte>(
                packet.Array, packet.Offset + PacketHeader.Size, PayloadSize));
            if (!reader.TryReadUInt32(out uint entityId)
                || !reader.TryReadUInt32(out uint serverTick)
                || !ReadVector(ref reader, out FixVec3 position)
                || !ReadVector(ref reader, out FixVec3 velocity)
                || !reader.TryReadInt32(out int yaw)
                || !reader.TryReadInt32(out int pitch)
                || !reader.TryReadByte(out byte flags)
                || flags > 7 || reader.BytesRemaining != 0)
            {
                return false;
            }

            var candidate = new RemotePlayerSnapshot(entityId, serverTick, position,
                Fix.FromRaw(yaw), Fix.FromRaw(pitch), velocity, (flags & 1) != 0, (flags & 2) != 0, (flags & 4) != 0);
            if (!candidate.IsValid)
            {
                return false;
            }

            snapshot = candidate;
            return true;
        }

        internal static bool WriteVector(ref PacketBufferWriter writer, in FixVec3 value) =>
            writer.TryWriteInt32(value.X.Raw)
            && writer.TryWriteInt32(value.Y.Raw)
            && writer.TryWriteInt32(value.Z.Raw);

        internal static bool ReadVector(ref PacketBufferReader reader, out FixVec3 value)
        {
            value = default;
            if (!reader.TryReadInt32(out int x)
                || !reader.TryReadInt32(out int y)
                || !reader.TryReadInt32(out int z))
            {
                return false;
            }

            value = new FixVec3(Fix.FromRaw(x), Fix.FromRaw(y), Fix.FromRaw(z));
            return true;
        }
    }
}

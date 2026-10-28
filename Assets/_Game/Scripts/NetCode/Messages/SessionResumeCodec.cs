using System;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.Serialization;
using AFPS.NetCode.Sessions;
using AFPS.Simulation.World;

namespace AFPS.NetCode.Messages
{
    /// <summary>
    /// 凭据下发与恢复请求。请求不接受客户端自报entityId，必须由服务器凭据索引解析。
    /// 编解码成功不代表准入或授权；管理器接线前不能据此直接迁移会话。
    /// </summary>
    public static class SessionResumeCodec
    {
        public const int CredentialSize = 32;
        public const int GrantPayloadSize = 4 + CredentialSize;
        public const int RequestPayloadSize = SimulationHandshakeCodec.IdentitySize + CredentialSize;
        public const int GrantPacketSize = PacketHeader.Size + GrantPayloadSize;
        public const int RequestPacketSize = PacketHeader.Size + RequestPayloadSize;

        /// <param name="attempt">非零本次连接尝试号，使用包头Sequence；重试沿用，新的连接尝试必须改变。</param>
        public static bool TryWriteGrant(
            uint entityId,
            SessionResumeCredential credential,
            uint attempt,
            ArraySegment<byte> destination,
            out int bytesWritten)
        {
            bytesWritten = 0;
            if (entityId == 0 || !credential.IsValid || attempt == 0 || destination.Count < GrantPacketSize
                || !PacketHeaderCodec.TryWrite(new PacketHeader(
                    NetworkMessageType.SessionResumeGrant, GrantPayloadSize, attempt), destination))
            {
                return false;
            }
            var writer = PayloadWriter(destination, GrantPayloadSize);
            if (!writer.TryWriteUInt32(entityId) || !WriteCredential(ref writer, credential))
            {
                return false;
            }
            bytesWritten = GrantPacketSize;
            return true;
        }

        public static bool TryReadGrant(
            ArraySegment<byte> packet,
            out uint attempt,
            out uint entityId,
            out SessionResumeCredential credential)
        {
            attempt = 0;
            entityId = 0;
            credential = default;
            if (!TryReadHeader(packet, NetworkMessageType.SessionResumeGrant, GrantPayloadSize, out var header))
            {
                return false;
            }
            var reader = PayloadReader(packet, GrantPayloadSize);
            if (!reader.TryReadUInt32(out uint decodedEntityId) || decodedEntityId == 0
                || !ReadCredential(ref reader, out var decodedCredential) || reader.BytesRemaining != 0)
            {
                return false;
            }
            attempt = header.Sequence;
            entityId = decodedEntityId;
            credential = decodedCredential;
            return true;
        }

        /// <param name="identity">客户端期望的算法、世界和配置指纹；恢复与首次准入必须采用同一兼容性校验。</param>
        public static bool TryWriteRequest(
            in SimulationWorldIdentity identity,
            SessionResumeCredential credential,
            uint attempt,
            ArraySegment<byte> destination,
            out int bytesWritten)
        {
            bytesWritten = 0;
            if (identity.AlgorithmVersion == 0 || !credential.IsValid || attempt == 0
                || destination.Count < RequestPacketSize
                || !PacketHeaderCodec.TryWrite(new PacketHeader(
                    NetworkMessageType.SessionResumeRequest, RequestPayloadSize, attempt), destination))
            {
                return false;
            }
            var writer = PayloadWriter(destination, RequestPayloadSize);
            if (!writer.TryWriteUInt32(identity.AlgorithmVersion)
                || !writer.TryWriteUInt64(identity.WorldHash)
                || !writer.TryWriteUInt64(identity.ConfigurationHash)
                || !WriteCredential(ref writer, credential))
            {
                return false;
            }
            bytesWritten = RequestPacketSize;
            return true;
        }

        public static bool TryReadRequest(
            ArraySegment<byte> packet,
            out uint attempt,
            out SimulationWorldIdentity identity,
            out SessionResumeCredential credential)
        {
            attempt = 0;
            identity = default;
            credential = default;
            if (!TryReadHeader(packet, NetworkMessageType.SessionResumeRequest, RequestPayloadSize, out var header))
            {
                return false;
            }
            var reader = PayloadReader(packet, RequestPayloadSize);
            if (!reader.TryReadUInt32(out uint algorithm) || algorithm == 0
                || !reader.TryReadUInt64(out ulong world) || !reader.TryReadUInt64(out ulong configuration)
                || !ReadCredential(ref reader, out var decodedCredential) || reader.BytesRemaining != 0)
            {
                return false;
            }
            attempt = header.Sequence;
            identity = new SimulationWorldIdentity(algorithm, world, configuration);
            credential = decodedCredential;
            return true;
        }

        private static bool TryReadHeader(
            ArraySegment<byte> packet, NetworkMessageType type, int size, out PacketHeader header)
        {
            return PacketHeaderCodec.TryRead(packet, out header) && header.MessageType == type
                && header.PayloadLength == size && header.Sequence != 0;
        }

        private static PacketBufferWriter PayloadWriter(ArraySegment<byte> packet, int size)
        {
            return new PacketBufferWriter(new ArraySegment<byte>(packet.Array, packet.Offset + PacketHeader.Size, size));
        }

        private static PacketBufferReader PayloadReader(ArraySegment<byte> packet, int size)
        {
            return new PacketBufferReader(new ArraySegment<byte>(packet.Array, packet.Offset + PacketHeader.Size, size));
        }

        private static bool WriteCredential(ref PacketBufferWriter writer, SessionResumeCredential credential)
        {
            return writer.TryWriteUInt64(credential.Part0) && writer.TryWriteUInt64(credential.Part1)
                && writer.TryWriteUInt64(credential.Part2) && writer.TryWriteUInt64(credential.Part3);
        }

        private static bool ReadCredential(ref PacketBufferReader reader, out SessionResumeCredential credential)
        {
            credential = default;
            if (!reader.TryReadUInt64(out ulong part0) || !reader.TryReadUInt64(out ulong part1)
                || !reader.TryReadUInt64(out ulong part2) || !reader.TryReadUInt64(out ulong part3))
            {
                return false;
            }
            var decoded = new SessionResumeCredential(part0, part1, part2, part3);
            if (!decoded.IsValid)
            {
                return false;
            }
            credential = decoded;
            return true;
        }
    }
}

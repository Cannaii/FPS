using System;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.Serialization;
using AFPS.Simulation.World;

namespace AFPS.NetCode.Messages
{
    public enum SimulationCompatibility : byte
    {
        Compatible = 0,
        AlgorithmMismatch = 1,
        WorldMismatch = 2,
        ConfigurationMismatch = 3
    }

    /// <summary>三步准入协议：Hello/Ready 携带本端指纹，Welcome 返回服务器指纹及比较结果。</summary>
    public static class SimulationHandshakeCodec
    {
        // 算法版本 4B、世界 hash 8B、配置 hash 8B；Welcome 多一个结果字节。
        public const int IdentitySize = 20;
        public const int MaximumPacketSize = PacketHeader.Size + IdentitySize + 1;

        public static SimulationCompatibility Compare(
            in SimulationWorldIdentity expected,
            in SimulationWorldIdentity actual)
        {
            // 多处同时不匹配时固定优先级，客户端也按同一规则验证拒绝原因。
            if (expected.AlgorithmVersion != actual.AlgorithmVersion)
            {
                return SimulationCompatibility.AlgorithmMismatch;
            }

            if (expected.WorldHash != actual.WorldHash)
            {
                return SimulationCompatibility.WorldMismatch;
            }

            return expected.ConfigurationHash == actual.ConfigurationHash
                ? SimulationCompatibility.Compatible
                : SimulationCompatibility.ConfigurationMismatch;
        }

        /// <param name="attempt">非零连接尝试号，存入包头 Sequence；断开重连后递增，防止旧 Welcome 被接受。</param>
        public static bool TrySerialize(
            NetworkMessageType type,
            uint attempt,
            in SimulationWorldIdentity identity,
            SimulationCompatibility result,
            ArraySegment<byte> destination,
            out int bytesWritten)
        {
            bytesWritten = 0;
            bool welcome = type == NetworkMessageType.SimulationWelcome;
            if (!IsHandshake(type) || attempt == 0 || identity.AlgorithmVersion == 0
                || result > SimulationCompatibility.ConfigurationMismatch
                || (!welcome && result != SimulationCompatibility.Compatible))
            {
                return false;
            }

            int payloadSize = IdentitySize + (welcome ? 1 : 0);
            if (destination.Count < PacketHeader.Size + payloadSize
                || !PacketHeaderCodec.TryWrite(new PacketHeader(type, (ushort)payloadSize, attempt), destination))
            {
                return false;
            }

            var writer = new PacketBufferWriter(new ArraySegment<byte>(
                destination.Array, destination.Offset + PacketHeader.Size, payloadSize));
            if (!writer.TryWriteUInt32(identity.AlgorithmVersion)
                || !writer.TryWriteUInt64(identity.WorldHash)
                || !writer.TryWriteUInt64(identity.ConfigurationHash)
                || (welcome && !writer.TryWriteByte((byte)result)))
            {
                return false;
            }

            bytesWritten = PacketHeader.Size + payloadSize;
            return true;
        }

        public static bool TryDeserialize(
            ArraySegment<byte> packet,
            out PacketHeader header,
            out SimulationWorldIdentity identity,
            out SimulationCompatibility result)
        {
            identity = default;
            result = default;
            if (!PacketHeaderCodec.TryRead(packet, out header) || !IsHandshake(header.MessageType)
                || header.Sequence == 0)
            {
                return false;
            }

            bool welcome = header.MessageType == NetworkMessageType.SimulationWelcome;
            if (header.PayloadLength != IdentitySize + (welcome ? 1 : 0))
            {
                return false;
            }

            var reader = new PacketBufferReader(new ArraySegment<byte>(
                packet.Array, packet.Offset + PacketHeader.Size, header.PayloadLength));
            if (!reader.TryReadUInt32(out uint algorithm) || algorithm == 0
                || !reader.TryReadUInt64(out ulong world) || !reader.TryReadUInt64(out ulong configuration))
            {
                return false;
            }

            if (welcome)
            {
                if (!reader.TryReadByte(out byte status) || status > (byte)SimulationCompatibility.ConfigurationMismatch)
                {
                    return false;
                }

                result = (SimulationCompatibility)status;
            }

            identity = new SimulationWorldIdentity(algorithm, world, configuration);
            return reader.BytesRemaining == 0;
        }

        public static bool IsHandshake(NetworkMessageType type)
        {
            return type == NetworkMessageType.SimulationHello
                || type == NetworkMessageType.SimulationWelcome
                || type == NetworkMessageType.SimulationReady;
        }
    }
}

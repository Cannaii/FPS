using System;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.Serialization;
using AFPS.Simulation.Characters;
using UnityEngine;

namespace AFPS.NetCode.Messages
{
    /// <summary>
    /// 将服务器权威玩家状态编码为固定布局网络包，并解码为客户端校正状态。
    /// </summary>
    public static class AuthoritativePlayerStateCodec
    {
        /// <summary>
        /// 权威状态负载的固定字节数。
        /// </summary>
        public const int PayloadSize = 27;

        /// <summary>
        /// 包头和权威状态负载合计的固定字节数。
        /// </summary>
        public const int PacketSize = PacketHeader.Size + PayloadSize;

        /// <summary>
        /// 位置整数最小单位为一毫米。
        /// </summary>
        public const float PositionResolution = 0.001f;

        /// <summary>
        /// 速度整数最小单位为每秒一厘米。
        /// </summary>
        public const float VelocityResolution = 0.01f;

        /// <summary>
        /// 三个位置分量共同量化后可能产生的最大空间距离误差，单位为米。
        /// </summary>
        public const float MaximumPositionQuantizationError = 0.000867f;

        /// <summary>
        /// 三个速度分量共同量化后可能产生的最大速度差，单位为米每秒。
        /// </summary>
        public const float MaximumVelocityQuantizationError = 0.008661f;

        /// <summary>
        /// 为量化误差和少量浮点运算误差保留余量的位置校正阈值建议值，单位为米。
        /// </summary>
        public const float RecommendedPositionErrorThreshold = 0.002f;

        /// <summary>
        /// 为量化误差和少量浮点运算误差保留余量的速度校正阈值建议值，单位为米每秒。
        /// </summary>
        public const float RecommendedVelocityErrorThreshold = 0.02f;

        private const byte GroundedMask = 1 << 0;

        public static bool TrySerialize(in AuthoritativePlayerState authoritativeState, uint sequence, ArraySegment<byte> destination, out int bytesWritten)
        {
            bytesWritten = 0;
            if (destination.Count < PacketSize || authoritativeState.State.Tick != authoritativeState.LastProcessedInputTick)
            {
                return false;
            }

            PlayerState state = authoritativeState.State;
            if (!TryQuantizePosition(state.Position.x, out int positionX) || !TryQuantizePosition(state.Position.y, out int positionY) || !TryQuantizePosition(state.Position.z, out int positionZ))
            {
                return false;
            }

            if (!TryQuantizeVelocity(state.Velocity.x, out short velocityX) || !TryQuantizeVelocity(state.Velocity.y, out short velocityY) || !TryQuantizeVelocity(state.Velocity.z, out short velocityZ))
            {
                return false;
            }

            PacketHeader header = new PacketHeader(NetworkMessageType.AuthoritativePlayerState, PayloadSize, sequence);
            if (!PacketHeaderCodec.TryWrite(header, destination))
            {
                return false;
            }

            ArraySegment<byte> payloadDestination = new ArraySegment<byte>(destination.Array, destination.Offset + PacketHeader.Size, PayloadSize);
            PacketBufferWriter writer = new PacketBufferWriter(payloadDestination);
            byte flags = state.IsGrounded ? GroundedMask : (byte)0;
            bool success = writer.TryWriteUInt32(authoritativeState.ServerTick) && writer.TryWriteUInt32(authoritativeState.LastProcessedInputTick) && writer.TryWriteInt32(positionX) && writer.TryWriteInt32(positionY) && writer.TryWriteInt32(positionZ) && writer.TryWriteInt16(velocityX) && writer.TryWriteInt16(velocityY) && writer.TryWriteInt16(velocityZ) && writer.TryWriteByte(flags);
            if (!success)
            {
                return false;
            }

            bytesWritten = PacketHeader.Size + writer.BytesWritten;
            return true;
        }

        public static bool TryDeserialize(ArraySegment<byte> packet, out PacketHeader header, out AuthoritativePlayerState authoritativeState)
        {
            header = default;
            authoritativeState = default;
            if (!PacketHeaderCodec.TryRead(packet, out header) || header.MessageType != NetworkMessageType.AuthoritativePlayerState || header.PayloadLength != PayloadSize)
            {
                return false;
            }

            ArraySegment<byte> payload = new ArraySegment<byte>(packet.Array, packet.Offset + PacketHeader.Size, PayloadSize);
            PacketBufferReader reader = new PacketBufferReader(payload);
            if (!reader.TryReadUInt32(out uint serverTick) || !reader.TryReadUInt32(out uint lastProcessedInputTick) || !reader.TryReadInt32(out int positionX) || !reader.TryReadInt32(out int positionY) || !reader.TryReadInt32(out int positionZ) || !reader.TryReadInt16(out short velocityX) || !reader.TryReadInt16(out short velocityY) || !reader.TryReadInt16(out short velocityZ) || !reader.TryReadByte(out byte flags))
            {
                return false;
            }

            if (reader.BytesRemaining != 0 || (flags & ~GroundedMask) != 0)
            {
                return false;
            }

            PlayerState state = new PlayerState
            {
                Tick = lastProcessedInputTick,
                Position = new Vector3(DequantizePosition(positionX), DequantizePosition(positionY), DequantizePosition(positionZ)),
                Velocity = new Vector3(DequantizeVelocity(velocityX), DequantizeVelocity(velocityY), DequantizeVelocity(velocityZ)),
                IsGrounded = (flags & GroundedMask) != 0
            };
            authoritativeState = new AuthoritativePlayerState(serverTick, lastProcessedInputTick, state);
            return true;
        }

        private static bool TryQuantizePosition(float value, out int quantized)
        {
            if (!IsFinite(value))
            {
                quantized = default;
                return false;
            }

            double rounded = Math.Round(value / PositionResolution, MidpointRounding.AwayFromZero);
            if (rounded < int.MinValue || rounded > int.MaxValue)
            {
                quantized = default;
                return false;
            }

            quantized = (int)rounded;
            return true;
        }

        private static bool TryQuantizeVelocity(float value, out short quantized)
        {
            if (!IsFinite(value))
            {
                quantized = default;
                return false;
            }

            double rounded = Math.Round(value / VelocityResolution, MidpointRounding.AwayFromZero);
            if (rounded < short.MinValue || rounded > short.MaxValue)
            {
                quantized = default;
                return false;
            }

            quantized = (short)rounded;
            return true;
        }

        private static float DequantizePosition(int value) => value * PositionResolution;

        private static float DequantizeVelocity(short value) => value * VelocityResolution;

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

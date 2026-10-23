using System;
using AFPS.Math;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.Serialization;
using AFPS.Simulation.Characters;

namespace AFPS.NetCode.Messages
{
    /// <summary>权威状态：位置/速度/体力无损Raw，观察角沿用无损可逆的百分度网格。</summary>
    public static class AuthoritativePlayerStateCodec
    {
        // Tick各4B，位置/速度各12B，Yaw/Pitch各2B，落地/冲刺/耗尽位1B，已消耗体力Raw 4B。
        public const int PayloadSize = 41;
        public const int PacketSize = PacketHeader.Size + PayloadSize;
        // 以下分辨率暂供旧远端 Snapshot 使用，权威状态不再按这些常量量化。
        public const float PositionResolution = 0.001f;
        public const float VelocityResolution = 0.01f;
        public const float LookAngleResolution = 0.01f;
        public const float MaximumPositionQuantizationError = 0f;
        public const float MaximumVelocityQuantizationError = 0f;
        public const float RecommendedPositionErrorThreshold = 0f;
        public const float RecommendedVelocityErrorThreshold = 0f;
        private const byte GroundedMask = 1;
        private const byte SprintingMask = 2;
        private const byte ExhaustedMask = 4;
        private const byte KnownFlags = GroundedMask | SprintingMask | ExhaustedMask;
        public static bool TrySerialize(in AuthoritativePlayerState authoritative, uint sequence, ArraySegment<byte> destination, out int bytesWritten)
        {
            bytesWritten = 0;
            PlayerState state = authoritative.State;
            if (destination.Count < PacketSize || state.Tick != authoritative.LastProcessedInputTick
                || state.StaminaSpentFixed < Fix.Zero || state.StaminaSpentFixed > Fix.One
                || (state.IsSprinting && state.SprintExhausted))
            {
                return false;
            }

            // 观察角必须已处于输入网格，禁止在权威发送时再次量化后悄悄丢失状态精度。
            ushort yaw = InputAngleGrid.YawFromFix(state.YawFixedDegrees);
            short pitch = InputAngleGrid.PitchFromFix(state.PitchFixedDegrees);
            if (InputAngleGrid.YawToFix(yaw) != state.YawFixedDegrees || InputAngleGrid.PitchToFix(pitch) != state.PitchFixedDegrees)
            {
                return false;
            }

            if (!PacketHeaderCodec.TryWrite(new PacketHeader(NetworkMessageType.AuthoritativePlayerState, PayloadSize, sequence), destination))
            {
                return false;
            }

            var writer = new PacketBufferWriter(new ArraySegment<byte>(destination.Array, destination.Offset + PacketHeader.Size, PayloadSize));
            if (!writer.TryWriteUInt32(authoritative.ServerTick)
                || !writer.TryWriteUInt32(authoritative.LastProcessedInputTick)
                || !WriteVector(ref writer, state.PositionFixed)
                || !WriteVector(ref writer, state.VelocityFixed)
                || !writer.TryWriteUInt16(yaw)
                || !writer.TryWriteInt16(pitch)
                || !writer.TryWriteByte((byte)((state.IsGrounded ? GroundedMask : 0)
                    | (state.IsSprinting ? SprintingMask : 0) | (state.SprintExhausted ? ExhaustedMask : 0)))
                || !writer.TryWriteInt32(state.StaminaSpentFixed.Raw))
            {
                return false;
            }

            bytesWritten = PacketHeader.Size + writer.BytesWritten;
            return true;
        }

        public static bool TryDeserialize(ArraySegment<byte> packet, out PacketHeader header, out AuthoritativePlayerState authoritative)
        {
            header = default;
            authoritative = default;
            if (!PacketHeaderCodec.TryRead(packet, out header)
                || header.MessageType != NetworkMessageType.AuthoritativePlayerState
                || header.PayloadLength != PayloadSize)
            {
                return false;
            }

            var reader = new PacketBufferReader(new ArraySegment<byte>(packet.Array, packet.Offset + PacketHeader.Size, PayloadSize));
            if (!reader.TryReadUInt32(out uint serverTick)
                || !reader.TryReadUInt32(out uint inputTick)
                || !ReadVector(ref reader, out FixVec3 position)
                || !ReadVector(ref reader, out FixVec3 velocity)
                || !reader.TryReadUInt16(out ushort yaw)
                || !reader.TryReadInt16(out short pitch)
                || !reader.TryReadByte(out byte flags)
                || !reader.TryReadInt32(out int staminaSpent))
            {
                return false;
            }

            if (reader.BytesRemaining != 0 || (flags & ~KnownFlags) != 0 || !InputAngleGrid.IsValid(yaw, pitch)
                || staminaSpent < 0 || staminaSpent > Fix.Scale
                || ((flags & SprintingMask) != 0 && (flags & ExhaustedMask) != 0))
            {
                return false;
            }

            var state = new PlayerState
            {
                Tick = inputTick,
                PositionFixed = position,
                VelocityFixed = velocity,
                YawFixedDegrees = InputAngleGrid.YawToFix(yaw),
                PitchFixedDegrees = InputAngleGrid.PitchToFix(pitch),
                IsGrounded = (flags & GroundedMask) != 0,
                StaminaSpentFixed = Fix.FromRaw(staminaSpent),
                IsSprinting = (flags & SprintingMask) != 0,
                SprintExhausted = (flags & ExhaustedMask) != 0
            };
            authoritative = new AuthoritativePlayerState(serverTick, inputTick, state);
            return true;
        }

        private static bool WriteVector(ref PacketBufferWriter writer, in FixVec3 value) => writer.TryWriteInt32(value.X.Raw) && writer.TryWriteInt32(value.Y.Raw) && writer.TryWriteInt32(value.Z.Raw);
        private static bool ReadVector(ref PacketBufferReader reader, out FixVec3 value)
        {
            value = default;
            if (!reader.TryReadInt32(out int x) || !reader.TryReadInt32(out int y) || !reader.TryReadInt32(out int z))
            {
                return false;
            }

            value = new FixVec3(Fix.FromRaw(x), Fix.FromRaw(y), Fix.FromRaw(z));
            return true;
        }
    }
}

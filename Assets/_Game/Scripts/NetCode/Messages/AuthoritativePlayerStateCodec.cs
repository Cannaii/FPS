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
        // 原99B后追加累计墙跑秒4B；墙跑状态使用flags的bit5。
        public const int PayloadSize = 103;
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
        private const byte GroundJumpConsumedMask = 8;
        private const byte CrouchedMask = 16;
        private const byte WallRunningMask = 32;
        private const byte KnownFlags = GroundedMask | SprintingMask | ExhaustedMask
            | GroundJumpConsumedMask | CrouchedMask | WallRunningMask;
        public static bool TrySerialize(in AuthoritativePlayerState authoritative, uint sequence, ArraySegment<byte> destination, out int bytesWritten)
        {
            bytesWritten = 0;
            PlayerState state = authoritative.State;
            if (destination.Count < PacketSize || state.Tick != authoritative.LastProcessedInputTick
                || state.StaminaSpentFixed < Fix.Zero || state.StaminaSpentFixed > Fix.One
                || (state.IsSprinting && state.SprintExhausted)
                || !IsValidDash(state) || !IsValidJump(state) || !IsValidWall(state) || !IsValidStance(state)
                || !IsValidWallRun(state))
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
                    | (state.IsSprinting ? SprintingMask : 0) | (state.SprintExhausted ? ExhaustedMask : 0)
                    | (state.GroundJumpConsumed ? GroundJumpConsumedMask : 0) | (state.IsCrouched ? CrouchedMask : 0)
                    | (state.IsWallRunning ? WallRunningMask : 0)))
                || !writer.TryWriteInt32(state.StaminaSpentFixed.Raw)
                || !writer.TryWriteInt32(state.DashRemainingFixed.Raw)
                || !writer.TryWriteInt32(state.DashVelocityFixed.X.Raw)
                || !writer.TryWriteInt32(state.DashVelocityFixed.Z.Raw)
                || !writer.TryWriteByte((byte)state.DashSpentCharges)
                || !writer.TryWriteInt32(state.DashRechargeRemainingFixed.Raw)
                || !writer.TryWriteInt32(state.CoyoteRemainingFixed.Raw)
                || !writer.TryWriteByte((byte)state.AirJumpsUsed)
                || !writer.TryWriteInt32(state.WallNormalFixed.X.Raw)
                || !writer.TryWriteInt32(state.WallNormalFixed.Z.Raw)
                || !WriteVector(ref writer, state.WallIncomingVelocityFixed)
                || !writer.TryWriteInt32(state.WallCooldownRemainingFixed.Raw)
                || !writer.TryWriteInt32(state.SlideRemainingFixed.Raw)
                || !writer.TryWriteInt32(state.SlideVelocityFixed.X.Raw)
                || !writer.TryWriteInt32(state.SlideVelocityFixed.Z.Raw)
                || !writer.TryWriteInt32(state.WallRunUsedFixed.Raw))
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
                || !reader.TryReadInt32(out int staminaSpent)
                || !reader.TryReadInt32(out int dashRemaining)
                || !reader.TryReadInt32(out int dashX)
                || !reader.TryReadInt32(out int dashZ)
                || !reader.TryReadByte(out byte dashSpentCharges)
                || !reader.TryReadInt32(out int dashRechargeRemaining)
                || !reader.TryReadInt32(out int coyoteRemaining)
                || !reader.TryReadByte(out byte airJumpsUsed)
                || !reader.TryReadInt32(out int wallX)
                || !reader.TryReadInt32(out int wallZ)
                || !ReadVector(ref reader, out FixVec3 incomingVelocity)
                || !reader.TryReadInt32(out int wallCooldown)
                || !reader.TryReadInt32(out int slideRemaining)
                || !reader.TryReadInt32(out int slideX)
                || !reader.TryReadInt32(out int slideZ)
                || !reader.TryReadInt32(out int wallRunUsed))
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
                SprintExhausted = (flags & ExhaustedMask) != 0,
                DashRemainingFixed = Fix.FromRaw(dashRemaining),
                DashVelocityFixed = new FixVec3(Fix.FromRaw(dashX), Fix.Zero, Fix.FromRaw(dashZ)),
                DashSpentCharges = dashSpentCharges,
                DashRechargeRemainingFixed = Fix.FromRaw(dashRechargeRemaining),
                CoyoteRemainingFixed = Fix.FromRaw(coyoteRemaining),
                GroundJumpConsumed = (flags & GroundJumpConsumedMask) != 0,
                AirJumpsUsed = airJumpsUsed,
                WallNormalFixed = new FixVec3(Fix.FromRaw(wallX), Fix.Zero, Fix.FromRaw(wallZ)),
                WallIncomingVelocityFixed = incomingVelocity,
                WallCooldownRemainingFixed = Fix.FromRaw(wallCooldown),
                IsCrouched = (flags & CrouchedMask) != 0,
                SlideRemainingFixed = Fix.FromRaw(slideRemaining),
                SlideVelocityFixed = new FixVec3(Fix.FromRaw(slideX), Fix.Zero, Fix.FromRaw(slideZ)),
                IsWallRunning = (flags & WallRunningMask) != 0,
                WallRunUsedFixed = Fix.FromRaw(wallRunUsed)
            };
            if (!IsValidDash(state) || !IsValidJump(state) || !IsValidWall(state) || !IsValidStance(state)
                || !IsValidWallRun(state))
            {
                return false;
            }
            authoritative = new AuthoritativePlayerState(serverTick, inputTick, state);
            return true;
        }

        private static bool IsValidWallRun(in PlayerState state) =>
            state.WallRunUsedFixed >= Fix.Zero && state.WallRunUsedFixed <= Fix.FromInt(5)
            && (!state.IsGrounded || state.WallRunUsedFixed == Fix.Zero)
            && (!state.IsWallRunning || (state.WallRunUsedFixed > Fix.Zero && !state.IsGrounded
                && !state.IsCrouched && !state.IsSprinting && state.DashRemainingFixed == Fix.Zero
                && state.SlideRemainingFixed == Fix.Zero && state.WallNormalFixed != FixVec3.Zero));

        private static bool IsValidStance(in PlayerState state)
        {
            bool active = state.SlideRemainingFixed > Fix.Zero;
            return state.SlideRemainingFixed >= Fix.Zero && state.SlideRemainingFixed <= Fix.FromInt(3)
                && state.SlideVelocityFixed.Y == Fix.Zero
                && state.SlideVelocityFixed.X >= Fix.FromInt(-100) && state.SlideVelocityFixed.X <= Fix.FromInt(100)
                && state.SlideVelocityFixed.Z >= Fix.FromInt(-100) && state.SlideVelocityFixed.Z <= Fix.FromInt(100)
                && (active == (state.SlideVelocityFixed != FixVec3.Zero))
                && (!state.IsCrouched || !state.IsSprinting)
                && (!active || (state.IsCrouched && state.IsGrounded && state.DashRemainingFixed == Fix.Zero));
        }

        private static bool IsValidWall(in PlayerState state)
        {
            FixVec3 normal = state.WallNormalFixed;
            if (state.WallCooldownRemainingFixed < Fix.Zero || state.WallCooldownRemainingFixed > Fix.FromInt(5)
                || normal.Y != Fix.Zero || normal.X < -Fix.One || normal.X > Fix.One
                || normal.Z < -Fix.One || normal.Z > Fix.One)
            {
                return false;
            }
            // 先限制分量再求长度，避免坏包极值在向量长度转换时溢出。
            return normal == FixVec3.Zero
                ? state.WallIncomingVelocityFixed == FixVec3.Zero
                : FixMath.Abs(normal.Magnitude - Fix.One) <= Fix.FromRaw(4);
        }

        private static bool IsValidJump(in PlayerState state)
        {
            // 允许零初始化的落地状态；活动宽限与已消耗地面机会互斥，空中跳必须先烧掉地面机会。
            return state.CoyoteRemainingFixed >= Fix.Zero && state.CoyoteRemainingFixed <= Fix.Half
                && state.AirJumpsUsed >= 0 && state.AirJumpsUsed <= 8
                && (!state.GroundJumpConsumed || state.CoyoteRemainingFixed == Fix.Zero)
                && (state.AirJumpsUsed == 0 || state.GroundJumpConsumed)
                && (!state.IsGrounded || (!state.GroundJumpConsumed && state.AirJumpsUsed == 0));
        }

        private static bool IsValidDash(in PlayerState state)
        {
            // 网络只校验所有合法配置共有的范围；具体充能上限由准入指纹保证一致。
            bool active = state.DashRemainingFixed > Fix.Zero;
            return state.DashRemainingFixed >= Fix.Zero && state.DashRemainingFixed <= Fix.FromInt(2)
                && state.DashRechargeRemainingFixed >= Fix.Zero && state.DashRechargeRemainingFixed <= Fix.FromInt(60)
                && state.DashSpentCharges >= 0 && state.DashSpentCharges <= 8
                && ((state.DashSpentCharges == 0) == (state.DashRechargeRemainingFixed == Fix.Zero))
                && state.DashVelocityFixed.Y == Fix.Zero
                && state.DashVelocityFixed.X >= Fix.FromInt(-100) && state.DashVelocityFixed.X <= Fix.FromInt(100)
                && state.DashVelocityFixed.Z >= Fix.FromInt(-100) && state.DashVelocityFixed.Z <= Fix.FromInt(100)
                && (active == (state.DashVelocityFixed != FixVec3.Zero))
                && (!active || (state.DashSpentCharges > 0 && !state.IsSprinting));
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

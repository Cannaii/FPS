using System;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.Serialization;
using AFPS.Simulation.Characters;

namespace AFPS.NetCode.Messages
{
    /// <summary>
    /// 将连续 Tick 输入编码为紧凑网络包，并解码到调用方复用的命令数组中。
    /// </summary>
    public static class InputCommandBatchCodec
    {
        /// <summary>
        /// 单包允许携带的最大输入数量，限制异常包的处理成本。
        /// </summary>
        public const int MaxCommandCount = 32;

        /// <summary>
        /// 输入负载固定字段的字节数：FirstTick 四字节，CommandCount 一字节。
        /// </summary>
        public const int PayloadHeaderSize = 5;

        /// <summary>
        /// 每条输入占用三个字节：MoveX、MoveY 和按键位掩码。
        /// </summary>
        public const int BytesPerCommand = 3;

        private const byte JumpPressedMask = 1 << 0;

        public static int GetPacketSize(int commandCount) => PacketHeader.Size + PayloadHeaderSize + commandCount * BytesPerCommand;

        /// <summary>
        /// 将本地输入转换为与网络解码后完全一致的值。客户端应在预测前调用，避免模拟原始摇杆值而服务器模拟量化值。
        /// </summary>
        public static PlayerInputCommand Canonicalize(PlayerInputCommand command)
        {
            command.MoveX = DequantizeAxis(QuantizeAxis(command.MoveX));
            command.MoveY = DequantizeAxis(QuantizeAxis(command.MoveY));
            return command;
        }

        public static bool TrySerialize(InputCommandBatch batch, uint sequence, ArraySegment<byte> destination, out int bytesWritten)
        {
            bytesWritten = 0;
            if (!IsValidBatch(batch) || destination.Count < GetPacketSize(batch.CommandCount))
            {
                return false;
            }

            int payloadLength = PayloadHeaderSize + batch.CommandCount * BytesPerCommand;
            PacketHeader header = new PacketHeader(NetworkMessageType.InputCommandBatch, (ushort)payloadLength, sequence);
            if (!PacketHeaderCodec.TryWrite(header, destination))
            {
                return false;
            }

            ArraySegment<byte> payloadDestination = new ArraySegment<byte>(destination.Array, destination.Offset + PacketHeader.Size, payloadLength);
            PacketBufferWriter writer = new PacketBufferWriter(payloadDestination);
            if (!writer.TryWriteUInt32(batch.FirstTick) || !writer.TryWriteByte((byte)batch.CommandCount))
            {
                return false;
            }

            for (int i = 0; i < batch.CommandCount; i++)
            {
                PlayerInputCommand command = batch.Commands.Array[batch.Commands.Offset + i];
                byte buttons = command.JumpPressed ? JumpPressedMask : (byte)0;
                if (!writer.TryWriteSByte(QuantizeAxis(command.MoveX)) || !writer.TryWriteSByte(QuantizeAxis(command.MoveY)) || !writer.TryWriteByte(buttons))
                {
                    return false;
                }
            }

            bytesWritten = PacketHeader.Size + writer.BytesWritten;
            return true;
        }

        public static bool TryDeserialize(ArraySegment<byte> packet, PlayerInputCommand[] commandBuffer, int commandBufferOffset, out PacketHeader header, out InputCommandBatch batch)
        {
            header = default;
            batch = default;
            if (!PacketHeaderCodec.TryRead(packet, out header) || header.MessageType != NetworkMessageType.InputCommandBatch || commandBuffer == null)
            {
                return false;
            }

            ArraySegment<byte> payload = new ArraySegment<byte>(packet.Array, packet.Offset + PacketHeader.Size, header.PayloadLength);
            PacketBufferReader reader = new PacketBufferReader(payload);
            if (!reader.TryReadUInt32(out uint firstTick) || !reader.TryReadByte(out byte commandCount))
            {
                return false;
            }

            int expectedPayloadLength = PayloadHeaderSize + commandCount * BytesPerCommand;
            if (commandCount == 0 || commandCount > MaxCommandCount || header.PayloadLength != expectedPayloadLength || commandBufferOffset < 0 || commandBufferOffset > commandBuffer.Length - commandCount)
            {
                return false;
            }

            for (int i = 0; i < commandCount; i++)
            {
                if (!reader.TryReadSByte(out sbyte moveX) || !reader.TryReadSByte(out sbyte moveY) || !reader.TryReadByte(out byte buttons))
                {
                    batch = default;
                    return false;
                }

                if ((buttons & ~JumpPressedMask) != 0)
                {
                    batch = default;
                    return false;
                }

                commandBuffer[commandBufferOffset + i] = new PlayerInputCommand
                {
                    Tick = unchecked(firstTick + (uint)i),
                    MoveX = DequantizeAxis(moveX),
                    MoveY = DequantizeAxis(moveY),
                    JumpPressed = (buttons & JumpPressedMask) != 0
                };
            }

            batch = new InputCommandBatch(firstTick, new ArraySegment<PlayerInputCommand>(commandBuffer, commandBufferOffset, commandCount));
            return reader.BytesRemaining == 0;
        }

        private static bool IsValidBatch(InputCommandBatch batch)
        {
            if (batch.Commands.Array == null || batch.CommandCount == 0 || batch.CommandCount > MaxCommandCount)
            {
                return false;
            }

            for (int i = 0; i < batch.CommandCount; i++)
            {
                uint expectedTick = unchecked(batch.FirstTick + (uint)i);
                if (batch.Commands.Array[batch.Commands.Offset + i].Tick != expectedTick)
                {
                    return false;
                }
            }

            return true;
        }

        private static sbyte QuantizeAxis(float value)
        {
            if (float.IsNaN(value))
            {
                return 0;
            }

            float clamped = value < -1f ? -1f : value > 1f ? 1f : value;
            float scaled = clamped * 127f;
            int rounded = scaled >= 0f ? (int)(scaled + 0.5f) : (int)(scaled - 0.5f);
            return (sbyte)rounded;
        }

        private static float DequantizeAxis(sbyte value) => value / 127f;
    }
}

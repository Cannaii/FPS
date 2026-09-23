using System;

namespace AFPS.NetCode.Serialization
{
    /// <summary>
    /// 在调用方提供的数组片段中按小端序写入协议字段，不拥有也不分配缓冲区。
    /// </summary>
    internal struct PacketBufferWriter
    {
        private readonly byte[] buffer;
        private readonly int start;
        private readonly int end;
        private int position;

        public int BytesWritten => position - start;

        public PacketBufferWriter(ArraySegment<byte> destination)
        {
            buffer = destination.Array;
            start = destination.Offset;
            end = destination.Offset + destination.Count;
            position = destination.Offset;
        }

        public bool TryWriteByte(byte value)
        {
            if (buffer == null || position >= end)
            {
                return false;
            }

            buffer[position++] = value;
            return true;
        }

        public bool TryWriteSByte(sbyte value) => TryWriteByte(unchecked((byte)value));

        public bool TryWriteInt16(short value) => TryWriteUInt16(unchecked((ushort)value));

        public bool TryWriteUInt16(ushort value)
        {
            if (buffer == null || end - position < 2)
            {
                return false;
            }

            buffer[position++] = (byte)value;
            buffer[position++] = (byte)(value >> 8);
            return true;
        }

        public bool TryWriteUInt32(uint value)
        {
            if (buffer == null || end - position < 4)
            {
                return false;
            }

            buffer[position++] = (byte)value;
            buffer[position++] = (byte)(value >> 8);
            buffer[position++] = (byte)(value >> 16);
            buffer[position++] = (byte)(value >> 24);
            return true;
        }

        public bool TryWriteInt32(int value) => TryWriteUInt32(unchecked((uint)value));

        public bool TryWriteUInt64(ulong value)
        {
            return TryWriteUInt32((uint)value) && TryWriteUInt32((uint)(value >> 32));
        }
    }

    /// <summary>
    /// 从网络数组片段中按小端序读取协议字段，并在越界前返回失败。
    /// </summary>
    internal struct PacketBufferReader
    {
        private readonly byte[] buffer;
        private readonly int end;
        private int position;

        public int BytesRemaining => end - position;

        public PacketBufferReader(ArraySegment<byte> source)
        {
            buffer = source.Array;
            end = source.Offset + source.Count;
            position = source.Offset;
        }

        public bool TryReadByte(out byte value)
        {
            if (buffer == null || position >= end)
            {
                value = default;
                return false;
            }

            value = buffer[position++];
            return true;
        }

        public bool TryReadSByte(out sbyte value)
        {
            bool success = TryReadByte(out byte rawValue);
            value = unchecked((sbyte)rawValue);
            return success;
        }

        public bool TryReadInt16(out short value)
        {
            bool success = TryReadUInt16(out ushort rawValue);
            value = unchecked((short)rawValue);
            return success;
        }

        public bool TryReadUInt16(out ushort value)
        {
            if (buffer == null || end - position < 2)
            {
                value = default;
                return false;
            }

            value = (ushort)(buffer[position] | buffer[position + 1] << 8);
            position += 2;
            return true;
        }

        public bool TryReadUInt32(out uint value)
        {
            if (buffer == null || end - position < 4)
            {
                value = default;
                return false;
            }

            value = (uint)(buffer[position] | buffer[position + 1] << 8 | buffer[position + 2] << 16 | buffer[position + 3] << 24);
            position += 4;
            return true;
        }

        public bool TryReadInt32(out int value)
        {
            bool success = TryReadUInt32(out uint rawValue);
            value = unchecked((int)rawValue);
            return success;
        }

        public bool TryReadUInt64(out ulong value)
        {
            if (!TryReadUInt32(out uint low) || !TryReadUInt32(out uint high))
            {
                value = default;
                return false;
            }

            value = low | (ulong)high << 32;
            return true;
        }
    }
}

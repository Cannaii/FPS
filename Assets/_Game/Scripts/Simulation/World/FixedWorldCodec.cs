using System;
using AFPS.Math;

namespace AFPS.Simulation.World
{
    public enum FixedWorldReadError
    {
        None,
        InvalidLength,
        InvalidMagic,
        UnsupportedVersion,
        ReservedBits,
        InvalidCount,
        InvalidShape,
        NonCanonicalOrder
    }

    /// <summary>世界快照 v1：小端 Raw、固定长度记录、显式版本；不使用运行时对象哈希或平台字节序。</summary>
    public static class FixedWorldCodec
    {
        public const ushort FormatVersion = 1;
        public const int HeaderSize = 16;
        public const int BoxSize = 36;
        public const int PlaneSize = 20;
        public const int MaxShapeCount = 65536;
        private const uint Magic = 0x31574641; // ASCII AFW1

        internal static byte[] WriteCanonical(FixedWorld world)
        {
            byte[] data = new byte[checked(HeaderSize + world.BoxCount * BoxSize + world.PlaneCount * PlaneSize)];
            int offset = 0;
            Write(data, ref offset, Magic);
            Write(data, ref offset, FormatVersion); // 低 16 位版本，高 16 位保留为零。
            Write(data, ref offset, (uint)world.BoxCount);
            Write(data, ref offset, (uint)world.PlaneCount);
            for (int i = 0; i < world.BoxCount; i++)
            {
                FixedWorldBox box = world.GetBox(i);
                Write(data, ref offset, box.ShapeId);
                Write(data, ref offset, (uint)box.Layer);
                Write(data, ref offset, (uint)box.Surface);
                WriteVector(data, ref offset, box.Bounds.Min);
                WriteVector(data, ref offset, box.Bounds.Max);
            }
            for (int i = 0; i < world.PlaneCount; i++)
            {
                FixedWorldPlane plane = world.GetPlane(i);
                Write(data, ref offset, plane.ShapeId);
                Write(data, ref offset, (uint)plane.Layer);
                Write(data, ref offset, (uint)plane.Surface);
                // axis 1B、flags 1B、reserved 2B。
                uint flags = (plane.PositiveNormal ? 1u : 0u) | (plane.DoubleSided ? 2u : 0u);
                Write(data, ref offset, (uint)plane.Axis | flags << 8);
                Write(data, ref offset, unchecked((uint)plane.Coordinate.Raw));
            }
            return data;
        }

        public static bool TryDeserialize(ArraySegment<byte> source, out FixedWorld world, out FixedWorldReadError error)
        {
            world = null;
            error = FixedWorldReadError.InvalidLength;
            if (source.Array == null || source.Count < HeaderSize) return false;
            byte[] data = source.Array;
            int offset = source.Offset;
            if (Read(data, ref offset) != Magic) { error = FixedWorldReadError.InvalidMagic; return false; }
            uint version = Read(data, ref offset);
            if ((version & 0xffff) != FormatVersion) { error = FixedWorldReadError.UnsupportedVersion; return false; }
            if ((version >> 16) != 0) { error = FixedWorldReadError.ReservedBits; return false; }
            uint boxCount = Read(data, ref offset), planeCount = Read(data, ref offset);
            if ((ulong)boxCount + planeCount > MaxShapeCount) { error = FixedWorldReadError.InvalidCount; return false; }
            long length = HeaderSize + (long)boxCount * BoxSize + (long)planeCount * PlaneSize;
            if (length != source.Count) return false;
            var boxes = new FixedWorldBox[(int)boxCount];
            var planes = new FixedWorldPlane[(int)planeCount];
            try
            {
                uint previousId = 0;
                for (int i = 0; i < boxes.Length; i++)
                {
                    uint id = Read(data, ref offset), layer = Read(data, ref offset);
                    var surface = (FixedSurface)Read(data, ref offset);
                    FixVec3 min = ReadVector(data, ref offset), max = ReadVector(data, ref offset);
                    if (id != 0 && id <= previousId) { error = FixedWorldReadError.NonCanonicalOrder; return false; }
                    if (layer > 31) { error = FixedWorldReadError.InvalidShape; return false; }
                    boxes[i] = new FixedWorldBox(id, (int)layer, surface, new FixedAabb(min, max));
                    previousId = id;
                }
                previousId = 0;
                for (int i = 0; i < planes.Length; i++)
                {
                    uint id = Read(data, ref offset), layer = Read(data, ref offset);
                    var surface = (FixedSurface)Read(data, ref offset);
                    uint bits = Read(data, ref offset);
                    int coordinate = unchecked((int)Read(data, ref offset));
                    if ((bits & 0xfffffc00u) != 0) { error = FixedWorldReadError.ReservedBits; return false; }
                    if ((bits & 0xff) > 2 || layer > 31) { error = FixedWorldReadError.InvalidShape; return false; }
                    if (id != 0 && id <= previousId) { error = FixedWorldReadError.NonCanonicalOrder; return false; }
                    planes[i] = new FixedWorldPlane(id, (int)layer, surface, (FixedAxis)(bits & 0xff), Fix.FromRaw(coordinate), (bits & 0x100) != 0, (bits & 0x200) != 0);
                    previousId = id;
                }
                world = new FixedWorld(boxes, planes);
                error = FixedWorldReadError.None;
                return true;
            }
            catch (ArgumentException)
            {
                error = FixedWorldReadError.InvalidShape;
                return false;
            }
        }

        // FNV-1a 64，对规范快照逐字节计算；它用于内容版本匹配，不是签名或认证。
        internal static ulong ComputeHash(byte[] snapshot)
        {
            ulong hash = 14695981039346656037UL;
            foreach (byte value in snapshot) hash = unchecked((hash ^ value) * 1099511628211UL);
            return hash;
        }

        private static void WriteVector(byte[] data, ref int offset, in FixVec3 value)
        {
            Write(data, ref offset, unchecked((uint)value.X.Raw));
            Write(data, ref offset, unchecked((uint)value.Y.Raw));
            Write(data, ref offset, unchecked((uint)value.Z.Raw));
        }

        private static FixVec3 ReadVector(byte[] data, ref int offset) => new FixVec3(
            Fix.FromRaw(unchecked((int)Read(data, ref offset))), Fix.FromRaw(unchecked((int)Read(data, ref offset))), Fix.FromRaw(unchecked((int)Read(data, ref offset))));

        private static void Write(byte[] data, ref int offset, uint value)
        {
            unchecked
            {
                data[offset++] = (byte)value;
                data[offset++] = (byte)(value >> 8);
                data[offset++] = (byte)(value >> 16);
                data[offset++] = (byte)(value >> 24);
            }
        }

        private static uint Read(byte[] data, ref int offset)
        {
            uint value = unchecked((uint)(data[offset] | data[offset + 1] << 8 | data[offset + 2] << 16 | data[offset + 3] << 24));
            offset += 4;
            return value;
        }
    }
}

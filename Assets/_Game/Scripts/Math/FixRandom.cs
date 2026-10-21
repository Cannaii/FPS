using System;

namespace AFPS.Math
{
    /// <summary>唯一的 xorshift32 实现。状态须随模拟参数/状态流转，不得使用静态单例。</summary>
    [Serializable]
    public struct FixRandom
    {
        // 保留既有网络劣化序列；default(FixRandom) 与显式零种子行为一致。
        public const uint ZeroSeed = 0xA341316Cu;
        private uint state;
        public uint State => state == 0 ? ZeroSeed : state;

        public FixRandom(uint seed) => state = seed == 0 ? ZeroSeed : seed;
        public uint NextUInt()
        {
            uint value = State;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            state = value;
            return value;
        }

        /// <summary>[min, maxExclusive)，整数区间按取模映射；不是密码学随机数。</summary>
        public int NextInt(int min, int maxExclusive)
        {
            if (min >= maxExclusive)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            }

            uint range = (uint)((long)maxExclusive - min);
            return checked((int)(min + (long)(NextUInt() % range)));
        }

        public Fix NextFix01() => Fix.FromRaw((int)(NextUInt() >> 16));
    }
}

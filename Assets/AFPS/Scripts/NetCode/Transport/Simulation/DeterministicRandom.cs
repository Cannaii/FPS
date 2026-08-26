namespace AFPS.NetCode.Transport.Simulation
{
    /// <summary>
    /// 为网络劣化测试提供跨运行可复现的轻量无符号随机序列。
    /// </summary>
    internal struct DeterministicRandom
    {
        /// <summary>
        /// 当前 xorshift32 随机序列状态；种子为零时会替换为固定非零值。
        /// </summary>
        private uint state;

        public DeterministicRandom(uint seed)
        {
            state = seed == 0 ? 0xA341316Cu : seed;
        }

        public double NextUnitDouble()
        {
            uint value = state;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            state = value;
            return value / 4294967296d;
        }
    }
}

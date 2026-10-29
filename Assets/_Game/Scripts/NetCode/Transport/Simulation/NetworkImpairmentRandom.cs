using AFPS.Math;

namespace AFPS.NetCode.Transport.Simulation
{
    /// <summary>网络劣化的浮点边界适配；随机序列统一由 AFPS.Math 产生。</summary>
    internal struct NetworkImpairmentRandom
    {
        private FixRandom random;
        public NetworkImpairmentRandom(uint seed) => random = new FixRandom(seed);
        public double NextUnitDouble() => random.NextUInt() / 4294967296d;
    }
}

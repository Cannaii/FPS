using System;

namespace AFPS.Simulation.World
{
    /// <summary>入局兼容性标识：算法语义、几何内容、数值配置分别比较，便于报告拒绝原因。</summary>
    public readonly struct SimulationWorldIdentity : IEquatable<SimulationWorldIdentity>
    {
        // 改变积分顺序、舍入、三角 LUT 或接触裁决时必须升级；代码排版与注释不升级。
        // v13：锁定水平方向的Dash、充能计时、体力门槛及其与跳跃/快跑的优先级。
        public const uint CurrentAlgorithmVersion = 13;

        /// <summary>模拟和查询的算法语义版本；零保留为未初始化状态。</summary>
        public readonly uint AlgorithmVersion;
        /// <summary>规范 FixedWorld 快照的内容指纹，包含几何与表面元数据。</summary>
        public readonly ulong WorldHash;
        /// <summary>移动/碰撞、步长/层掩码、会话空间和武器规则的规范 Raw 指纹。</summary>
        public readonly ulong ConfigurationHash;

        public SimulationWorldIdentity(uint algorithmVersion, ulong worldHash, ulong configurationHash)
        {
            if (algorithmVersion == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(algorithmVersion));
            }

            AlgorithmVersion = algorithmVersion;
            WorldHash = worldHash;
            ConfigurationHash = configurationHash;
        }

        public bool Equals(SimulationWorldIdentity other)
        {
            return AlgorithmVersion == other.AlgorithmVersion
                && WorldHash == other.WorldHash
                && ConfigurationHash == other.ConfigurationHash;
        }

        public override bool Equals(object obj) => obj is SimulationWorldIdentity other && Equals(other);

        public override int GetHashCode()
        {
            return unchecked((int)(AlgorithmVersion ^ (uint)WorldHash ^ (uint)(WorldHash >> 32)
                ^ (uint)ConfigurationHash ^ (uint)(ConfigurationHash >> 32)));
        }
    }
}

using System;
using AFPS.Math;

namespace AFPS.Simulation.Characters
{
    /// <summary>会话空间约定；所有坐标/间距/半径使用米的 Q16.16，入局兼容性必须覆盖这些参数。</summary>
    public readonly struct SimulationSessionSpatialConfig
    {
        public readonly FixVec3 SpawnOrigin;
        /// <summary>相邻实体出生点沿世界 +X 的非负间距，米；实体 1 位于原点。</summary>
        public readonly Fix SpawnSpacing;
        /// <summary>兴趣球半径，1 Raw～Fix.MaxValue，包含球面边界。</summary>
        public readonly Fix InterestRadius;
        public bool IsValid => SpawnSpacing.Raw >= 0 && InterestRadius.Raw > 0;

        public static SimulationSessionSpatialConfig Default => new SimulationSessionSpatialConfig(
            FixVec3.Zero, Fix.FromRatio(5, 2), Fix.FromInt(150));

        public SimulationSessionSpatialConfig(FixVec3 spawnOrigin, Fix spawnSpacing, Fix interestRadius)
        {
            if (spawnSpacing.Raw < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(spawnSpacing));
            }
            if (interestRadius.Raw <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(interestRadius));
            }
            SpawnOrigin = spawnOrigin;
            SpawnSpacing = spawnSpacing;
            InterestRadius = interestRadius;
        }

        public bool TryGetSpawnPosition(uint entityId, out FixVec3 position)
        {
            position = default;
            if (!IsValid || entityId == 0)
            {
                return false;
            }

            // uint 实体序号 × 非负 int Raw 的最大乘积及原点和仍可放进 long；只在最后检查坐标域。
            // 不先转 float，也不把完整实体序号转 Fix，避免大坐标丢位或序号乘法提前溢出。
            long x = SpawnOrigin.X.Raw + (long)(entityId - 1) * SpawnSpacing.Raw;
            if (x < int.MinValue || x > int.MaxValue)
            {
                return false;
            }
            position = new FixVec3(Fix.FromRaw((int)x), SpawnOrigin.Y, SpawnOrigin.Z);
            return true;
        }

        public FixVec3 GetSpawnPosition(uint entityId)
        {
            if (!TryGetSpawnPosition(entityId, out FixVec3 position))
            {
                throw new ArgumentOutOfRangeException(nameof(entityId), "出生位置超出 Q16.16 坐标域。");
            }
            return position;
        }

        public bool TryMeasureInterestDistance(
            in FixVec3 observer,
            in FixVec3 target,
            out ulong squaredRawDistance)
        {
            squaredRawDistance = 0;
            if (!IsValid)
            {
                return false;
            }
            long radius = InterestRadius.Raw;
            long dx = (long)target.X.Raw - observer.X.Raw;
            long dy = (long)target.Y.Raw - observer.Y.Raw;
            long dz = (long)target.Z.Raw - observer.Z.Raw;
            // 两个 int 极值之差可超过 int；先用 long 做轴向筛选，不能先做 FixVec3 减法或平方。
            if (dx < -radius || dx > radius || dy < -radius || dy > radius
                || dz < -radius || dz > radius)
            {
                return false;
            }
            // 筛选后每轴绝对值不超过 int.MaxValue，单轴平方可放进 long，三轴和可放进 ulong。
            // 保留完整 Q32.32 乘积，不开方、不除 Scale，以精确区分只差 1 Raw 的球面两侧。
            ulong distance = (ulong)(dx * dx) + (ulong)(dy * dy) + (ulong)(dz * dz);
            if (distance > (ulong)(radius * radius))
            {
                return false;
            }
            squaredRawDistance = distance;
            return true;
        }
    }
}
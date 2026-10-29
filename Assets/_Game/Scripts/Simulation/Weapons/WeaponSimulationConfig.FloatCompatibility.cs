using AFPS.Math;

namespace AFPS.Simulation.Weapons
{
    /// <summary>兼容现有战斗/表现的浮点边界；后续射击与弹道迁移后只在表现层使用。</summary>
    public readonly partial struct WeaponSimulationConfig
    {
        public float Damage => DamageFixed.ToFloat();
        public float Range => RangeFixed.ToFloat();
        public float ProjectileSpeed => ProjectileSpeedFixed.ToFloat();
        public float EyeHeight => EyeHeightFixed.ToFloat();

        public WeaponSimulationConfig(
            ushort weaponId,
            WeaponFireMode fireMode,
            int magazineCapacity,
            uint fireIntervalTicks,
            float damage,
            float range,
            float projectileSpeed,
            float eyeHeight)
            : this(weaponId, fireMode, magazineCapacity, fireIntervalTicks,
                Fix.FromFloat(damage), Fix.FromFloat(range),
                fireMode == WeaponFireMode.Projectile ? Fix.FromFloat(projectileSpeed) : Fix.Zero,
                QuantizeEyeHeight(eyeHeight))
        {
            // 非有限值、溢出以及量化后为零的正参数都必须失败，不能创建表面有效的零伤害武器。
        }

        private static Fix QuantizeEyeHeight(float eyeHeight)
        {
            // 先检查输入符号；负值即使小到会舍入成零，也不能绕过非负参数约束。
            if (eyeHeight < 0f)
            {
                throw new System.ArgumentOutOfRangeException(nameof(eyeHeight));
            }
            return Fix.FromFloat(eyeHeight);
        }
    }
}

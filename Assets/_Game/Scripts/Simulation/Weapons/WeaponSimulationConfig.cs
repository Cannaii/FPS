using System;
using AFPS.Math;

namespace AFPS.Simulation.Weapons
{
    /// <summary>预测和权威共用的不可变武器规则；数值只存 Q16.16，不在运行时反复量化。</summary>
    public readonly partial struct WeaponSimulationConfig
    {
        public readonly ushort WeaponId;
        public readonly WeaponFireMode FireMode;
        /// <summary>弹匣容量，1～int.MaxValue 发。</summary>
        public readonly int MagazineCapacity;
        /// <summary>两次接受射击之间最少经过的服务器 Tick 数，1～uint.MaxValue。</summary>
        public readonly uint FireIntervalTicks;
        /// <summary>单发伤害，1 Raw～Fix.MaxValue 生命值。</summary>
        public readonly Fix DamageFixed;
        /// <summary>最大射程，1 Raw～Fix.MaxValue 米。</summary>
        public readonly Fix RangeFixed;
        /// <summary>弹丸速度，米/秒；Projectile 必须为正，Hitscan 规范为零。</summary>
        public readonly Fix ProjectileSpeedFixed;
        /// <summary>射击起点相对脚底的世界 +Y 偏移，0～Fix.MaxValue 米。</summary>
        public readonly Fix EyeHeightFixed;

        public WeaponSimulationConfig(
            ushort weaponId,
            WeaponFireMode fireMode,
            int magazineCapacity,
            uint fireIntervalTicks,
            Fix damage,
            Fix range,
            Fix projectileSpeed,
            Fix eyeHeight)
        {
            if (weaponId == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(weaponId));
            }
            if (fireMode != WeaponFireMode.Hitscan && fireMode != WeaponFireMode.Projectile)
            {
                throw new ArgumentOutOfRangeException(nameof(fireMode));
            }
            if (magazineCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(magazineCapacity));
            }
            if (fireIntervalTicks == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(fireIntervalTicks));
            }
            if (damage.Raw <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(damage));
            }
            if (range.Raw <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(range));
            }
            if (fireMode == WeaponFireMode.Projectile && projectileSpeed.Raw <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(projectileSpeed));
            }
            if (eyeHeight.Raw < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(eyeHeight));
            }

            WeaponId = weaponId;
            FireMode = fireMode;
            MagazineCapacity = magazineCapacity;
            FireIntervalTicks = fireIntervalTicks;
            DamageFixed = damage;
            RangeFixed = range;
            // Hitscan 不消费弹速，必须规范为零，避免无效参数导致指纹分叉。
            ProjectileSpeedFixed = fireMode == WeaponFireMode.Projectile ? projectileSpeed : Fix.Zero;
            EyeHeightFixed = eyeHeight;
        }

        public static WeaponSimulationConfig DefaultRifle => new WeaponSimulationConfig(
            1, WeaponFireMode.Hitscan, 30, 5, Fix.FromInt(20), Fix.FromInt(200), Fix.Zero,
            Fix.FromRaw(104858)); // 1.6 米按半格远离零舍入为 104858 Raw。

        public bool IsValid => WeaponId != 0
            && (FireMode == WeaponFireMode.Hitscan || FireMode == WeaponFireMode.Projectile)
            && MagazineCapacity > 0 && FireIntervalTicks > 0
            && DamageFixed.Raw > 0 && RangeFixed.Raw > 0 && EyeHeightFixed.Raw >= 0
            && (FireMode == WeaponFireMode.Projectile
                ? ProjectileSpeedFixed.Raw > 0 : ProjectileSpeedFixed.Raw == 0);
    }
}
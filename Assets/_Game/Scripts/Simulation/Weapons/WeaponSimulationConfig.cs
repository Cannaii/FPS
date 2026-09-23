using System;

namespace AFPS.Simulation.Weapons
{
    /// <summary>客户端预测表现与服务器权威射击共享的不可变武器规则。</summary>
    public readonly struct WeaponSimulationConfig
    {
        public readonly ushort WeaponId;
        public readonly WeaponFireMode FireMode;
        public readonly int MagazineCapacity;
        public readonly uint FireIntervalTicks;
        public readonly float Damage;
        public readonly float Range;
        public readonly float ProjectileSpeed;
        public readonly float EyeHeight;

        public WeaponSimulationConfig(ushort weaponId, WeaponFireMode fireMode, int magazineCapacity, uint fireIntervalTicks, float damage, float range, float projectileSpeed, float eyeHeight)
        {
            if (weaponId == 0) throw new ArgumentOutOfRangeException(nameof(weaponId));
            if (fireMode != WeaponFireMode.Hitscan && fireMode != WeaponFireMode.Projectile) throw new ArgumentOutOfRangeException(nameof(fireMode));
            if (magazineCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(magazineCapacity));
            if (fireIntervalTicks == 0) throw new ArgumentOutOfRangeException(nameof(fireIntervalTicks));
            if (!IsPositiveFinite(damage)) throw new ArgumentOutOfRangeException(nameof(damage));
            if (!IsPositiveFinite(range)) throw new ArgumentOutOfRangeException(nameof(range));
            if (fireMode == WeaponFireMode.Projectile && !IsPositiveFinite(projectileSpeed)) throw new ArgumentOutOfRangeException(nameof(projectileSpeed));
            if (float.IsNaN(eyeHeight) || float.IsInfinity(eyeHeight) || eyeHeight < 0f) throw new ArgumentOutOfRangeException(nameof(eyeHeight));

            WeaponId = weaponId;
            FireMode = fireMode;
            MagazineCapacity = magazineCapacity;
            FireIntervalTicks = fireIntervalTicks;
            Damage = damage;
            Range = range;
            ProjectileSpeed = fireMode == WeaponFireMode.Projectile ? projectileSpeed : 0f;
            EyeHeight = eyeHeight;
        }

        public static WeaponSimulationConfig DefaultRifle => new WeaponSimulationConfig(1, WeaponFireMode.Hitscan, 30, 5, 20f, 200f, 0f, 1.6f);

        public bool IsValid => WeaponId != 0 && (FireMode == WeaponFireMode.Hitscan || FireMode == WeaponFireMode.Projectile) && MagazineCapacity > 0 && FireIntervalTicks > 0 && IsPositiveFinite(Damage) && IsPositiveFinite(Range) && (FireMode != WeaponFireMode.Projectile || IsPositiveFinite(ProjectileSpeed)) && EyeHeight >= 0f && !float.IsNaN(EyeHeight) && !float.IsInfinity(EyeHeight);

        private static bool IsPositiveFinite(float value) => value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

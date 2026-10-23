using AFPS.Math;

namespace AFPS.Simulation.Weapons
{
    /// <summary>不可变双武器目录。主武器由世界配置提供，次武器及切换规则也必须进入世界指纹。</summary>
    public static class WeaponCatalog
    {
        public const uint EquipDurationTicks = 10; // 50Hz 下约 0.2 秒，使用服务器世界 Tick。
        public static Fix LauncherExplosionRadius => Fix.FromInt(3); // 次武器爆炸半径，米；进入世界指纹。

        public static WeaponSimulationConfig SecondaryFor(in WeaponSimulationConfig primary) =>
            new WeaponSimulationConfig((ushort)(primary.WeaponId == 2 ? 3 : 2), WeaponFireMode.Projectile,
                5, 35, Fix.FromInt(100), Fix.FromInt(120), Fix.FromInt(30), Fix.FromRaw(104858));
    }
}

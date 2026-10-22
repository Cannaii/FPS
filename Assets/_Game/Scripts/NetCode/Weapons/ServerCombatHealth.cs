using System;
using AFPS.Math;

namespace AFPS.NetCode.Weapons
{
    public static class ServerCombatHealth
    {
        /// <summary>伤害和生命值单位一致且非负；过量伤害钳到零，不让负血触发定点溢出。</summary>
        public static Fix ApplyDamage(Fix health, Fix damage)
        {
            if (health.Raw < 0 || damage.Raw < 0)
            {
                throw new ArgumentOutOfRangeException(health.Raw < 0 ? nameof(health) : nameof(damage));
            }
            return Fix.FromRaw(System.Math.Max(0, health.Raw - damage.Raw));
        }

        /// <summary>旧 Deathmatch 整数显示边界；恰好半生命值向偶数舍入，保留原 Mathf.RoundToInt 语义。</summary>
        public static ushort ToDisplayHealth(Fix health)
        {
            if (health.Raw < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(health));
            }
            int whole = health.Raw / (int)Fix.Scale;
            int remainder = health.Raw % (int)Fix.Scale;
            if (remainder > Fix.HalfScale || (remainder == Fix.HalfScale && (whole & 1) != 0))
            {
                whole++;
            }
            return (ushort)whole;
        }
    }
}

using System;
using AFPS.Math;

namespace AFPS.Simulation.Characters
{
    /// <summary>瞬时冲刺规则；全部字段进入世界指纹，禁止端间单独调参。</summary>
    public readonly struct PlayerDashConfig
    {
        public readonly Fix Speed; // 水平米/秒，(0,100]。
        public readonly Fix Duration; // 秒，(0,2]；起始Tick计入持续时间。
        public readonly Fix RechargeDuration; // 每恢复一次充能所需秒数，[Duration,60]。
        public readonly int MaxCharges; // 充能上限，[1,8]。
        public readonly Fix StaminaCost; // 单次归一化体力消耗，[0,1]；不足时拒绝。

        public PlayerDashConfig(Fix speed, Fix duration, Fix rechargeDuration, int maxCharges, Fix staminaCost)
        {
            if (speed <= Fix.Zero || speed > Fix.FromInt(100)
                || duration <= Fix.Zero || duration > Fix.FromInt(2)
                || rechargeDuration < duration || rechargeDuration > Fix.FromInt(60)
                || maxCharges < 1 || maxCharges > 8 || staminaCost < Fix.Zero || staminaCost > Fix.One)
            {
                throw new ArgumentOutOfRangeException(nameof(speed));
            }
            Speed = speed;
            Duration = duration;
            RechargeDuration = rechargeDuration;
            MaxCharges = maxCharges;
            StaminaCost = staminaCost;
        }

        public static PlayerDashConfig Default => new PlayerDashConfig(
            Fix.FromInt(16), Fix.FromRatio(4, 25), Fix.FromRatio(3, 2), 2, Fix.FromRatio(1, 5));
    }
}

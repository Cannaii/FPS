using System;
using AFPS.Math;

namespace AFPS.Simulation.Characters
{
    /// <summary>墙跳/反弹纯数值规则；全部进入世界指纹，几何由运动器接触提供。</summary>
    public readonly struct PlayerWallConfig
    {
        public readonly Fix PushSpeed; // 离墙水平速度，米/秒，(0,100]。
        public readonly Fix UpSpeed; // 墙跳替换后的向上速度，米/秒，(0,100]；反弹保留原Y动量。
        public readonly Fix MaximumBounceSpeed; // 反弹总速/墙跳水平速度上限，米/秒，[PushSpeed,100]。
        public readonly Fix Cooldown; // 触发后完整冷却秒数，(0,5]，两类动作共用。
        public readonly Fix StaminaCost; // 单次归一化消耗，[0,1]。

        public PlayerWallConfig(Fix pushSpeed, Fix upSpeed, Fix maximumBounceSpeed,
            Fix cooldown, Fix staminaCost)
        {
            if (pushSpeed <= Fix.Zero || pushSpeed > Fix.FromInt(100)
                || upSpeed <= Fix.Zero || upSpeed > Fix.FromInt(100)
                || maximumBounceSpeed < pushSpeed || maximumBounceSpeed > Fix.FromInt(100)
                || cooldown <= Fix.Zero || cooldown > Fix.FromInt(5)
                || staminaCost < Fix.Zero || staminaCost > Fix.One)
            {
                throw new ArgumentOutOfRangeException(nameof(pushSpeed));
            }
            PushSpeed = pushSpeed;
            UpSpeed = upSpeed;
            MaximumBounceSpeed = maximumBounceSpeed;
            Cooldown = cooldown;
            StaminaCost = staminaCost;
        }

        public static PlayerWallConfig Default => new PlayerWallConfig(
            Fix.FromInt(6), Fix.FromInt(8), Fix.FromInt(20), Fix.FromRatio(1, 4), Fix.FromRatio(3, 20));
    }
}

using System;
using AFPS.Math;

namespace AFPS.Simulation.Characters
{
    /// <summary>快跑纯数值规则；体力规范为 [0,1]，所有字段进入世界指纹。</summary>
    public readonly struct PlayerSprintConfig
    {
        public readonly Fix SpeedMultiplier; // 速度倍率，[1,4]。
        public readonly Fix DrainPerSecond; // 每秒消耗的归一化体力，必须为正。
        public readonly Fix RecoveryPerSecond; // 每秒恢复的归一化体力，必须为正。
        public readonly Fix RestartStamina; // 耗尽后重启所需的剩余体力，(0,1]。

        public PlayerSprintConfig(Fix speedMultiplier, Fix drainPerSecond,
            Fix recoveryPerSecond, Fix restartStamina)
        {
            if (speedMultiplier < Fix.One || speedMultiplier > Fix.FromInt(4)
                || drainPerSecond <= Fix.Zero || recoveryPerSecond <= Fix.Zero
                || restartStamina <= Fix.Zero || restartStamina > Fix.One)
            {
                throw new ArgumentOutOfRangeException(nameof(speedMultiplier));
            }
            SpeedMultiplier = speedMultiplier;
            DrainPerSecond = drainPerSecond;
            RecoveryPerSecond = recoveryPerSecond;
            RestartStamina = restartStamina;
        }

        // 默认 1.5 倍移速、约4秒耗尽、约5秒回满；耗尽后松键且恢复25%才能重新快跑。
        public static PlayerSprintConfig Default => new PlayerSprintConfig(
            Fix.FromRatio(3, 2), Fix.FromRatio(1, 4), Fix.FromRatio(1, 5), Fix.FromRatio(1, 4));
    }
}

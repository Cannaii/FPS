using System;
using AFPS.Math;

namespace AFPS.Simulation.Characters
{
    /// <summary>跳跃能力规则；两次跳跃共用JumpSpeedFixed，不额外消耗体力。</summary>
    public readonly struct PlayerJumpConfig
    {
        public readonly Fix CoyoteDuration; // 自然离地后的宽限秒数，[0,0.5]；零禁用宽限。
        public readonly int ExtraAirJumps; // 每次落地后可用的额外空中跳次数，[0,8]；零禁用双跳。

        public PlayerJumpConfig(Fix coyoteDuration, int extraAirJumps)
        {
            if (coyoteDuration < Fix.Zero || coyoteDuration > Fix.Half
                || extraAirJumps < 0 || extraAirJumps > 8)
            {
                throw new ArgumentOutOfRangeException(nameof(coyoteDuration));
            }
            CoyoteDuration = coyoteDuration;
            ExtraAirJumps = extraAirJumps;
        }

        public static PlayerJumpConfig Default => new PlayerJumpConfig(Fix.FromRatio(1, 10), 1);
    }
}

using System;
using AFPS.Math;
using AFPS.Simulation.Characters.Collision;

namespace AFPS.Simulation.Characters
{
    public readonly partial struct PlayerSimulationConfig
    {
        /// <summary>最大地面速度，米/秒，非负。</summary>
        public readonly Fix MaxGroundSpeedFixed;
        /// <summary>水平加速度，米/秒²，非负；当前空中也沿用此配置。</summary>
        public readonly Fix GroundAccelerationFixed;
        /// <summary>重力加速度大小，米/秒²，非负；积分时沿 Y 负方向施加。</summary>
        public readonly Fix GravityFixed;
        /// <summary>起跳时的竖直速度，米/秒，沿 Y 正方向。</summary>
        public readonly Fix JumpSpeedFixed;
        public readonly CharacterCollisionConfig Collision;
        public readonly PlayerSprintConfig Sprint;
        public readonly PlayerDashConfig Dash;
        public readonly PlayerJumpConfig Jump;
        public PlayerSimulationConfig(
            Fix maxGroundSpeed,
            Fix groundAcceleration,
            Fix gravity,
            Fix jumpSpeed,
            CharacterCollisionConfig? collision = null,
            PlayerSprintConfig? sprint = null,
            PlayerDashConfig? dash = null,
            PlayerJumpConfig? jump = null)
        {
            if (maxGroundSpeed.Raw < 0 || groundAcceleration.Raw < 0 || gravity.Raw < 0 || jumpSpeed.Raw < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxGroundSpeed));
            }

            MaxGroundSpeedFixed = maxGroundSpeed;
            GroundAccelerationFixed = groundAcceleration;
            GravityFixed = gravity;
            JumpSpeedFixed = jumpSpeed;
            Collision = collision ?? CharacterCollisionConfig.Default;
            Sprint = sprint ?? PlayerSprintConfig.Default;
            PlayerDashConfig dashValue = dash ?? PlayerDashConfig.Default;
            // 重建校验也拒绝default(PlayerDashConfig)，不能让零配置绕过构造约束。
            Dash = new PlayerDashConfig(dashValue.Speed, dashValue.Duration,
                dashValue.RechargeDuration, dashValue.MaxCharges, dashValue.StaminaCost);
            PlayerJumpConfig jumpValue = jump ?? PlayerJumpConfig.Default;
            Jump = new PlayerJumpConfig(jumpValue.CoyoteDuration, jumpValue.ExtraAirJumps);
            if (Sprint.SpeedMultiplier < Fix.One || Sprint.DrainPerSecond <= Fix.Zero
                || Sprint.RecoveryPerSecond <= Fix.Zero || Sprint.RestartStamina <= Fix.Zero
                || Sprint.RestartStamina > Fix.One
                || (long)maxGroundSpeed.Raw * Sprint.SpeedMultiplier.Raw > (long)int.MaxValue * Fix.Scale)
            {
                throw new ArgumentOutOfRangeException(nameof(sprint));
            }
        }
    }
}

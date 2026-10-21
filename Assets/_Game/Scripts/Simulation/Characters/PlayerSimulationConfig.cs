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
        public PlayerSimulationConfig(
            Fix maxGroundSpeed,
            Fix groundAcceleration,
            Fix gravity,
            Fix jumpSpeed,
            CharacterCollisionConfig? collision = null)
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
        }
    }
}

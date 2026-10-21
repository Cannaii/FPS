using System;
using AFPS.Math;
using AFPS.Simulation.Characters.Collision;

namespace AFPS.Simulation.Characters
{
    public readonly partial struct PlayerSimulationConfig
    {
        public readonly Fix MaxGroundSpeedFixed;
        public readonly Fix GroundAccelerationFixed;
        public readonly Fix GravityFixed;
        public readonly Fix JumpSpeedFixed;
        public readonly CharacterCollisionConfig Collision;
        public PlayerSimulationConfig(Fix maxGroundSpeed, Fix groundAcceleration, Fix gravity, Fix jumpSpeed, CharacterCollisionConfig? collision = null)
        {
            if (maxGroundSpeed.Raw < 0 || groundAcceleration.Raw < 0 || gravity.Raw < 0 || jumpSpeed.Raw < 0) throw new ArgumentOutOfRangeException(nameof(maxGroundSpeed));
            MaxGroundSpeedFixed = maxGroundSpeed;
            GroundAccelerationFixed = groundAcceleration;
            GravityFixed = gravity;
            JumpSpeedFixed = jumpSpeed;
            Collision = collision ?? CharacterCollisionConfig.Default;
        }
    }
}

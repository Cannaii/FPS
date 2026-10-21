using AFPS.Math;
using AFPS.Simulation.Characters.Collision;

namespace AFPS.Simulation.Characters
{
    public readonly partial struct PlayerSimulationConfig
    {
        public float MaxGroundSpeed => MaxGroundSpeedFixed.ToFloat();
        public float GroundAcceleration => GroundAccelerationFixed.ToFloat();
        public float Gravity => GravityFixed.ToFloat();
        public float JumpSpeed => JumpSpeedFixed.ToFloat();

        public PlayerSimulationConfig(
            float maxGroundSpeed,
            float groundAcceleration,
            float gravity,
            float jumpSpeed,
            CharacterCollisionConfig? collision = null) : this(Fix.FromFloat(maxGroundSpeed), Fix.FromFloat(groundAcceleration), Fix.FromFloat(gravity), Fix.FromFloat(jumpSpeed), collision)
        {
        }
    }
}

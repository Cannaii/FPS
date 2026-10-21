using AFPS.Math;

namespace AFPS.Simulation.Characters.Collision
{
    public readonly partial struct CharacterCollisionConfig
    {
        public float Radius => RadiusFixed.ToFloat();
        public float Height => HeightFixed.ToFloat();
        public float SkinWidth => SkinWidthFixed.ToFloat();
        public float GroundProbeDistance => GroundProbeDistanceFixed.ToFloat();
        public float StepHeight => StepHeightFixed.ToFloat();
        public float MaxSlopeAngle => MaxSlopeAngleFixedDegrees.ToFloat();
        public float MinimumMoveDistance => MinimumMoveDistanceFixed.ToFloat();
        public float MinimumGroundNormalY => MinimumGroundNormalYFixed.ToFloat();
        public CharacterCollisionConfig(float radius, float height, float skinWidth, float groundProbeDistance, float stepHeight, float maxSlopeAngle, int maxSlideIterations)
            : this(Fix.FromFloat(radius), Fix.FromFloat(height), Fix.FromFloat(skinWidth), Fix.FromFloat(groundProbeDistance), Fix.FromFloat(stepHeight), Fix.FromFloat(maxSlopeAngle), maxSlideIterations) { }
    }
}

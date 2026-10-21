using System;
using AFPS.Math;

namespace AFPS.Simulation.Characters.Collision
{
    public readonly partial struct CharacterCollisionConfig
    {
        public readonly Fix RadiusFixed;
        public readonly Fix HeightFixed;
        public readonly Fix SkinWidthFixed;
        public readonly Fix GroundProbeDistanceFixed;
        public readonly Fix StepHeightFixed;
        public readonly Fix MaxSlopeAngleFixedDegrees;
        public readonly Fix MinimumGroundNormalYFixed;
        public readonly int MaxSlideIterations;
        public Fix MinimumMoveDistanceFixed => FixMath.Max(Fix.Epsilon, SkinWidthFixed * Fix.FromRaw(6554));
        public CharacterCollisionConfig(Fix radius, Fix height, Fix skinWidth, Fix groundProbeDistance, Fix stepHeight, Fix maxSlopeAngle, int maxSlideIterations)
        {
            if (radius <= Fix.Zero) throw new ArgumentOutOfRangeException(nameof(radius));
            if ((long)height.Raw < (long)radius.Raw * 2) throw new ArgumentOutOfRangeException(nameof(height));
            if (skinWidth.Raw < 0 || skinWidth >= radius) throw new ArgumentOutOfRangeException(nameof(skinWidth));
            if (groundProbeDistance.Raw < 0) throw new ArgumentOutOfRangeException(nameof(groundProbeDistance));
            if (stepHeight.Raw < 0) throw new ArgumentOutOfRangeException(nameof(stepHeight));
            if (maxSlopeAngle <= Fix.Zero || maxSlopeAngle >= Fix.FromInt(90)) throw new ArgumentOutOfRangeException(nameof(maxSlopeAngle));
            if (maxSlideIterations <= 0) throw new ArgumentOutOfRangeException(nameof(maxSlideIterations));
            RadiusFixed = radius; HeightFixed = height; SkinWidthFixed = skinWidth;
            GroundProbeDistanceFixed = groundProbeDistance; StepHeightFixed = stepHeight;
            MaxSlopeAngleFixedDegrees = maxSlopeAngle;
            MinimumGroundNormalYFixed = FixTrig.Cos(FixAngles.DegreesToRadians(maxSlopeAngle));
            MaxSlideIterations = maxSlideIterations;
        }
        public static CharacterCollisionConfig Default => new CharacterCollisionConfig(Fix.Half, Fix.FromInt(2), Fix.FromRaw(655), Fix.FromRaw(6554), Fix.FromRaw(19661), Fix.FromInt(50), 4);
    }
}

using System;
using AFPS.Math;

namespace AFPS.Simulation.Characters.Collision
{
    public readonly partial struct CharacterCollisionConfig
    {
        /// <summary>胶囊半径，米，必须大于零；位置原点在脚底。</summary>
        public readonly Fix RadiusFixed;
        /// <summary>胶囊总高度，米，包含两端半球，至少为半径的两倍。</summary>
        public readonly Fix HeightFixed;
        /// <summary>碰撞皮肤厚度，米，范围 [0, RadiusFixed)。</summary>
        public readonly Fix SkinWidthFixed;
        /// <summary>向下探地/吸附的最大距离，米，非负。</summary>
        public readonly Fix GroundProbeDistanceFixed;
        /// <summary>允许自动跨越的最大台阶高度，米；零表示关闭跨阶。</summary>
        public readonly Fix StepHeightFixed;
        /// <summary>可行走坡度上限，角度制，范围 (0, 90)。</summary>
        public readonly Fix MaxSlopeAngleFixedDegrees;
        /// <summary>单位地面法线的 Y 分量阈值，等于坡度上限的余弦。</summary>
        public readonly Fix MinimumGroundNormalYFixed;
        /// <summary>单 Tick 扫掠/滑动的最大迭代次数，防止墙角接触无限循环。</summary>
        public readonly int MaxSlideIterations;
        // 小于皮肤厚度约 1/10 的位移不再迭代；下限 1 Raw，避免定点零进展。
        public Fix MinimumMoveDistanceFixed => FixMath.Max(Fix.Epsilon, SkinWidthFixed * Fix.FromRaw(6554));

        public CharacterCollisionConfig(
            Fix radius,
            Fix height,
            Fix skinWidth,
            Fix groundProbeDistance,
            Fix stepHeight,
            Fix maxSlopeAngle,
            int maxSlideIterations)
        {
            if (radius <= Fix.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(radius));
            }

            if ((long)height.Raw < (long)radius.Raw * 2)
            {
                throw new ArgumentOutOfRangeException(nameof(height));
            }

            if (skinWidth.Raw < 0 || skinWidth >= radius)
            {
                throw new ArgumentOutOfRangeException(nameof(skinWidth));
            }

            if (groundProbeDistance.Raw < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(groundProbeDistance));
            }

            if (stepHeight.Raw < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(stepHeight));
            }

            if (maxSlopeAngle <= Fix.Zero || maxSlopeAngle >= Fix.FromInt(90))
            {
                throw new ArgumentOutOfRangeException(nameof(maxSlopeAngle));
            }

            if (maxSlideIterations <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxSlideIterations));
            }

            RadiusFixed = radius;
            HeightFixed = height;
            SkinWidthFixed = skinWidth;
            GroundProbeDistanceFixed = groundProbeDistance;
            StepHeightFixed = stepHeight;
            MaxSlopeAngleFixedDegrees = maxSlopeAngle;
            MinimumGroundNormalYFixed = FixTrig.Cos(FixAngles.DegreesToRadians(maxSlopeAngle));
            MaxSlideIterations = maxSlideIterations;
        }

        public static CharacterCollisionConfig Default =>
            new CharacterCollisionConfig(Fix.Half, Fix.FromInt(2), Fix.FromRaw(655), Fix.FromRaw(6554), Fix.FromRaw(19661), Fix.FromInt(50), 4);
    }
}

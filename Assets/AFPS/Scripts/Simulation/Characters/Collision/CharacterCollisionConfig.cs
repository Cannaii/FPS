using System;
using UnityEngine;

namespace AFPS.Simulation.Characters.Collision
{
    /// <summary>
    /// 客户端与服务器共同使用的角色胶囊和碰撞求解参数。
    /// </summary>
    public readonly struct CharacterCollisionConfig
    {
        public readonly float Radius;
        public readonly float Height;
        public readonly float SkinWidth;
        public readonly float GroundProbeDistance;
        public readonly float StepHeight;
        public readonly float MaxSlopeAngle;
        public readonly int MaxSlideIterations;

        public float MinimumMoveDistance => Mathf.Max(0.00001f, SkinWidth * 0.1f);
        public float MinimumGroundNormalY => Mathf.Cos(MaxSlopeAngle * Mathf.Deg2Rad);

        public CharacterCollisionConfig(float radius, float height, float skinWidth, float groundProbeDistance, float stepHeight, float maxSlopeAngle, int maxSlideIterations)
        {
            if (radius <= 0f || float.IsNaN(radius) || float.IsInfinity(radius))
            {
                throw new ArgumentOutOfRangeException(nameof(radius));
            }

            if (height < radius * 2f || float.IsNaN(height) || float.IsInfinity(height))
            {
                throw new ArgumentOutOfRangeException(nameof(height));
            }

            if (skinWidth < 0f || skinWidth >= radius || float.IsNaN(skinWidth) || float.IsInfinity(skinWidth))
            {
                throw new ArgumentOutOfRangeException(nameof(skinWidth));
            }

            if (groundProbeDistance < 0f || float.IsNaN(groundProbeDistance) || float.IsInfinity(groundProbeDistance))
            {
                throw new ArgumentOutOfRangeException(nameof(groundProbeDistance));
            }

            if (stepHeight < 0f || float.IsNaN(stepHeight) || float.IsInfinity(stepHeight))
            {
                throw new ArgumentOutOfRangeException(nameof(stepHeight));
            }

            if (maxSlopeAngle <= 0f || maxSlopeAngle >= 90f || float.IsNaN(maxSlopeAngle))
            {
                throw new ArgumentOutOfRangeException(nameof(maxSlopeAngle));
            }

            if (maxSlideIterations <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxSlideIterations));
            }

            Radius = radius;
            Height = height;
            SkinWidth = skinWidth;
            GroundProbeDistance = groundProbeDistance;
            StepHeight = stepHeight;
            MaxSlopeAngle = maxSlopeAngle;
            MaxSlideIterations = maxSlideIterations;
        }

        public static CharacterCollisionConfig Default => new CharacterCollisionConfig(0.5f, 2f, 0.01f, 0.1f, 0.3f, 50f, 4);
    }
}

using System;
using AFPS.Math;

namespace AFPS.Simulation.Characters
{
    /// <summary>下蹲与滑铲规则；实际下蹲高度还必须位于胶囊直径与站立高度之间。</summary>
    public readonly struct PlayerStanceConfig
    {
        public readonly Fix Height; // 下蹲总高度，米，(0,10]；脚底位置不变。
        public readonly Fix SpeedMultiplier; // 下蹲行走速度比例，(0,1]。
        public readonly Fix SlideSpeed; // 触发滑铲后的水平速度，米/秒，(0,100]。
        public readonly Fix SlideMinimumSpeed; // 触发前水平速度门槛，米/秒，(0,SlideSpeed]。
        public readonly Fix SlideDuration; // 最长滑铲时间，秒，(0,3]。
        public readonly Fix SlideDeceleration; // 速度衰减，米/秒²，(0,100]。
        public readonly Fix SlideSteer; // 转向收敛速度，米/秒²，[0,100]。

        public static PlayerStanceConfig Default => new PlayerStanceConfig(
            Fix.FromRatio(6, 5), Fix.Half, Fix.FromInt(10), Fix.FromInt(6),
            Fix.FromRatio(7, 10), Fix.FromInt(8), Fix.FromInt(2));

        public PlayerStanceConfig(Fix height, Fix speedMultiplier, Fix slideSpeed,
            Fix slideMinimumSpeed, Fix slideDuration, Fix slideDeceleration, Fix slideSteer)
        {
            if (height <= Fix.Zero || height > Fix.FromInt(10)
                || speedMultiplier <= Fix.Zero || speedMultiplier > Fix.One
                || slideSpeed <= Fix.Zero || slideSpeed > Fix.FromInt(100)
                || slideMinimumSpeed <= Fix.Zero || slideMinimumSpeed > slideSpeed
                || slideDuration <= Fix.Zero || slideDuration > Fix.FromInt(3)
                || slideDeceleration <= Fix.Zero || slideDeceleration > Fix.FromInt(100)
                || slideSteer < Fix.Zero || slideSteer > Fix.FromInt(100))
            {
                throw new ArgumentOutOfRangeException(nameof(height));
            }
            Height = height;
            SpeedMultiplier = speedMultiplier;
            SlideSpeed = slideSpeed;
            SlideMinimumSpeed = slideMinimumSpeed;
            SlideDuration = slideDuration;
            SlideDeceleration = slideDeceleration;
            SlideSteer = slideSteer;
        }
    }
}

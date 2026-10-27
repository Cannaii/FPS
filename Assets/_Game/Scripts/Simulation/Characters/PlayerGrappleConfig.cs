using System;
using AFPS.Math;

namespace AFPS.Simulation.Characters
{
    public readonly struct PlayerGrappleConfig
    {
        public readonly Fix Range; // 从眼部发射的最大距离，米，[2,100]。
        public readonly Fix PullAcceleration; // 朝锚点轻拉，米/秒²，[0,100]。
        public readonly Fix ReelSpeed; // 持续卷绳速度，米/秒，[0,20]。
        public readonly Fix MaximumSpeed; // 摆荡总速度上限，米/秒，(0,100]。
        public readonly Fix CameraInfluence; // 瞄准方向在绳切平面的控制加速度，米/秒²，[0,100]。
        public readonly Fix BreakDistance; // 脚底积分点进入此半径时断绳，米，[0.25,Range)。
        public readonly Fix OriginHeight; // 站立发射眼高，米，[0,10]；按实际胶囊高度缩放/限制。

        public static PlayerGrappleConfig Default => new PlayerGrappleConfig(
            Fix.FromInt(30), Fix.FromInt(15), Fix.FromInt(2), Fix.FromInt(20),
            Fix.FromInt(8), Fix.FromRatio(3, 2), Fix.FromRatio(8, 5));

        public PlayerGrappleConfig(Fix range, Fix pullAcceleration, Fix reelSpeed, Fix maximumSpeed,
            Fix cameraInfluence, Fix breakDistance, Fix originHeight)
        {
            if (range < Fix.FromInt(2) || range > Fix.FromInt(100)
                || pullAcceleration < Fix.Zero || pullAcceleration > Fix.FromInt(100)
                || reelSpeed < Fix.Zero || reelSpeed > Fix.FromInt(20)
                || maximumSpeed <= Fix.Zero || maximumSpeed > Fix.FromInt(100)
                || cameraInfluence < Fix.Zero || cameraInfluence > Fix.FromInt(100)
                || breakDistance < Fix.FromRatio(1, 4) || breakDistance >= range
                || originHeight < Fix.Zero || originHeight > Fix.FromInt(10))
            {
                throw new ArgumentOutOfRangeException(nameof(range));
            }
            Range = range;
            PullAcceleration = pullAcceleration;
            ReelSpeed = reelSpeed;
            MaximumSpeed = maximumSpeed;
            CameraInfluence = cameraInfluence;
            BreakDistance = breakDistance;
            OriginHeight = originHeight;
        }
    }
}

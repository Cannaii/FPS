using System;
using AFPS.Math;

namespace AFPS.Simulation.Characters
{
    public readonly struct PlayerClimbConfig
    {
        public readonly Fix Speed; // 上下攀爬速度，米/秒，(0,20]。
        public readonly Fix Reach; // 胶囊半径外接梯距离，米，(0,1]。
        public readonly Fix ExitPushSpeed; // 跳离向外/顶端向内的水平速度，米/秒，(0,20]。
        public readonly Fix ReattachDelay; // 脱梯后的禁止重挂秒数，(0,5]。
        public readonly Fix FacingAlignment; // 面向梯面门槛，前向与内法线点积，[0,1]。
        public static PlayerClimbConfig Default => new PlayerClimbConfig(
            Fix.FromInt(3), Fix.FromRatio(1, 5), Fix.FromInt(3), Fix.FromRatio(1, 4), Fix.Half);

        public PlayerClimbConfig(Fix speed, Fix reach, Fix exitPushSpeed, Fix reattachDelay, Fix facingAlignment)
        {
            if (speed <= Fix.Zero || speed > Fix.FromInt(20) || reach <= Fix.Zero || reach > Fix.One
                || exitPushSpeed <= Fix.Zero || exitPushSpeed > Fix.FromInt(20)
                || reattachDelay <= Fix.Zero || reattachDelay > Fix.FromInt(5)
                || facingAlignment < Fix.Zero || facingAlignment > Fix.One)
            {
                throw new ArgumentOutOfRangeException(nameof(speed));
            }
            Speed = speed;
            Reach = reach;
            ExitPushSpeed = exitPushSpeed;
            ReattachDelay = reattachDelay;
            FacingAlignment = facingAlignment;
        }
    }
}

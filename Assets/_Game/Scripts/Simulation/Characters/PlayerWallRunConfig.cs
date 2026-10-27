using System;
using AFPS.Math;

namespace AFPS.Simulation.Characters
{
    /// <summary>墙跑数值规则；墙面标签与法线来自会话绑定的确定性世界。</summary>
    public readonly struct PlayerWallRunConfig
    {
        public readonly Fix Speed; // 墙切线目标/水平速度上限，米/秒，(0,100]；不钳掉起跳的竖直动量。
        public readonly Fix Acceleration; // 水平速度收敛，米/秒²，(0,100]。
        public readonly Fix Duration; // 同一次离地累计墙跑预算，秒，(0,5]；换墙/松键不刷新。
        public readonly Fix GravityMultiplier; // 墙跑期间重力比例，[0,1]。
        public readonly Fix AdhesionSpeed; // 朝墙的速度分量，米/秒，(0,10]，始终经过碰撞运动器。
        public readonly Fix MaximumFallSpeed; // 墙跑下坠限速，米/秒，[0,100]。
        public readonly Fix DrainPerSecond; // 每秒归一化体力消耗，(0,5]。
        public readonly Fix ProbeDistance; // 胶囊半径之外的贴墙射线余量，米，(0,1]。
        public readonly Fix MinimumAlignment; // 输入方向与墙切线的绝对点积门槛，(0,1]，包含边界。

        public static PlayerWallRunConfig Default => new PlayerWallRunConfig(
            Fix.FromInt(9), Fix.FromInt(30), Fix.FromRatio(3, 2), Fix.FromRatio(1, 4),
            Fix.FromInt(2), Fix.FromInt(2), Fix.FromRatio(1, 3), Fix.FromRatio(3, 20), Fix.FromRatio(1, 4));

        public PlayerWallRunConfig(Fix speed, Fix acceleration, Fix duration, Fix gravityMultiplier,
            Fix adhesionSpeed, Fix maximumFallSpeed, Fix drainPerSecond, Fix probeDistance, Fix minimumAlignment)
        {
            if (speed <= Fix.Zero || speed > Fix.FromInt(100)
                || acceleration <= Fix.Zero || acceleration > Fix.FromInt(100)
                || duration <= Fix.Zero || duration > Fix.FromInt(5)
                || gravityMultiplier < Fix.Zero || gravityMultiplier > Fix.One
                || adhesionSpeed <= Fix.Zero || adhesionSpeed > Fix.FromInt(10)
                || maximumFallSpeed < Fix.Zero || maximumFallSpeed > Fix.FromInt(100)
                || drainPerSecond <= Fix.Zero || drainPerSecond > Fix.FromInt(5)
                || probeDistance <= Fix.Zero || probeDistance > Fix.One
                || minimumAlignment <= Fix.Zero || minimumAlignment > Fix.One)
            {
                throw new ArgumentOutOfRangeException(nameof(speed));
            }
            Speed = speed;
            Acceleration = acceleration;
            Duration = duration;
            GravityMultiplier = gravityMultiplier;
            AdhesionSpeed = adhesionSpeed;
            MaximumFallSpeed = maximumFallSpeed;
            DrainPerSecond = drainPerSecond;
            ProbeDistance = probeDistance;
            MinimumAlignment = minimumAlignment;
        }
    }
}

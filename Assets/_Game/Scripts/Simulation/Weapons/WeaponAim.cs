using AFPS.Math;
using AFPS.Simulation.Characters;

namespace AFPS.Simulation.Weapons
{
    /// <summary>定点瞄准：世界 +Y 向上，Yaw 向右转，正 Pitch 向下看；角度单位为度。</summary>
    public static class WeaponAim
    {
        public static readonly Fix MaximumPitch = InputAngleGrid.PitchToFix(InputAngleGrid.MaximumPitch);
        private const int FullTurnRaw = 360 * (int)Fix.Scale;

        public static bool TryGetDirection(Fix yawDegrees, Fix pitchDegrees, out FixVec3 direction)
        {
            direction = default;
            // 与权威状态的规范角域一致；拒绝损坏状态，不在射击时静默回绕或钳位。
            if (yawDegrees.Raw < 0 || yawDegrees.Raw >= FullTurnRaw
                || pitchDegrees < -MaximumPitch || pitchDegrees > MaximumPitch)
            {
                return false;
            }

            Fix yaw = FixAngles.DegreesToRadians(yawDegrees);
            Fix pitch = FixAngles.DegreesToRadians(pitchDegrees);
            Fix cosPitch = FixTrig.Cos(pitch);
            // 固定 LUT 和整数归一化消除 Quaternion/浮点三角函数的运行时差异。
            direction = new FixVec3(
                FixTrig.Sin(yaw) * cosPitch,
                -FixTrig.Sin(pitch),
                FixTrig.Cos(yaw) * cosPitch).Normalized();
            return true;
        }
    }
}

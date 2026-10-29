using AFPS.Math;

namespace AFPS.Simulation.Characters
{
    /// <summary>只处理输入、资源和计时；位移始终经过主运动器的碰撞，不提供无敌帧。</summary>
    public static class PlayerDashSimulation
    {
        public static bool Begin(ref PlayerMovementState next, in PlayerInputCommand input,
            in PlayerDashConfig config, in FixVec3 worldMove, in FixVec3 forward, Fix deltaTime)
        {
            // 串行恢复充能，大步长也保留跨越多个恢复周期的余量；不依赖绝对Tick，支持uint回绕。
            Fix remainingTime = deltaTime;
            while (next.DashSpentCharges > 0 && remainingTime >= next.DashRechargeRemainingFixed)
            {
                remainingTime -= next.DashRechargeRemainingFixed;
                next.DashSpentCharges--;
                next.DashRechargeRemainingFixed = next.DashSpentCharges > 0 ? config.RechargeDuration : Fix.Zero;
            }
            if (next.DashSpentCharges > 0)
            {
                next.DashRechargeRemainingFixed -= remainingTime;
            }

            bool active = next.DashRemainingFixed > Fix.Zero;
            if (!active && input.DashPressed && next.DashSpentCharges < config.MaxCharges
                && Fix.One - next.StaminaSpentFixed >= config.StaminaCost)
            {
                // 微小摇杆输入也使用完整速度；无方向时沿当前Yaw前冲，Pitch不影响水平冲刺。
                FixVec3 direction = worldMove == FixVec3.Zero ? forward.Normalized() : worldMove.Normalized();
                next.DashVelocityFixed = direction * config.Speed;
                next.DashRemainingFixed = config.Duration;
                if (next.DashSpentCharges == 0)
                {
                    next.DashRechargeRemainingFixed = config.RechargeDuration;
                }
                next.DashSpentCharges++;
                next.StaminaSpentFixed += config.StaminaCost;
                next.SprintExhausted |= next.StaminaSpentFixed == Fix.One;
                active = true;
            }
            return active;
        }

        public static void Complete(ref PlayerMovementState next, Fix deltaTime)
        {
            // 末Tick仍完整积分，持续时间向上取整到模拟Tick；之后由正常加速度收敛余速。
            next.DashRemainingFixed = FixMath.Max(Fix.Zero, next.DashRemainingFixed - deltaTime);
            if (next.DashRemainingFixed == Fix.Zero)
            {
                next.DashVelocityFixed = FixVec3.Zero;
            }
        }

        public static void Cancel(ref PlayerMovementState state)
        {
            // 传送/死亡只终止冲刺，不返还体力或充能，避免利用状态切换刷新资源。
            state.DashRemainingFixed = Fix.Zero;
            state.DashVelocityFixed = FixVec3.Zero;
        }
    }
}

using AFPS.Math;

namespace AFPS.Simulation.Characters
{
    /// <summary>纯数值快跑能力；碰撞由主积分器处理，本能力不查询几何、不持有全局状态。</summary>
    public static class PlayerSprintSimulation
    {
        public static bool CanSprint(in PlayerState previous, in PlayerInputCommand input, in FixVec3 move) =>
            input.SprintHeld && move.Z > Fix.Zero && previous.IsGrounded && !input.JumpPressed
            && !previous.SprintExhausted && previous.StaminaSpentFixed < Fix.One;

        public static PlayerState Complete(PlayerState next, in PlayerState previous,
            in PlayerInputCommand input, in PlayerSprintConfig config, Fix tickDeltaTime, bool requested)
        {
            // 只有实际落地移动才扣体力；乘法沿用Fix半格远离零舍入，体力最终夹到[0,1]。
            next.IsSprinting = requested && next.IsGrounded
                && (next.PositionFixed.X != previous.PositionFixed.X || next.PositionFixed.Z != previous.PositionFixed.Z);
            if (next.IsSprinting)
            {
                next.StaminaSpentFixed = FixMath.Min(Fix.One,
                    next.StaminaSpentFixed + ChangePerTick(config.DrainPerSecond, tickDeltaTime));
                if (next.StaminaSpentFixed == Fix.One)
                {
                    next.SprintExhausted = true;
                    next.IsSprinting = false;
                }
            }
            else
            {
                next.StaminaSpentFixed = FixMath.Max(Fix.Zero,
                    next.StaminaSpentFixed - ChangePerTick(config.RecoveryPerSecond, tickDeltaTime));
                if (!input.SprintHeld && Fix.One - next.StaminaSpentFixed >= config.RestartStamina)
                {
                    next.SprintExhausted = false;
                }
            }
            return next;
        }

        private static Fix ChangePerTick(Fix rate, Fix tickDeltaTime)
        {
            // 正数乘法采用与Fix相同的半格远离零舍入；先用long饱和到1，再做加减。
            // 大步长/高消耗率不能在夹体力之前发生int溢出；正int乘积加半格仍在long范围内。
            long raw = ((long)rate.Raw * tickDeltaTime.Raw + Fix.Scale / 2) / Fix.Scale;
            return Fix.FromRaw((int)System.Math.Min(Fix.Scale, raw));
        }
    }
}

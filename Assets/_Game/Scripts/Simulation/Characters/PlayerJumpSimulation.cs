using AFPS.Math;

namespace AFPS.Simulation.Characters
{
    /// <summary>只消费落地状态和输入边沿，不进行几何查询；碰撞结果由主运动器提供。</summary>
    public static class PlayerJumpSimulation
    {
        public static bool TryJump(ref PlayerMovementState next, in PlayerInputCommand input,
            in PlayerJumpConfig config, bool dashing)
        {
            if (next.IsGrounded)
            {
                ResetGrounded(ref next, config);
            }
            if (dashing || !input.JumpPressed)
            {
                return false;
            }

            // 主动起跳必须烧掉地面机会及宽限，否则首个空中边沿会被误当作免费地面跳。
            if (next.IsGrounded || (!next.GroundJumpConsumed && next.CoyoteRemainingFixed > Fix.Zero))
            {
                next.GroundJumpConsumed = true;
                next.CoyoteRemainingFixed = Fix.Zero;
                return true;
            }
            if (next.AirJumpsUsed >= config.ExtraAirJumps)
            {
                return false;
            }

            next.AirJumpsUsed++;
            next.GroundJumpConsumed = true;
            next.CoyoteRemainingFixed = Fix.Zero;
            return true;
        }

        public static void Complete(ref PlayerMovementState next, in PlayerJumpConfig config, Fix deltaTime)
        {
            if (next.IsGrounded)
            {
                // 必须消费碰撞后的真实落地，空中Dash/撞顶/传送不能伪造一次落地重置。
                ResetGrounded(ref next, config);
            }
            else
            {
                // 保存当前Tick后的剩余秒数；严格大于零才可用，不依赖绝对Tick编号。
                next.CoyoteRemainingFixed = FixMath.Max(Fix.Zero, next.CoyoteRemainingFixed - deltaTime);
            }
        }

        public static void ResetGrounded(ref PlayerMovementState state, in PlayerJumpConfig config)
        {
            state.CoyoteRemainingFixed = config.CoyoteDuration;
            state.GroundJumpConsumed = false;
            state.AirJumpsUsed = 0;
        }

        public static void CancelGroundOpportunity(ref PlayerMovementState state)
        {
            // 普通传送保留已用空中跳，不授予新的宽限；真实落地后才会补充跳跃资源。
            state.CoyoteRemainingFixed = Fix.Zero;
            state.GroundJumpConsumed = true;
        }
    }
}

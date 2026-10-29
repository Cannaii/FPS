using AFPS.Math;

namespace AFPS.Simulation.Characters
{
    /// <summary>纯墙能力；只读取上一Tick已确认的墙法线与碰撞前速度，不自行查询几何。</summary>
    public static class PlayerWallSimulation
    {
        public static void AdvanceTimers(ref PlayerMovementState next, Fix deltaTime)
        {
            next.WallCooldownRemainingFixed = FixMath.Max(Fix.Zero, next.WallCooldownRemainingFixed - deltaTime);
        }

        public static bool TryBounce(ref PlayerMovementState next, in PlayerMovementState previous,
            in PlayerInputCommand input, in PlayerWallConfig config)
        {
            if (!input.WallBouncePressed || !CanAct(next, previous, config))
            {
                return false;
            }
            FixVec3 normal = previous.WallNormalFixed;
            // 先限制输入速度再做点积/反射；极端下坠速度不能使Q16.16中间结果溢出。
            FixVec3 incoming = FixVec3.ClampMagnitude(previous.WallIncomingVelocityFixed, config.MaximumBounceSpeed);
            Fix normalSpeed = FixVec3.Dot(incoming, normal);
            FixVec3 reflected = normalSpeed < Fix.Zero
                ? incoming - normal * (Fix.FromInt(2) * normalSpeed) : incoming;
            Fix outward = FixMath.Clamp(FixVec3.Dot(reflected, normal), config.PushSpeed, config.MaximumBounceSpeed);
            FixVec3 tangent = reflected - normal * FixVec3.Dot(reflected, normal);
            Fix tangentLimit = FixMath.Sqrt(FixMath.Max(Fix.Zero,
                config.MaximumBounceSpeed * config.MaximumBounceSpeed - outward * outward));
            next.VelocityFixed = normal * outward + FixVec3.ClampMagnitude(tangent, tangentLimit);
            Spend(ref next, config);
            PlayerDashSimulation.Cancel(ref next);
            return true;
        }

        public static bool TryJump(ref PlayerMovementState next, in PlayerMovementState previous,
            in PlayerInputCommand input, in PlayerWallConfig config)
        {
            if (!input.JumpPressed || !CanAct(next, previous, config))
            {
                return false;
            }
            FixVec3 normal = previous.WallNormalFixed;
            FixVec3 horizontal = FixVec3.ClampMagnitude(new FixVec3(
                previous.VelocityFixed.X, Fix.Zero, previous.VelocityFixed.Z), config.MaximumBounceSpeed);
            FixVec3 tangent = horizontal - normal * FixVec3.Dot(horizontal, normal);
            horizontal = FixVec3.ClampMagnitude(tangent + normal * config.PushSpeed, config.MaximumBounceSpeed);
            next.VelocityFixed = new FixVec3(horizontal.X, config.UpSpeed, horizontal.Z);
            Spend(ref next, config);
            return true;
        }

        private static bool CanAct(in PlayerMovementState next, in PlayerMovementState previous, in PlayerWallConfig config) =>
            !previous.IsGrounded && previous.WallNormalFixed != FixVec3.Zero
            && next.WallCooldownRemainingFixed == Fix.Zero
            && Fix.One - next.StaminaSpentFixed >= config.StaminaCost;

        private static void Spend(ref PlayerMovementState next, in PlayerWallConfig config)
        {
            next.StaminaSpentFixed += config.StaminaCost;
            next.SprintExhausted |= next.StaminaSpentFixed == Fix.One;
            next.WallCooldownRemainingFixed = config.Cooldown;
            next.IsGrounded = false;
            next.IsSprinting = false;
            next.GroundJumpConsumed = true;
            next.CoyoteRemainingFixed = Fix.Zero;
            // 墙能力不刷新额外空中跳；落地重置仍只由原跳跃状态机执行。
        }

        public static void ClearContact(ref PlayerMovementState state)
        {
            state.WallNormalFixed = FixVec3.Zero;
            state.WallIncomingVelocityFixed = FixVec3.Zero;
        }
    }
}

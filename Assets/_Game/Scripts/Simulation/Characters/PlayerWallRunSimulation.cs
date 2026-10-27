using AFPS.Math;
using AFPS.Simulation.Characters.Collision;
using AFPS.Simulation.World;

namespace AFPS.Simulation.Characters
{
    public static class PlayerWallRunSimulation
    {
        public static void Begin(ref PlayerState next, in PlayerState previous, in PlayerInputCommand input,
            in PlayerSimulationConfig config, IFixedCharacterCollisionWorld collisionWorld,
            FixVec3 worldMovement, Fix deltaTime, bool interrupted, out FixVec3 horizontalTarget)
        {
            next.IsWallRunning = false;
            next.WallRunUsedFixed = previous.IsGrounded ? Fix.Zero
                : FixMath.Clamp(previous.WallRunUsedFixed, Fix.Zero, config.WallRun.Duration);
            horizontalTarget = FixVec3.Zero;
            if (interrupted || previous.IsGrounded || next.IsGrounded || next.IsCrouched
                || !input.SprintHeld || input.MoveYFixed <= Fix.Zero || input.JumpPressed
                || previous.SprintExhausted || next.WallRunUsedFixed >= config.WallRun.Duration
                || previous.WallNormalFixed == FixVec3.Zero || !(collisionWorld is IWorldQuery query))
            {
                return;
            }
            long originY = (long)previous.PositionFixed.Y.Raw + config.Collision.HeightFixed.Raw / 2;
            if (originY < int.MinValue || originY > int.MaxValue)
            {
                return;
            }
            var origin = new FixVec3(previous.PositionFixed.X, Fix.FromRaw((int)originY), previous.PositionFixed.Z);
            var wall = query.Raycast(origin, -previous.WallNormalFixed,
                config.Collision.RadiusFixed + config.WallRun.ProbeDistance, surfaceMask: FixedSurface.Solid);
            // 先找实际最近阻挡再检查Wall标签，不能穿过未标记实体去借用后方墙面的资格。
            if (!wall.DidHit || wall.StartedInside || (wall.Surface & FixedSurface.Wall) == 0
                || FixMath.Abs(wall.Normal.Y) >= config.Collision.MinimumGroundNormalYFixed)
            {
                return;
            }
            FixVec3 normal = new FixVec3(wall.Normal.X, Fix.Zero, wall.Normal.Z).Normalized();
            FixVec3 tangent = FixVec3.Cross(normal, FixVec3.Up);
            Fix alignment = FixVec3.Dot(worldMovement.Normalized(), tangent);
            if (FixMath.Abs(alignment) < config.WallRun.MinimumAlignment)
            {
                return;
            }
            if (alignment < Fix.Zero)
            {
                tangent = -tangent;
            }
            // 宽Raw计算并饱和扣费，避免大步长先溢出；耗尽沿用Shift松键恢复锁存。
            long costRaw = ((long)config.WallRun.DrainPerSecond.Raw * deltaTime.Raw + Fix.Scale / 2) / Fix.Scale;
            Fix cost = Fix.FromRaw((int)System.Math.Min(Fix.Scale, costRaw));
            if (Fix.One - next.StaminaSpentFixed < cost)
            {
                next.SprintExhausted = true;
                return;
            }
            next.StaminaSpentFixed += cost;
            next.SprintExhausted |= next.StaminaSpentFixed == Fix.One;
            next.WallRunUsedFixed = Fix.FromRaw((int)System.Math.Min(config.WallRun.Duration.Raw,
                (long)next.WallRunUsedFixed.Raw + deltaTime.Raw));
            next.IsWallRunning = true;
            // 吸墙也只是速度目标，不能直接修正位置或绕过扫掠，以免穿过薄墙。
            horizontalTarget = tangent * config.WallRun.Speed - normal * config.WallRun.AdhesionSpeed;
        }

        public static void Complete(ref PlayerState next, in PlayerWallRunConfig config)
        {
            if (next.IsGrounded)
            {
                next.WallRunUsedFixed = Fix.Zero;
            }
            if (next.IsGrounded || next.WallNormalFixed == FixVec3.Zero || next.WallRunUsedFixed >= config.Duration)
            {
                next.IsWallRunning = false;
            }
        }
    }
}

using AFPS.Math;
using AFPS.Simulation.Characters.Collision;
using AFPS.Simulation.World;

namespace AFPS.Simulation.Characters
{
    public static class PlayerClimbSimulation
    {
        public static void AdvanceTimers(ref PlayerState state, Fix deltaTime)
        {
            state.ClimbCooldownRemainingFixed = FixMath.Max(Fix.Zero, state.ClimbCooldownRemainingFixed - deltaTime);
        }

        public static bool TryJump(ref PlayerState next, in PlayerState previous, in PlayerInputCommand input,
            in PlayerSimulationConfig config)
        {
            if (!previous.IsClimbing || !input.JumpPressed)
            {
                return false;
            }
            next.VelocityFixed = previous.ClimbNormalFixed * config.Climb.ExitPushSpeed + FixVec3.Up * config.JumpSpeedFixed;
            Detach(ref next, config.Climb);
            next.IsGrounded = false;
            next.GroundJumpConsumed = true;
            next.CoyoteRemainingFixed = Fix.Zero;
            return true;
        }

        public static FixedLadderContact Begin(ref PlayerState next, in PlayerState previous,
            in PlayerInputCommand input, in PlayerSimulationConfig config,
            IFixedCharacterCollisionWorld collisionWorld, FixVec3 forward, bool interrupted)
        {
            Clear(ref next);
            if (interrupted || next.IsCrouched || input.JumpPressed || input.MoveXFixed != Fix.Zero
                || next.ClimbCooldownRemainingFixed > Fix.Zero || !(collisionWorld is IWorldQuery query))
            {
                if (previous.IsClimbing)
                {
                    Detach(ref next, config.Climb);
                }
                return default;
            }
            if (!previous.IsClimbing && input.MoveYFixed == Fix.Zero)
            {
                return default;
            }
            var ladder = query.LadderCheck(previous.PositionFixed, config.Collision.RadiusFixed,
                config.Collision.HeightFixed, config.Climb.Reach, previous.ClimbShapeId);
            if (!ladder.HasLadder || FixVec3.Dot(forward, -ladder.Normal) < config.Climb.FacingAlignment)
            {
                if (previous.IsClimbing)
                {
                    Detach(ref next, config.Climb);
                }
                return default;
            }
            // 从身体与梯盒的共同高度发射遮挡查询；临近梯顶时不能从梯盒上方漏射。
            long feetY = previous.PositionFixed.Y.Raw;
            long y = System.Math.Max(feetY, ladder.Bounds.Min.Y.Raw);
            long top = System.Math.Min(feetY + config.Collision.HeightFixed.Raw, ladder.Bounds.Max.Y.Raw);
            y = System.Math.Max(y, System.Math.Min(top - 1, feetY + config.Collision.HeightFixed.Raw / 2));
            if (y < int.MinValue || y > int.MaxValue)
            {
                return default;
            }
            var origin = new FixVec3(previous.PositionFixed.X, Fix.FromRaw((int)y), previous.PositionFixed.Z);
            var hit = query.Raycast(origin, -ladder.Normal, config.Collision.RadiusFixed + config.Climb.Reach,
                surfaceMask: FixedSurface.Solid | FixedSurface.Ladder);
            if (!hit.DidHit || hit.ShapeId != ladder.ShapeId)
            {
                if (previous.IsClimbing)
                {
                    Detach(ref next, config.Climb);
                }
                return default;
            }
            if (input.MoveYFixed < Fix.Zero && previous.PositionFixed.Y <= ladder.Bounds.Min.Y)
            {
                Detach(ref next, config.Climb);
                return default;
            }
            next.IsClimbing = true;
            next.ClimbShapeId = ladder.ShapeId;
            next.ClimbNormalFixed = ladder.Normal;
            next.IsGrounded = false;
            next.GroundJumpConsumed = true;
            next.CoyoteRemainingFixed = Fix.Zero;
            PlayerStanceSimulation.CancelSlide(ref next);
            return ladder;
        }

        public static void Complete(ref PlayerState next, in PlayerInputCommand input,
            in FixedLadderContact ladder, in PlayerClimbConfig config)
        {
            if (!next.IsClimbing)
            {
                return;
            }
            if (next.PositionFixed.Y >= ladder.Bounds.Max.Y)
            {
                next.VelocityFixed = -ladder.Normal * config.ExitPushSpeed;
                Detach(ref next, config);
            }
            else if (input.MoveYFixed < Fix.Zero && next.PositionFixed.Y <= ladder.Bounds.Min.Y)
            {
                next.VelocityFixed = FixVec3.Zero;
                Detach(ref next, config);
            }
            else
            {
                next.IsGrounded = false;
            }
        }

        public static void Detach(ref PlayerState state, in PlayerClimbConfig config)
        {
            Clear(ref state);
            state.ClimbCooldownRemainingFixed = config.ReattachDelay;
        }

        public static void Clear(ref PlayerState state)
        {
            state.IsClimbing = false;
            state.ClimbShapeId = 0;
            state.ClimbNormalFixed = FixVec3.Zero;
        }
    }
}

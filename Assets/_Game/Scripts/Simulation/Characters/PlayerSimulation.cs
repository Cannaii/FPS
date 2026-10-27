using System;
using AFPS.Math;
using AFPS.Simulation.Characters.Collision;

namespace AFPS.Simulation.Characters
{
    /// <summary>纯定点移动积分；固定步长与碰撞后端必须由调用方显式提供。</summary>
    public static partial class PlayerSimulation
    {
        public static PlayerState Simulate(
            in PlayerState previousState,
            in PlayerInputCommand input,
            in PlayerSimulationConfig config,
            Fix tickDeltaTime,
            IFixedCharacterCollisionWorld collisionWorld)
        {
            if (tickDeltaTime.Raw <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tickDeltaTime));
            }

            if (collisionWorld == null)
            {
                throw new ArgumentNullException(nameof(collisionWorld));
            }

            PlayerState next = previousState;
            next.Tick = input.Tick;
            next.YawFixedDegrees = input.LookYawFixedDegrees;
            next.PitchFixedDegrees = input.LookPitchFixedDegrees;
            // 先在定点域限制输入长度，避免斜向超速，以及采集端浮点归一化造成端间差异。
            FixVec3 move = FixVec3.ClampMagnitude(new FixVec3(input.MoveXFixed, Fix.Zero, input.MoveYFixed), Fix.One);
            Fix radians = FixAngles.DegreesToRadians(next.YawFixedDegrees);
            Fix sine = FixTrig.Sin(radians),
                cosine = FixTrig.Cos(radians);
            FixVec3 right = new FixVec3(cosine, Fix.Zero, -sine),
                forward = new FixVec3(sine, Fix.Zero, cosine);
            next.StaminaSpentFixed = FixMath.Clamp(previousState.StaminaSpentFixed, Fix.Zero, Fix.One);
            PlayerStanceSimulation.Begin(ref next, previousState, input, config, collisionWorld);
            PlayerWallSimulation.AdvanceTimers(ref next, tickDeltaTime);
            PlayerClimbSimulation.AdvanceTimers(ref next, tickDeltaTime);
            bool wallAction = PlayerWallSimulation.TryBounce(ref next, previousState, input, config.Wall);
            PlayerInputCommand dashInput = input;
            if (wallAction)
            {
                // 显式反弹优先，不能在同Tick再为被覆盖的Dash扣充能与体力。
                dashInput.DashPressed = false;
            }
            bool dashing = PlayerDashSimulation.Begin(ref next, dashInput, config.Dash,
                right * move.X + forward * move.Z, forward, tickDeltaTime);
            if (!dashing && !wallAction)
            {
                wallAction = PlayerClimbSimulation.TryJump(ref next, previousState, input, config)
                    || PlayerWallSimulation.TryJump(ref next, previousState, input, config.Wall);
            }
            bool sprintRequested = !next.IsCrouched && !dashing && !wallAction
                && PlayerSprintSimulation.CanSprint(previousState, input, move);
            Fix targetSpeed = sprintRequested
                ? config.MaxGroundSpeedFixed * config.Sprint.SpeedMultiplier : config.MaxGroundSpeedFixed;
            if (next.IsCrouched)
            {
                targetSpeed *= config.Stance.SpeedMultiplier;
            }
            FixVec3 target = (right * move.X + forward * move.Z) * targetSpeed;
            FixVec3 horizontal = new FixVec3(previousState.VelocityFixed.X, Fix.Zero, previousState.VelocityFixed.Z);
            Fix vertical = previousState.VelocityFixed.Y;
            if (wallAction)
            {
                horizontal = new FixVec3(next.VelocityFixed.X, Fix.Zero, next.VelocityFixed.Z);
                vertical = next.VelocityFixed.Y;
            }
            else if (PlayerJumpSimulation.TryJump(ref next, input, config.Jump, dashing))
            {
                vertical = config.JumpSpeedFixed;
                next.IsGrounded = false;
            }
            if (dashing || wallAction || !next.IsGrounded)
            {
                PlayerStanceSimulation.CancelSlide(ref next);
            }
            bool sliding = next.SlideRemainingFixed > Fix.Zero;
            bool grappling = PlayerGrappleSimulation.Begin(ref next, previousState, input, config,
                collisionWorld, dashing || wallAction || next.IsCrouched);
            if (grappling)
            {
                PlayerStanceSimulation.CancelSlide(ref next);
            }
            var ladder = PlayerClimbSimulation.Begin(ref next, previousState, input, config,
                collisionWorld, forward, dashing || wallAction || grappling);
            bool climbing = next.IsClimbing;
            sprintRequested &= !climbing && !grappling;
            PlayerWallRunSimulation.Begin(ref next, previousState, input, config, collisionWorld,
                right * move.X + forward * move.Z, tickDeltaTime,
                dashing || wallAction || climbing || grappling, out FixVec3 wallRunTarget);
            bool wallRunning = next.IsWallRunning;

            // 起跳当 Tick 也施加重力；此顺序属于预测与服务器共同遵守的积分规则。
            if (!dashing && !climbing && !next.IsGrounded)
            {
                Fix gravity = wallRunning ? config.GravityFixed * config.WallRun.GravityMultiplier : config.GravityFixed;
                vertical -= gravity * tickDeltaTime;
                if (wallRunning)
                {
                    vertical = FixMath.Max(-config.WallRun.MaximumFallSpeed, vertical);
                }
            }

            if (climbing)
            {
                horizontal = FixVec3.Zero;
                vertical = input.MoveYFixed * config.Climb.Speed;
            }
            else if (wallRunning)
            {
                horizontal = FixVec3.MoveTowards(horizontal, wallRunTarget, config.WallRun.Acceleration * tickDeltaTime);
                // 墙跑水平限速也覆盖刚结束的Dash余速；只限制水平量，保留起跳的竖直动量。
                horizontal = FixVec3.ClampMagnitude(horizontal, config.WallRun.Speed);
            }
            else if (sliding)
            {
                horizontal = PlayerStanceSimulation.Steer(next, right * move.X + forward * move.Z,
                    config.Stance, tickDeltaTime);
            }
            else if (!wallAction && !grappling)
            {
                horizontal = FixVec3.MoveTowards(horizontal, target, config.GroundAccelerationFixed * tickDeltaTime);
            }
            if (dashing)
            {
                // Dash优先于跳跃和快跑，期间冻结竖直速度；后续Yaw/方向输入不重定向。
                horizontal = next.DashVelocityFixed;
                vertical = Fix.Zero;
            }
            next.VelocityFixed = new FixVec3(horizontal.X, vertical, horizontal.Z);
            if (grappling)
            {
                PlayerGrappleSimulation.Apply(ref next, next.VelocityFixed, config, tickDeltaTime);
            }
            FixVec3 incoming = next.VelocityFixed;
            KinematicCharacterMotor.Move(
                previousState.PositionFixed,
                next.VelocityFixed,
                tickDeltaTime,
                PlayerStanceSimulation.Collision(next, config),
                collisionWorld,
                out next.PositionFixed,
                out next.VelocityFixed,
                out next.IsGrounded,
                out FixedCharacterContacts contacts);
            next.WallNormalFixed = contacts.HasWall ? contacts.WallNormal : FixVec3.Zero;
            next.WallIncomingVelocityFixed = contacts.HasWall ? incoming : FixVec3.Zero;
            PlayerClimbSimulation.Complete(ref next, input, ladder, config.Climb);
            PlayerGrappleSimulation.Complete(ref next, config.Grapple);
            next = PlayerSprintSimulation.Complete(next, previousState, input, config.Sprint,
                tickDeltaTime, sprintRequested, dashing || wallAction || wallRunning);
            PlayerDashSimulation.Complete(ref next, tickDeltaTime);
            PlayerJumpSimulation.Complete(ref next, config.Jump, tickDeltaTime);
            PlayerStanceSimulation.Complete(ref next, tickDeltaTime);
            PlayerWallRunSimulation.Complete(ref next, config.WallRun);
            return next;
        }
    }
}

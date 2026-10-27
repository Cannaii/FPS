using AFPS.Math;
using AFPS.Simulation.Characters.Collision;

namespace AFPS.Simulation.Characters
{
    public static class PlayerStanceSimulation
    {
        public static CharacterCollisionConfig Collision(in PlayerState state, in PlayerSimulationConfig config) =>
            state.IsCrouched ? config.Collision.WithHeight(config.Stance.Height) : config.Collision;

        public static Fix EyeHeight(in PlayerState state, Fix standingEyeHeight, in PlayerSimulationConfig config) =>
            state.IsCrouched ? standingEyeHeight * (config.Stance.Height / config.Collision.HeightFixed) : standingEyeHeight;

        public static void Begin(ref PlayerState next, in PlayerState previous, in PlayerInputCommand input,
            in PlayerSimulationConfig config, IFixedCharacterCollisionWorld world)
        {
            next.IsCrouched = input.CrouchHeld;
            if (!input.CrouchHeld && previous.IsCrouched)
            {
                // 松键后仍受顶棚约束；自动维持低姿态，直到原地站起的完整体积安全。
                next.IsCrouched = !KinematicCharacterMotor.CanResizeAtFeet(previous.PositionFixed,
                    Collision(previous, config), config.Collision.HeightFixed, world);
            }
            if (!next.IsCrouched || !input.CrouchHeld || !previous.IsGrounded)
            {
                CancelSlide(ref next);
                return;
            }
            if (!previous.IsCrouched)
            {
                FixVec3 horizontal = new FixVec3(previous.VelocityFixed.X, Fix.Zero, previous.VelocityFixed.Z);
                if (horizontal.Magnitude >= config.Stance.SlideMinimumSpeed)
                {
                    // 只在站立→主动下蹲时触发，不允许持续按住或顶棚强制下蹲反复续期。
                    next.SlideRemainingFixed = config.Stance.SlideDuration;
                    next.SlideVelocityFixed = horizontal.Normalized() * config.Stance.SlideSpeed;
                }
            }
        }

        public static FixVec3 Steer(in PlayerState next, FixVec3 direction, in PlayerStanceConfig config, Fix deltaTime)
        {
            Fix speed = FixMath.Max(Fix.Zero, next.SlideVelocityFixed.Magnitude - config.SlideDeceleration * deltaTime);
            FixVec3 target = direction == FixVec3.Zero ? next.SlideVelocityFixed : direction.Normalized() * speed;
            return FixVec3.ClampMagnitude(FixVec3.MoveTowards(
                next.SlideVelocityFixed, target, config.SlideSteer * deltaTime), speed);
        }

        public static void Complete(ref PlayerState next, Fix deltaTime)
        {
            if (next.SlideRemainingFixed <= Fix.Zero)
            {
                return;
            }
            next.SlideRemainingFixed = FixMath.Max(Fix.Zero, next.SlideRemainingFixed - deltaTime);
            next.SlideVelocityFixed = new FixVec3(next.VelocityFixed.X, Fix.Zero, next.VelocityFixed.Z);
            if (!next.IsGrounded || next.SlideRemainingFixed == Fix.Zero || next.SlideVelocityFixed == FixVec3.Zero)
            {
                CancelSlide(ref next);
            }
        }

        public static void CancelSlide(ref PlayerState state)
        {
            state.SlideRemainingFixed = Fix.Zero;
            state.SlideVelocityFixed = FixVec3.Zero;
        }
    }
}

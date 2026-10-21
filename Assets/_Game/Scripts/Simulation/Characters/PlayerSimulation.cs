using System;
using AFPS.Math;
using AFPS.Simulation.Characters.Collision;

namespace AFPS.Simulation.Characters
{
    /// <summary>纯定点移动积分；固定步长与碰撞后端必须由调用方显式提供。</summary>
    public static partial class PlayerSimulation
    {
        public static PlayerState Simulate(in PlayerState previousState, in PlayerInputCommand input, in PlayerSimulationConfig config, Fix tickDeltaTime, IFixedCharacterCollisionWorld collisionWorld)
        {
            if (tickDeltaTime.Raw <= 0) throw new ArgumentOutOfRangeException(nameof(tickDeltaTime));
            if (collisionWorld == null) throw new ArgumentNullException(nameof(collisionWorld));
            PlayerState next = previousState;
            next.Tick = input.Tick;
            next.YawFixedDegrees = input.LookYawFixedDegrees;
            next.PitchFixedDegrees = input.LookPitchFixedDegrees;
            FixVec3 move = FixVec3.ClampMagnitude(new FixVec3(input.MoveXFixed, Fix.Zero, input.MoveYFixed), Fix.One);
            Fix radians = FixAngles.DegreesToRadians(next.YawFixedDegrees);
            Fix sine = FixTrig.Sin(radians), cosine = FixTrig.Cos(radians);
            FixVec3 right = new FixVec3(cosine, Fix.Zero, -sine), forward = new FixVec3(sine, Fix.Zero, cosine);
            FixVec3 target = (right * move.X + forward * move.Z) * config.MaxGroundSpeedFixed;
            FixVec3 horizontal = new FixVec3(previousState.VelocityFixed.X, Fix.Zero, previousState.VelocityFixed.Z);
            Fix vertical = previousState.VelocityFixed.Y;
            if (input.JumpPressed && previousState.IsGrounded) { vertical = config.JumpSpeedFixed; next.IsGrounded = false; }
            if (!next.IsGrounded) vertical -= config.GravityFixed * tickDeltaTime;
            horizontal = FixVec3.MoveTowards(horizontal, target, config.GroundAccelerationFixed * tickDeltaTime);
            next.VelocityFixed = new FixVec3(horizontal.X, vertical, horizontal.Z);
            KinematicCharacterMotor.Move(previousState.PositionFixed, next.VelocityFixed, tickDeltaTime, config.Collision, collisionWorld, out next.PositionFixed, out next.VelocityFixed, out next.IsGrounded);
            return next;
        }
    }
}

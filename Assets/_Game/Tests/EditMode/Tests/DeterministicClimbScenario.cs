using System;
using AFPS.Math;
using AFPS.Simulation.Characters;
using AFPS.Simulation.Characters.Collision;
using AFPS.Simulation.World;

namespace AFPS.Tests.EditMode
{
    public static class DeterministicClimbScenario
    {
        public const ulong ExpectedHash = 11503603436642040718UL;

        public static ulong Run()
        {
            var config = new PlayerMovementConfig(Fix.FromInt(6), Fix.FromInt(30), Fix.FromInt(20), Fix.FromInt(8));
            var ladder = new FixedWorldBox(1, 0, FixedSurface.Ladder, new FixedAabb(
                new FixVec3(Fix.FromInt(2), Fix.Zero, Fix.FromInt(-1)),
                new FixVec3(Fix.FromRatio(21, 10), Fix.FromInt(3), Fix.One)));
            var floor = new FixedWorldPlane(2, 0, FixedSurface.Ground | FixedSurface.Solid, FixedAxis.Y, Fix.Zero);
            var world = new FixedCharacterCollisionWorld(new FixedWorld(new[] { ladder }, new[] { floor }));
            PlayerMovementState state = default;
            ulong hash = 14695981039346656037UL;
            bool climbed = false;
            bool held = false;
            bool topExit = false;
            bool bottomExit = false;
            bool jumped = false;
            for (int i = 0; i < 2048; i++)
            {
                int phase = i % 256;
                if (phase == 0 || phase == 140 || phase == 170 || phase == 200)
                {
                    state = new PlayerMovementState
                    {
                        PositionFixed = new FixVec3(Fix.FromRatio(3, 2),
                            phase == 0 ? Fix.Zero : phase == 200 ? Fix.FromRatio(1, 5) : Fix.One, Fix.Zero),
                        IsGrounded = phase == 0,
                        GroundJumpConsumed = phase != 0
                    };
                }
                var input = new PlayerInputCommand
                {
                    Tick = unchecked(uint.MaxValue - 1024 + (uint)i),
                    MoveY = phase >= 25 && phase < 40 ? 0f : phase >= 40 && phase < 50 || phase >= 200 ? -1f : 1f,
                    LookYaw = 90f,
                    JumpPressed = phase == 150,
                    DashPressed = phase == 180
                };
                var previous = state;
                state = PlayerMovementSimulation.Simulate(state, input, config, SimulationStep.Step50Hz, world);
                climbed |= state.IsClimbing && state.VelocityFixed.Y > Fix.Zero;
                held |= state.IsClimbing && phase >= 25 && phase < 40 && state.PositionFixed == previous.PositionFixed;
                topExit |= previous.IsClimbing && !state.IsClimbing && state.PositionFixed.Y >= Fix.FromInt(3);
                bottomExit |= previous.IsClimbing && !state.IsClimbing && state.PositionFixed.Y == Fix.Zero;
                jumped |= phase == 150 && state.VelocityFixed.X < Fix.Zero && state.VelocityFixed.Y > Fix.FromInt(7);
                ulong raw = PlayerMovementStateHash.Compute(state);
                for (int shift = 0; shift < 64; shift += 8)
                {
                    hash = unchecked((hash ^ ((raw >> shift) & 255UL)) * 1099511628211UL);
                }
            }
            if (!climbed || !held || !topExit || !bottomExit || !jumped)
            {
                throw new Exception("Climb replay did not cover climb/hold/top/bottom/jump.");
            }
            return hash;
        }
    }
}

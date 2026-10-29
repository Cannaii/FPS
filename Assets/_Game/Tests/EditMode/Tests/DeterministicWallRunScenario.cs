using System;
using AFPS.Math;
using AFPS.Simulation.Characters;
using AFPS.Simulation.Characters.Collision;
using AFPS.Simulation.World;

namespace AFPS.Tests.EditMode
{
    public static class DeterministicWallRunScenario
    {
        public const ulong ExpectedHash = 15642934158764997073UL;

        public static ulong Run()
        {
            var config = new PlayerMovementConfig(Fix.FromInt(6), Fix.FromInt(30), Fix.FromInt(20), Fix.FromInt(8));
            var wall = new FixedWorldBox(1, 0, FixedSurface.Solid | FixedSurface.Wall, new FixedAabb(
                new FixVec3(Fix.FromInt(2), Fix.Zero, Fix.FromInt(-100)),
                new FixVec3(Fix.FromInt(3), Fix.FromInt(40), Fix.FromInt(100))));
            var ground = new FixedWorldPlane(2, 0, FixedSurface.Solid | FixedSurface.Ground, FixedAxis.Y, Fix.Zero);
            var world = new FixedCharacterCollisionWorld(new FixedWorld(new[] { wall }, new[] { ground }));
            PlayerMovementState state = default;
            ulong hash = 14695981039346656037UL;
            bool ran = false;
            bool budgetEnded = false;
            for (int i = 0; i < 2048; i++)
            {
                int phase = i % 256;
                if (phase == 0)
                {
                    state = new PlayerMovementState
                    {
                        PositionFixed = new FixVec3(Fix.FromRatio(3, 2), Fix.FromInt(20), Fix.Zero),
                        WallNormalFixed = -FixVec3.Right,
                        GroundJumpConsumed = true
                    };
                }
                var input = new PlayerInputCommand
                {
                    Tick = unchecked(uint.MaxValue - 1024 + (uint)i),
                    MoveY = 1f,
                    SprintHeld = phase < 140 && (phase < 30 || phase >= 40),
                    JumpPressed = phase == 100,
                    DashPressed = phase == 110,
                    CrouchHeld = phase >= 150 && phase < 180
                };
                state = PlayerMovementSimulation.Simulate(state, input, config, SimulationStep.Step50Hz, world);
                ran |= state.IsWallRunning && state.StaminaSpentFixed > Fix.Zero;
                budgetEnded |= !state.IsWallRunning && state.WallRunUsedFixed == config.WallRun.Duration;
                ulong raw = PlayerMovementStateHash.Compute(state);
                for (int shift = 0; shift < 64; shift += 8)
                {
                    hash = unchecked((hash ^ ((raw >> shift) & 255UL)) * 1099511628211UL);
                }
            }
            if (!ran || !budgetEnded)
            {
                throw new Exception("Wall run replay did not cover active running and exhausted air budget.");
            }
            return hash;
        }
    }
}

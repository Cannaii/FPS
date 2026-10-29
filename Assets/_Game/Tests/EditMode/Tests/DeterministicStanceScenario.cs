using System;
using AFPS.Math;
using AFPS.Simulation.Characters;
using AFPS.Simulation.Characters.Collision;
using AFPS.Simulation.World;

namespace AFPS.Tests.EditMode
{
    public static class DeterministicStanceScenario
    {
        public const ulong ExpectedHash = 11685849721617599281UL;

        public static ulong Run()
        {
            var config = new PlayerMovementConfig(Fix.FromInt(6), Fix.FromInt(30), Fix.FromInt(20), Fix.FromInt(8));
            var roof = new FixedWorldBox(1, 0, FixedSurface.Solid, new FixedAabb(
                new FixVec3(Fix.FromInt(2), Fix.FromRatio(3, 2), Fix.FromInt(-5)),
                new FixVec3(Fix.FromInt(8), Fix.FromInt(3), Fix.FromInt(5))));
            var ground = new FixedWorldPlane(2, 0, FixedSurface.Solid | FixedSurface.Ground, FixedAxis.Y, Fix.Zero);
            var world = new FixedCharacterCollisionWorld(new FixedWorld(new[] { roof }, new[] { ground }));
            PlayerMovementState state = default;
            ulong hash = 14695981039346656037UL;
            bool slid = false;
            bool blocked = false;
            bool stood = false;
            for (int i = 0; i < 2048; i++)
            {
                int phase = i % 256;
                if (phase == 0)
                {
                    state = new PlayerMovementState { PositionFixed = -FixVec3.Right * Fix.FromInt(4), IsGrounded = true };
                }
                var input = new PlayerInputCommand
                {
                    Tick = unchecked(uint.MaxValue - 1024 + (uint)i),
                    SprintHeld = phase < 24,
                    CrouchHeld = phase >= 24 && phase < 90,
                    JumpPressed = phase == 200,
                    DashPressed = phase == 220
                };
                input.SetFixedControls(Fix.One, Fix.Zero, Fix.Zero, Fix.Zero);
                state = PlayerMovementSimulation.Simulate(state, input, config, SimulationStep.Step50Hz, world);
                slid |= state.SlideRemainingFixed > Fix.Zero;
                blocked |= phase >= 90 && state.IsCrouched;
                stood |= phase > 90 && !state.IsCrouched;
                ulong raw = PlayerMovementStateHash.Compute(state);
                for (int shift = 0; shift < 64; shift += 8)
                {
                    hash = unchecked((hash ^ ((raw >> shift) & 255UL)) * 1099511628211UL);
                }
            }
            if (!slid || !blocked || !stood)
            {
                throw new Exception("Stance replay did not cover sliding, blocked release and standing.");
            }
            return hash;
        }
    }
}

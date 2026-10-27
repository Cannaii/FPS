using System;
using AFPS.Math;
using AFPS.Simulation.Characters;
using AFPS.Simulation.Characters.Collision;
using AFPS.Simulation.World;

namespace AFPS.Tests.EditMode
{
    public static class DeterministicGrappleScenario
    {
        public const ulong ExpectedHash = 15583312201833629016UL;

        public static ulong Run()
        {
            var config = new PlayerSimulationConfig(Fix.FromInt(6), Fix.FromInt(30), Fix.FromInt(20), Fix.FromInt(8));
            var plane = new FixedWorldPlane(1, 0, FixedSurface.Solid | FixedSurface.Grapple,
                FixedAxis.Z, Fix.FromInt(12), positiveNormal: false);
            var floor = new FixedWorldPlane(2, 0, FixedSurface.Solid | FixedSurface.Ground, FixedAxis.Y, Fix.Zero);
            var world = new FixedCharacterCollisionWorld(new FixedWorld(null, new[] { plane, floor }));
            PlayerState state = default;
            ulong hash = 14695981039346656037UL;
            bool attached = false;
            bool reeled = false;
            bool swung = false;
            bool released = false;
            for (int i = 0; i < 2048; i++)
            {
                int phase = i % 128;
                if (phase == 0)
                {
                    state = new PlayerState
                    {
                        PositionFixed = FixVec3.Up * Fix.FromInt(5),
                        VelocityFixed = FixVec3.Right * Fix.FromInt(2), GroundJumpConsumed = true
                    };
                }
                var input = new PlayerInputCommand
                {
                    Tick = unchecked(uint.MaxValue - 1024 + (uint)i),
                    GrappleHeld = phase < 70 || phase >= 90 && phase < 100,
                    LookYaw = phase < 10 ? 0f : 45f,
                    LookPitch = phase < 10 ? -25f : 0f,
                    DashPressed = phase == 85
                };
                var previous = state;
                state = PlayerSimulation.Simulate(state, input, config, SimulationStep.Step50Hz, world);
                attached |= state.IsGrappling;
                reeled |= previous.IsGrappling && state.IsGrappling && state.GrappleLengthFixed < previous.GrappleLengthFixed;
                swung |= state.IsGrappling && state.PositionFixed.X > Fix.One;
                released |= phase == 70 && !state.IsGrappling && !state.GrappleConsumed;
                ulong raw = PlayerStateHash.Compute(state);
                for (int shift = 0; shift < 64; shift += 8)
                {
                    hash = unchecked((hash ^ ((raw >> shift) & 255UL)) * 1099511628211UL);
                }
            }
            if (!attached || !reeled || !swung || !released)
            {
                throw new Exception("Grapple replay did not cover anchor/reel/swing/release.");
            }
            return hash;
        }
    }
}

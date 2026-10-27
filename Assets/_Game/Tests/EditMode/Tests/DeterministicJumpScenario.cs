using AFPS.Math;
using AFPS.Simulation.Characters;
using AFPS.Simulation.Characters.Collision;

namespace AFPS.Tests.EditMode
{
    /// <summary>无Unity/NUnit的重放：落地复位、双跳拒绝、下坠跳、Dash优先级及Tick回绕。</summary>
    public static class DeterministicJumpScenario
    {
        public const ulong ExpectedHash = 5247468599979997174UL;

        public static ulong Run()
        {
            var config = new PlayerSimulationConfig(Fix.FromInt(6), Fix.FromInt(20), Fix.FromInt(20), Fix.FromInt(8));
            var state = new PlayerState { Tick = uint.MaxValue - 100, IsGrounded = true };
            ulong hash = 14695981039346656037UL;
            for (int i = 0; i < 2048; i++)
            {
                var input = new PlayerInputCommand
                {
                    Tick = unchecked(state.Tick + 1),
                    JumpPressed = i % 11 == 0,
                    DashPressed = i % 90 == 22,
                    SprintHeld = i % 150 < 80
                };
                input.SetFixedControls(Fix.Zero, Fix.One, Fix.FromInt(i / 128 % 4 * 90), Fix.Zero);
                state = PlayerSimulation.Simulate(state, input, config, SimulationStep.Step50Hz,
                    FixedFlatGroundCollisionWorld.Instance);
                ulong raw = PlayerStateHash.Compute(state);
                for (int shift = 0; shift < 64; shift += 8)
                {
                    hash = unchecked((hash ^ ((raw >> shift) & 255UL)) * 1099511628211UL);
                }
            }
            return hash;
        }
    }
}

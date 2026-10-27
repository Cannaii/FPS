using AFPS.Math;
using AFPS.Simulation.Characters;
using AFPS.Simulation.Characters.Collision;

namespace AFPS.Tests.EditMode
{
    /// <summary>跨运行时重放：空中/地面Dash、资源拒绝、快跑竞争、方向锁定及Tick回绕。</summary>
    public static class DeterministicDashScenario
    {
        public const ulong ExpectedHash = 6658677928207285667UL;

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
                    DashPressed = i % 17 == 0,
                    SprintHeld = i % 300 < 160,
                    JumpPressed = i % 70 == 5
                };
                input.SetFixedControls(i % 80 < 40 ? Fix.One : -Fix.One,
                    i % 90 < 60 ? Fix.One : Fix.Zero, Fix.FromInt(i / 29 % 4 * 90), Fix.Zero);
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

using AFPS.Math;
using AFPS.Simulation.Characters;
using AFPS.Simulation.Characters.Collision;

namespace AFPS.Tests.EditMode
{
    /// <summary>不依赖 Unity/NUnit 的能力重放，覆盖耗尽、恢复、转向、跳跃和输入Tick回绕。</summary>
    public static class DeterministicSprintScenario
    {
        public const ulong ExpectedHash = 11160452174145322228UL;
        public static ulong Run()
        {
            var config = new PlayerSimulationConfig(Fix.FromInt(6), Fix.FromInt(20), Fix.FromInt(20), Fix.FromInt(8));
            var state = new PlayerState { Tick = uint.MaxValue - 100, IsGrounded = true };
            ulong hash = 14695981039346656037UL;
            for (int i = 0; i < 2048; i++)
            {
                var command = new PlayerInputCommand
                {
                    Tick = unchecked(state.Tick + 1), SprintHeld = i % 400 < 240, JumpPressed = i % 400 == 300
                };
                command.SetFixedControls(Fix.Zero, Fix.One, Fix.FromInt(i / 128 % 4 * 90), Fix.Zero);
                state = PlayerSimulation.Simulate(state, command, config, SimulationStep.Step50Hz,
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

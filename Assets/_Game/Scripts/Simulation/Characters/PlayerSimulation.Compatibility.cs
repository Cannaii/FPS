using System;
using AFPS.Simulation.Characters.Collision;

namespace AFPS.Simulation.Characters
{
    public static partial class PlayerSimulation
    {
        // 旧测试/实验接口；新的网络 Tick 路径使用 Fix 步长及显式 IFixedCharacterCollisionWorld。
        public static PlayerState Simulate(in PlayerState state, in PlayerInputCommand input, in PlayerSimulationConfig config, float seconds) =>
            Simulate(state, input, config, SimulationStep.FromSeconds(seconds), FixedFlatGroundCollisionWorld.Instance);
        public static PlayerState Simulate(in PlayerState state, in PlayerInputCommand input, in PlayerSimulationConfig config, float seconds, ICharacterCollisionWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            return Simulate(state, input, config, SimulationStep.FromSeconds(seconds), LegacyCharacterCollisionWorldAdapter.Adapt(world));
        }
    }
}

using System;
using AFPS.Simulation.Characters.Collision;

namespace AFPS.Simulation.Characters
{
    public static partial class PlayerMovementSimulation
    {
        // 浮点秒仅作为旧调用边界；禁止省略world，以免漏接场景几何时静默落回无限平地。
        public static PlayerMovementState Simulate(
            in PlayerMovementState state,
            in PlayerInputCommand input,
            in PlayerMovementConfig config,
            float seconds,
            ICharacterCollisionWorld world)
        {
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            return Simulate(state, input, config, SimulationStep.FromSeconds(seconds), LegacyCharacterCollisionWorldAdapter.Adapt(world));
        }
    }
}

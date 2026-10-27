using AFPS.Math;
using AFPS.Simulation.Characters;
using AFPS.Simulation.Characters.Collision;
using AFPS.Simulation.World;

namespace AFPS.Tests.EditMode
{
    public static class DeterministicWallScenario
    {
        public const ulong ExpectedHash = 17692361390487258193UL;

        public static ulong Run()
        {
            var config = new PlayerSimulationConfig(Fix.FromInt(6), Fix.FromInt(20), Fix.FromInt(20), Fix.FromInt(8));
            var box = new FixedWorldBox(1, 0, FixedSurface.Solid, new FixedAabb(
                new FixVec3(Fix.FromInt(2), Fix.Zero, Fix.FromInt(-10)),
                new FixVec3(Fix.FromInt(3), Fix.FromInt(10), Fix.FromInt(10))));
            var world = new FixedCharacterCollisionWorld(new FixedWorld(new[] { box }));
            var state = new PlayerState { Tick = uint.MaxValue - 100 };
            ulong hash = 14695981039346656037UL;
            for (int i = 0; i < 2048; i++)
            {
                if (i % 100 == 0)
                {
                    // 重置测试姿态而非时间，覆盖真实碰撞生成接触后的墙跳、冲刺反弹和Tick回绕。
                    state.PositionFixed = new FixVec3(Fix.FromRatio(7, 5), Fix.FromInt(3), Fix.Zero);
                    state.VelocityFixed = FixVec3.Zero;
                    state.StaminaSpentFixed = Fix.Zero;
                    state.WallCooldownRemainingFixed = Fix.Zero;
                    PlayerWallSimulation.ClearContact(ref state);
                }
                var input = new PlayerInputCommand
                {
                    Tick = unchecked(state.Tick + 1),
                    DashPressed = i % 200 == 0,
                    WallBouncePressed = i % 200 == 1,
                    JumpPressed = i % 200 == 101
                };
                input.SetFixedControls(Fix.One, Fix.Zero, Fix.Zero, Fix.Zero);
                state = PlayerSimulation.Simulate(state, input, config, SimulationStep.Step50Hz, world);
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

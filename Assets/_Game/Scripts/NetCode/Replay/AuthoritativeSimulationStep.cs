using AFPS.Simulation.Characters;

namespace AFPS.NetCode.Replay
{
    /// <summary>服务器实际执行的一步；Previous已包含死亡/传送等外部修正，Input是最终应用的输入。</summary>
    public readonly struct AuthoritativeSimulationStep
    {
        public readonly uint EntityId;
        // 世界Tick可因等待输入/断线而留空；Input.Tick仍须逐步连续，二者均允许uint回绕。
        public readonly uint WorldTick;
        public readonly PlayerState Previous;
        public readonly PlayerInputCommand Input;
        public readonly PlayerState Result;

        public AuthoritativeSimulationStep(uint entityId, uint worldTick, PlayerState previous,
            PlayerInputCommand input, PlayerState result)
        {
            EntityId = entityId;
            WorldTick = worldTick;
            Previous = previous;
            Input = input;
            Result = result;
        }
    }
}

using AFPS.Math;

namespace AFPS.Simulation.Characters
{
    public static partial class SimulationStep
    {
        /// <summary>50Hz 固定模拟步长；调度时钟仍按 20ms Tick，数值积分统一使用 1311 Raw。</summary>
        public static readonly Fix Step50Hz = Fix.FromRaw(1311);
    }
}

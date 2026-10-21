using System;
using AFPS.Math;

namespace AFPS.Simulation.Characters
{
    public static partial class SimulationStep
    {
        /// <summary>会话/配置创建边界调用一次，Tick 热路径直接使用得到的 Fix。</summary>
        public static Fix FromSeconds(float seconds)
        {
            Fix value = Fix.FromFloat(seconds);
            if (value.Raw <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds));
            }

            return value;
        }
    }
}

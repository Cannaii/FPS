using AFPS.Core.Tick;
using UnityEngine;

namespace AFPS.Bootstrap
{
    /// <summary>
    /// 观察模拟 Tick 的运行情况。
    /// 该组件只用于学习和调试，不参与实际游戏模拟。
    /// </summary>
    public sealed class TickDebugObserver : MonoBehaviour
    {
        /// <summary>
        /// 当前需要观察的固定 Tick 驱动器。
        /// </summary>
        [SerializeField]
        private SimulationTickRunner tickRunner;

        /// <summary>
        /// 自上次输出日志后累计执行的 Tick 数量。
        /// </summary>
        private int tickCountSinceLastReport;

        /// <summary>
        /// 组件启用时订阅 Tick 事件。
        /// </summary>
        private void OnEnable()
        {
            if (tickRunner == null)
            {
                Debug.LogError(
                    "TickDebugObserver 没有引用 SimulationTickRunner。",
                    this);

                enabled = false;
                return;
            }

            tickRunner.TickOccurred += HandleTickOccurred;
        }

        /// <summary>
        /// 组件停用或销毁时取消订阅，避免对象失效后仍然接收事件。
        /// </summary>
        private void OnDisable()
        {
            if (tickRunner != null)
            {
                tickRunner.TickOccurred -= HandleTickOccurred;
            }
        }

        /// <summary>
        /// 每次模拟 Tick 发生时记录 Tick 数量。
        /// 达到约一秒所包含的 Tick 数量后输出一次日志。
        /// </summary>
        /// <param name="tick">当前执行的模拟 Tick 编号。</param>
        /// <param name="tickDeltaTime">单个模拟 Tick 的固定时长，单位为秒。</param>
        private void HandleTickOccurred(
            uint tick,
            float tickDeltaTime)
        {
            tickCountSinceLastReport++;

            // 根据固定 Tick 时长计算一秒应包含多少个 Tick。
            int expectedTicksPerSecond =
                Mathf.RoundToInt(1f / tickDeltaTime);

            if (tickCountSinceLastReport < expectedTicksPerSecond)
            {
                return;
            }

            float simulatedTime =
                tickCountSinceLastReport * tickDeltaTime;

            Debug.Log(
                $"当前 Tick：{tick}，" +
                $"本段执行数量：{tickCountSinceLastReport}，" +
                $"累计模拟时间：{simulatedTime:F3} 秒",
                this);

            tickCountSinceLastReport = 0;
        }
    }
}
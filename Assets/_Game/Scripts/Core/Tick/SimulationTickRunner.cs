using System;
using UnityEngine;

namespace AFPS.Core.Tick
{
    /// <summary>
    /// 以固定频率产生模拟 Tick。
    /// Tick 频率不依赖当前画面帧率，可用于驱动客户端预测和服务器模拟。
    /// </summary>
    public sealed class SimulationTickRunner : MonoBehaviour
    {
        /// <summary>
        /// 每秒执行的模拟 Tick 数量。
        /// 例如 50 表示每个 Tick 持续 0.02 秒。
        /// </summary>
        [SerializeField]
        [Min(1)]
        private int tickRate = 50;

        /// <summary>
        /// 尚未被模拟 Tick 消耗的累计时间，单位为秒。
        /// </summary>
        private double accumulator;

        /// <summary>
        /// 当前已经执行完成的模拟 Tick 编号。
        /// 第一次触发 Tick 时，该值为 1。
        /// </summary>
        public uint CurrentTick { get; private set; }

        /// <summary>
        /// 单个模拟 Tick 的固定持续时间，单位为秒。
        /// 例如 TickRate 为 50 时，该值为 0.02。
        /// </summary>
        public float TickDeltaTime => 1f / tickRate;

        /// <summary>
        /// 每当需要执行一个固定模拟 Tick 时触发。
        /// 参数依次为当前 Tick 编号和固定 Tick 时长。
        /// </summary>
        public event Action<uint, float> TickOccurred;

        /// <summary>
        /// 当前渲染时刻在两个模拟 Tick 之间的进度。
        /// 取值通常为 0 到 1：0 表示刚完成一个 Tick，
        /// 1 表示即将开始下一个 Tick。
        /// </summary>
        public float TickAlpha
        {
            get
            {
                double tickDeltaTime = 1.0 / tickRate;
                return Mathf.Clamp01(
                    (float)(accumulator / tickDeltaTime));
            }
        }

        private void Update()
        {
            // 累积当前渲染帧经过的真实时间。
            accumulator += Time.unscaledDeltaTime;

            double tickDeltaTime = 1.0 / tickRate;

            // 一帧可能积累了多个 Tick 的时间，因此这里必须使用 while。
            while (accumulator >= tickDeltaTime)
            {
                accumulator -= tickDeltaTime;

                CurrentTick++;

                TickOccurred?.Invoke(
                    CurrentTick,
                    (float)tickDeltaTime);
            }
        }

        
    }
}
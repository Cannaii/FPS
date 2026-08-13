using AFPS.Simulation.Characters;
using UnityEngine;

namespace AFPS.Input
{
    /// <summary>
    /// 采集本地玩家的键盘输入，并将其转换成每个模拟 Tick
    /// 可以消费的 PlayerInputCommand。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class LocalPlayerInputCollector : MonoBehaviour
    {
        /// <summary>
        /// 最近一次渲染帧采集到的水平移动输入。
        /// 推荐取值范围为 -1 到 1。
        /// </summary>
        private float moveX;

        /// <summary>
        /// 最近一次渲染帧采集到的前后移动输入。
        /// 推荐取值范围为 -1 到 1。
        /// </summary>
        private float moveY;

        /// <summary>
        /// 表示自上一个模拟 Tick 消费输入后，
        /// 玩家是否至少按下过一次跳跃键。
        /// </summary>
        private bool jumpPressedSinceLastTick;

        /// <summary>
        /// 每个渲染帧采集一次设备输入。
        /// </summary>
        private void Update()
        {
            moveX = UnityEngine.Input.GetAxisRaw("Horizontal");
            moveY = UnityEngine.Input.GetAxisRaw("Vertical");

            // 使用 |= 保留已经采集到但尚未被 Tick 消费的跳跃事件。
            jumpPressedSinceLastTick |=
                UnityEngine.Input.GetKeyDown(KeyCode.Space);
        }

        /// <summary>
        /// 为指定模拟 Tick 创建一条玩家输入命令。
        /// 跳跃输入被读取后会立即清除，避免同一次按键触发多个 Tick。
        /// </summary>
        /// <param name="tick">即将执行的模拟 Tick 编号。</param>
        /// <returns>当前 Tick 需要处理的玩家输入命令。</returns>
        public PlayerInputCommand ConsumeCommand(uint tick)
        {
            PlayerInputCommand command = new PlayerInputCommand
            {
                Tick = tick,
                MoveX = moveX,
                MoveY = moveY,
                JumpPressed = jumpPressedSinceLastTick
            };

            // 跳跃是一次性事件，生成命令后需要清除。
            jumpPressedSinceLastTick = false;

            return command;
        }
    }
}
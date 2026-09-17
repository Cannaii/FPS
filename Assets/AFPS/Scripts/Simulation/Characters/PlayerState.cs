
using UnityEngine;

namespace AFPS.Simulation.Characters
{
    /// <summary>
    /// 描述玩家完成某个模拟 Tick 后的位置、速度和地面状态。
    /// 该结构可用于客户端预测状态、服务器权威状态以及历史状态缓存。
    /// </summary>
    public struct PlayerState
    {
        /// <summary>
        /// 该玩家状态对应的模拟 Tick 编号。
        /// 表示执行完这个 Tick 的输入和模拟后所得到的状态。
        /// </summary>
        public uint Tick;

        /// <summary>
        /// 玩家在游戏世界中的模拟位置，单位为米。
        /// 这是模拟系统使用的位置，不等同于画面中 PlayerView 的显示位置。
        /// </summary>
        public Vector3 Position;

        /// <summary>
        /// 玩家在游戏世界中的模拟速度，单位为米每秒。
        /// 包含水平方向移动速度和垂直方向速度。
        /// </summary>
        public Vector3 Velocity;

        /// <summary>
        /// 完成当前 Tick 后玩家身体的水平朝向，单位为度，范围为 0 到 360 度。
        /// 服务器使用该值生成远端玩家朝向，并在后续射击阶段验证瞄准方向。
        /// </summary>
        public float Yaw;

        /// <summary>
        /// 完成当前 Tick 后玩家的垂直瞄准角，单位为度。
        /// 该值属于模拟状态，不包含后坐力、呼吸和镜头晃动等纯表现偏移。
        /// </summary>
        public float Pitch;

        /// <summary>
        /// 玩家在当前模拟状态下是否接触地面。
        /// 模拟系统使用该字段判断玩家当前是否允许跳跃。
        /// </summary>
        public bool IsGrounded;
    }
}

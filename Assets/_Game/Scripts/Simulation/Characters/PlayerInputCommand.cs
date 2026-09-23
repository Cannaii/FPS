
namespace AFPS.Simulation.Characters
{
    /// <summary>
    /// 描述单次Tick中输入的用户命令
    /// </summary>
    public struct PlayerInputCommand
    {
        /// <summary>
        /// 该输入命令对应的客户端模拟 Tick 编号。
        /// 服务器使用它确定输入顺序，客户端使用它进行输入确认和预测校正。
        /// </summary>
        public uint Tick;

        /// <summary>
        /// 玩家在本地水平方向上的移动输入。
        /// -1 表示向左，1 表示向右，0 表示没有水平移动输入。
        /// </summary>
        public float MoveX;

        /// <summary>
        /// 玩家在本地前后方向上的移动输入。
        /// -1 表示向后，1 表示向前，0 表示没有前后移动输入。
        /// </summary>
        public float MoveY;

        /// <summary>
        /// 玩家在本 Tick 希望采用的水平观察角，单位为度。
        /// 该值由客户端输入层产生，经过网络量化后同时供客户端预测和服务器权威模拟使用。
        /// </summary>
        public float LookYaw;

        /// <summary>
        /// 玩家在本 Tick 希望采用的垂直观察角，单位为度，向上为负、向下为正。
        /// 该值会被限制在第一人称相机允许的俯仰范围内。
        /// </summary>
        public float LookPitch;

        /// <summary>
        /// 玩家是否在当前 Tick 按下了跳跃键。
        /// 该字段表示一次跳跃输入事件，不表示玩家当前是否处于跳跃状态。
        /// </summary>
        public bool JumpPressed;

        /// <summary>玩家是否在当前 Tick 请求射击；这是一次性事件，替代输入不得复用。</summary>
        public bool FirePressed;

        /// <summary>客户端为每次射击请求分配的单调递增序号；未射击时为零。</summary>
        public uint ShotSequence;

        /// <summary>客户端估计的开火服务器 Tick；服务器只在受限历史窗口内使用。</summary>
        public uint ShotServerTick;
    }
}

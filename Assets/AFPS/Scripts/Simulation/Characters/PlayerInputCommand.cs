
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
        /// 玩家是否在当前 Tick 按下了跳跃键。
        /// 该字段表示一次跳跃输入事件，不表示玩家当前是否处于跳跃状态。
        /// </summary>
        public bool JumpPressed;
    }
}

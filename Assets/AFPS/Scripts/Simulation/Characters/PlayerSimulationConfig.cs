
namespace AFPS.Simulation.Characters
{
    /// <summary>
    /// 保存速度、加速度等参数
    /// </summary>
    public readonly struct PlayerSimulationConfig
    {
        /// <summary>
        /// 玩家在地面上的最大水平移动速度，单位为米每秒。
        /// </summary>
        public readonly float MaxGroundSpeed;

        /// <summary>
        /// 玩家在地面上的水平加速度，单位为米每二次方秒。
        /// 数值越大，玩家达到最大移动速度所需的时间越短。
        /// </summary>
        public readonly float GroundAcceleration;

        /// <summary>
        /// 玩家受到的垂直重力加速度，单位为米每二次方秒。
        /// 建议保存为正数，例如 20，由模拟逻辑负责向下应用。
        /// </summary>
        public readonly float Gravity;

        /// <summary>
        /// 玩家成功跳跃时获得的初始向上速度，单位为米每秒。
        /// </summary>
        public readonly float JumpSpeed;

        /// <summary>
        /// 创建一组玩家移动模拟参数。
        /// 客户端预测与服务器权威模拟必须使用一致的参数。
        /// </summary>
        public PlayerSimulationConfig(
            float maxGroundSpeed,
            float groundAcceleration,
            float gravity,
            float jumpSpeed)
        {
            MaxGroundSpeed = maxGroundSpeed;
            GroundAcceleration = groundAcceleration;
            Gravity = gravity;
            JumpSpeed = jumpSpeed;
        }
    }
}

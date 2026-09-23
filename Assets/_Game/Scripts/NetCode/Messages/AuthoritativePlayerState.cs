using AFPS.Simulation.Characters;

namespace AFPS.NetCode.Messages
{
    /// <summary>
    /// 描述服务器返回给客户端的玩家权威状态确认。
    /// 客户端使用最后处理的输入 Tick 查找同 Tick 预测状态，
    /// 并在出现误差时执行回滚与输入重放。
    /// </summary>
    public readonly struct AuthoritativePlayerState
    {
        /// <summary>
        /// 服务器生成该确认数据时的世界 Tick。
        /// 该值属于服务器世界时间，不等同于客户端输入 Tick。
        /// </summary>
        public readonly uint ServerTick;

        /// <summary>
        /// 服务器最后处理完成的客户端输入 Tick。
        /// 客户端使用该值查找相同 Tick 的本地预测状态。
        /// </summary>
        public readonly uint LastProcessedInputTick;

        /// <summary>
        /// 服务器处理完 LastProcessedInputTick 后得到的玩家权威状态。
        /// State.Tick 应与 LastProcessedInputTick 保持一致。
        /// </summary>
        public readonly PlayerState State;

        /// <summary>
        /// 创建一条服务器玩家状态确认数据。
        /// </summary>
        /// <param name="serverTick">生成确认时的服务器世界 Tick。</param>
        /// <param name="lastProcessedInputTick">服务器最后处理完成的客户端输入 Tick。</param>
        /// <param name="state">服务器处理完对应输入后得到的玩家权威状态。</param>
        public AuthoritativePlayerState(
            uint serverTick,
            uint lastProcessedInputTick,
            in PlayerState state)
        {
            ServerTick = serverTick;
            LastProcessedInputTick = lastProcessedInputTick;
            State = state;
        }
    }
}

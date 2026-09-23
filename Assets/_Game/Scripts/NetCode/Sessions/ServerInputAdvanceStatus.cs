namespace AFPS.NetCode.Sessions
{
    /// <summary>
    /// 描述服务器在当前世界 Tick 如何处理某个玩家的客户端输入。
    /// </summary>
    public enum ServerInputAdvanceStatus : byte
    {
        /// <summary>
        /// 尚未尝试推进该玩家的权威模拟。
        /// </summary>
        None = 0,

        /// <summary>
        /// 缺少下一条连续输入，仍在等待冗余包补齐。
        /// </summary>
        WaitingForInput = 1,

        /// <summary>
        /// 使用客户端实际发送的连续输入推进了模拟。
        /// </summary>
        ReceivedInput = 2,

        /// <summary>
        /// 输入等待超时，短暂复用了上一条真实输入的持续移动轴。
        /// 跳跃等单次触发字段不会被复用。
        /// </summary>
        RepeatedContinuousInput = 3,

        /// <summary>
        /// 输入等待超时且持续移动复用已达到上限，使用全零安全输入推进模拟。
        /// </summary>
        NeutralFallback = 4
    }
}

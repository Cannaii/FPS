namespace AFPS.NetCode.Prediction
{
    /// <summary>
    /// 描述一次客户端预测校正的处理结果。
    /// </summary>
    public enum ReconciliationStatus
    {
        /// <summary>
        /// 同 Tick 误差未超过阈值，客户端保持当前预测状态。
        /// </summary>
        NoCorrection,

        /// <summary>
        /// 已恢复服务器权威状态并重放到客户端当前 Tick。
        /// </summary>
        Corrected,

        /// <summary>
        /// 找不到服务器确认 Tick 对应的客户端预测状态。
        /// </summary>
        MissingPredictionHistory,

        /// <summary>
        /// 重放过程中缺少某个 Tick 的历史输入。
        /// </summary>
        MissingInputHistory
    }
}

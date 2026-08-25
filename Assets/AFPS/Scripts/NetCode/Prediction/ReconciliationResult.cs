using AFPS.Simulation.Characters;

namespace AFPS.NetCode.Prediction
{
    /// <summary>
    /// 描述客户端处理一条服务器权威状态后的结果。
    /// </summary>
    public readonly struct ReconciliationResult
    {
        public readonly ReconciliationStatus Status;
        public readonly PlayerState State;
        public readonly PredictionError Error;
        public readonly int ReplayedTickCount;

        /// <summary>
        /// 历史缺失时无法安全重放，调用者需要采用服务器状态并重置预测历史。
        /// </summary>
        public bool RequiresHardCorrection =>
            Status == ReconciliationStatus.MissingPredictionHistory ||
            Status == ReconciliationStatus.MissingInputHistory;

        public ReconciliationResult(
            ReconciliationStatus status,
            in PlayerState state,
            in PredictionError error,
            int replayedTickCount)
        {
            Status = status;
            State = state;
            Error = error;
            ReplayedTickCount = replayedTickCount;
        }
    }
}

using AFPS.Simulation.Characters;

namespace AFPS.NetCode.Prediction
{
    /// <summary>
    /// 描述客户端处理一条服务器权威状态后的结果。
    /// </summary>
    public readonly struct MovementReconciliationResult
    {
        public readonly MovementReconciliationStatus Status;
        public readonly PlayerMovementState State;
        public readonly MovementPredictionError Error;
        public readonly int ReplayedTickCount;

        /// <summary>
        /// 历史缺失时无法安全重放，调用者需要采用服务器状态并重置预测历史。
        /// </summary>
        public bool RequiresHardCorrection =>
            Status == MovementReconciliationStatus.MissingPredictionHistory ||
            Status == MovementReconciliationStatus.MissingInputHistory;

        public MovementReconciliationResult(
            MovementReconciliationStatus status,
            in PlayerMovementState state,
            in MovementPredictionError error,
            int replayedTickCount)
        {
            Status = status;
            State = state;
            Error = error;
            ReplayedTickCount = replayedTickCount;
        }
    }
}

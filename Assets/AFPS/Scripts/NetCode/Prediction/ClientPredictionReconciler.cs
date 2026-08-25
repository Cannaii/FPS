using System;
using AFPS.Core.Collections;
using AFPS.NetCode.Messages;
using AFPS.Simulation.Characters;

namespace AFPS.NetCode.Prediction
{
    /// <summary>
    /// 根据服务器权威状态校正客户端预测，并重放尚未确认的历史输入。
    /// </summary>
    public static class ClientPredictionReconciler
    {
        /// <summary>
        /// 比较同 Tick 状态；必要时从服务器权威状态重放到客户端当前 Tick。
        /// </summary>
        public static ReconciliationResult Reconcile(
            in AuthoritativePlayerState authoritativeState,
            uint currentTick,
            in PlayerState currentState,
            TickBuffer<PlayerInputCommand> inputHistory,
            TickBuffer<PlayerState> stateHistory,
            in PlayerSimulationConfig config,
            float tickDeltaTime,
            float positionErrorThreshold,
            float velocityErrorThreshold)
        {
            ValidateArguments(
                authoritativeState,
                currentTick,
                currentState,
                inputHistory,
                stateHistory,
                tickDeltaTime,
                positionErrorThreshold,
                velocityErrorThreshold);

            uint acknowledgedTick = authoritativeState.LastProcessedInputTick;

            if (!stateHistory.TryGet(acknowledgedTick, out PlayerState predictedState))
            {
                return new ReconciliationResult(
                    ReconciliationStatus.MissingPredictionHistory,
                    authoritativeState.State,
                    default,
                    0);
            }

            PredictionError error = ClientPredictionComparer.Compare(predictedState, authoritativeState);

            if (!error.Exceeds(positionErrorThreshold, velocityErrorThreshold))
            {
                return new ReconciliationResult(ReconciliationStatus.NoCorrection, currentState, error, 0);
            }

            int replayCount = unchecked((int)(currentTick - acknowledgedTick));

            for (int offset = 1; offset <= replayCount; offset++)
            {
                uint replayTick = unchecked(acknowledgedTick + (uint)offset);

                if (!inputHistory.TryGet(replayTick, out _))
                {
                    return new ReconciliationResult(
                        ReconciliationStatus.MissingInputHistory,
                        authoritativeState.State,
                        error,
                        0);
                }
            }

            PlayerState replayState = authoritativeState.State;
            stateHistory.Store(acknowledgedTick, replayState);

            for (int offset = 1; offset <= replayCount; offset++)
            {
                uint replayTick = unchecked(acknowledgedTick + (uint)offset);
                inputHistory.TryGet(replayTick, out PlayerInputCommand input);
                replayState = PlayerSimulation.Simulate(replayState, input, config, tickDeltaTime);
                stateHistory.Store(replayTick, replayState);
            }

            return new ReconciliationResult(ReconciliationStatus.Corrected, replayState, error, replayCount);
        }

        private static void ValidateArguments(
            in AuthoritativePlayerState authoritativeState,
            uint currentTick,
            in PlayerState currentState,
            TickBuffer<PlayerInputCommand> inputHistory,
            TickBuffer<PlayerState> stateHistory,
            float tickDeltaTime,
            float positionErrorThreshold,
            float velocityErrorThreshold)
        {
            if (inputHistory == null)
            {
                throw new ArgumentNullException(nameof(inputHistory));
            }

            if (stateHistory == null)
            {
                throw new ArgumentNullException(nameof(stateHistory));
            }

            if (currentState.Tick != currentTick)
            {
                throw new ArgumentException("客户端当前状态 Tick 必须与 currentTick 一致。", nameof(currentState));
            }

            if (authoritativeState.State.Tick != authoritativeState.LastProcessedInputTick)
            {
                throw new ArgumentException("服务器权威状态 Tick 必须与最后处理输入 Tick 一致。", nameof(authoritativeState));
            }

            int replayCount = unchecked((int)(currentTick - authoritativeState.LastProcessedInputTick));

            if (replayCount < 0)
            {
                throw new ArgumentException("服务器确认 Tick 不能晚于客户端当前 Tick。", nameof(authoritativeState));
            }

            if (tickDeltaTime <= 0f || float.IsNaN(tickDeltaTime) || float.IsInfinity(tickDeltaTime))
            {
                throw new ArgumentOutOfRangeException(nameof(tickDeltaTime));
            }

            if (positionErrorThreshold < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(positionErrorThreshold));
            }

            if (velocityErrorThreshold < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(velocityErrorThreshold));
            }
        }
    }
}

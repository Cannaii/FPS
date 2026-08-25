using System;
using AFPS.NetCode.Messages;
using AFPS.Simulation.Characters;
using UnityEngine;

namespace AFPS.NetCode.Prediction
{
    /// <summary>
    /// 比较服务器权威状态与客户端相同输入 Tick 的历史预测状态。
    /// </summary>
    public static class ClientPredictionComparer
    {
        /// <summary>
        /// 计算服务器权威状态与客户端同 Tick 预测状态之间的误差。
        /// </summary>
        /// <param name="predictedState">客户端缓存的历史预测状态。</param>
        /// <param name="authoritativeState">服务器返回的权威状态确认。</param>
        /// <returns>位置、速度和落地状态的比较结果。</returns>
        public static PredictionError Compare(
            in PlayerState predictedState,
            in AuthoritativePlayerState authoritativeState)
        {
            if (authoritativeState.State.Tick != authoritativeState.LastProcessedInputTick)
            {
                throw new ArgumentException("服务器权威状态 Tick 必须与最后处理的输入 Tick 一致。", nameof(authoritativeState));
            }

            if (predictedState.Tick != authoritativeState.LastProcessedInputTick)
            {
                throw new ArgumentException("只能比较相同输入 Tick 的客户端预测状态与服务器权威状态。", nameof(predictedState));
            }

            float positionError = Vector3.Distance(predictedState.Position, authoritativeState.State.Position);
            float velocityError = Vector3.Distance(predictedState.Velocity, authoritativeState.State.Velocity);
            bool groundedMismatch = predictedState.IsGrounded != authoritativeState.State.IsGrounded;

            return new PredictionError(predictedState.Tick, positionError, velocityError, groundedMismatch);
        }
    }
}

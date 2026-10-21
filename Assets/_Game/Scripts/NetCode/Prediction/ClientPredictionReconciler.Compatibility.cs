using System;
using AFPS.Core.Collections;
using AFPS.NetCode.Messages;
using AFPS.Simulation.Characters;
using AFPS.Simulation.Characters.Collision;

namespace AFPS.NetCode.Prediction
{
    public static partial class ClientPredictionReconciler
    {
        /// <summary>旧调用签名。阈值仅保留配置兼容；定点状态校正一律按 RawMismatch 决策。</summary>
        public static ReconciliationResult Reconcile(in AuthoritativePlayerState authoritativeState, uint currentTick, in PlayerState currentState, TickBuffer<PlayerInputCommand> inputHistory, TickBuffer<PlayerState> stateHistory, in PlayerSimulationConfig config, float tickDeltaTime, float positionErrorThreshold, float velocityErrorThreshold, ICharacterCollisionWorld collisionWorld = null)
        {
            if (positionErrorThreshold < 0 || float.IsNaN(positionErrorThreshold) || float.IsInfinity(positionErrorThreshold)) throw new ArgumentOutOfRangeException(nameof(positionErrorThreshold));
            if (velocityErrorThreshold < 0 || float.IsNaN(velocityErrorThreshold) || float.IsInfinity(velocityErrorThreshold)) throw new ArgumentOutOfRangeException(nameof(velocityErrorThreshold));
            return Reconcile(authoritativeState, currentTick, currentState, inputHistory, stateHistory, config, SimulationStep.FromSeconds(tickDeltaTime), LegacyCharacterCollisionWorldAdapter.Adapt(collisionWorld));
        }
    }
}

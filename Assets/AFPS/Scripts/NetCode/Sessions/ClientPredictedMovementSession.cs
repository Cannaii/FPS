using System;
using AFPS.Core.Collections;
using AFPS.NetCode.InputReplication;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Prediction;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.StateReplication;
using AFPS.NetCode.Transport;
using AFPS.Simulation.Characters;

namespace AFPS.NetCode.Sessions
{
    /// <summary>
    /// 组装单个本地玩家的预测、输入历史、冗余发送、权威状态接收和回滚重放。
    /// 该类只维护模拟状态，不直接操作 Unity 场景中的显示对象。
    /// </summary>
    public sealed class ClientPredictedMovementSession
    {
        private readonly TickBuffer<PlayerInputCommand> inputHistory;
        private readonly TickBuffer<PlayerState> stateHistory;
        private readonly ClientInputBatchSender inputSender;
        private readonly ClientAuthoritativeStateReceiver stateReceiver = new ClientAuthoritativeStateReceiver();
        private readonly PlayerSimulationConfig simulationConfig;
        private readonly float tickDeltaTime;
        private readonly float positionErrorThreshold;
        private readonly float velocityErrorThreshold;

        /// <summary>
        /// 客户端执行完最新本地输入后持有的预测状态。
        /// </summary>
        public PlayerState CurrentState { get; private set; }

        public ClientPredictedMovementSession(IGameTransport transport, TransportConnectionId serverConnectionId, in PlayerState initialState, in PlayerSimulationConfig simulationConfig, float tickDeltaTime, int historyCapacity, int inputRedundancyCount, float positionErrorThreshold, float velocityErrorThreshold)
        {
            if (tickDeltaTime <= 0f || float.IsNaN(tickDeltaTime) || float.IsInfinity(tickDeltaTime))
            {
                throw new ArgumentOutOfRangeException(nameof(tickDeltaTime));
            }

            if (historyCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(historyCapacity));
            }

            if (positionErrorThreshold < AuthoritativePlayerStateCodec.MaximumPositionQuantizationError)
            {
                throw new ArgumentOutOfRangeException(nameof(positionErrorThreshold), "位置误差阈值不能低于权威状态协议的最大位置量化误差。");
            }

            if (velocityErrorThreshold < AuthoritativePlayerStateCodec.MaximumVelocityQuantizationError)
            {
                throw new ArgumentOutOfRangeException(nameof(velocityErrorThreshold), "速度误差阈值不能低于权威状态协议的最大速度量化误差。");
            }

            this.simulationConfig = simulationConfig;
            this.tickDeltaTime = tickDeltaTime;
            this.positionErrorThreshold = positionErrorThreshold;
            this.velocityErrorThreshold = velocityErrorThreshold;
            CurrentState = initialState;
            inputHistory = new TickBuffer<PlayerInputCommand>(historyCapacity);
            stateHistory = new TickBuffer<PlayerState>(historyCapacity);
            stateHistory.Store(initialState.Tick, initialState);
            inputSender = new ClientInputBatchSender(transport, serverConnectionId, inputHistory, inputRedundancyCount);
        }

        /// <summary>
        /// 规范化并立即模拟一条本地输入，同时把最近连续输入批次交给传输层。
        /// 网络发送失败不会撤销本地预测，后续冗余包仍有机会补发该输入。
        /// </summary>
        public PlayerState PredictAndSend(PlayerInputCommand command, out InputBatchSendResult sendResult)
        {
            uint expectedTick = unchecked(CurrentState.Tick + 1);
            if (command.Tick != expectedTick)
            {
                throw new ArgumentException($"新的客户端输入 Tick 必须连续。期望 {expectedTick}，实际 {command.Tick}。", nameof(command));
            }

            command = InputCommandBatchCodec.Canonicalize(command);
            inputHistory.Store(command.Tick, command);
            CurrentState = PlayerSimulation.Simulate(CurrentState, command, simulationConfig, tickDeltaTime);
            stateHistory.Store(command.Tick, CurrentState);
            inputSender.TrySendLatest(command.Tick, out sendResult);
            return CurrentState;
        }

        /// <summary>
        /// 接收一条服务器权威状态包，并在同 Tick 误差超过阈值时回滚到权威状态再重放到当前 Tick。
        /// </summary>
        public bool TryReceiveAuthoritativePacket(ArraySegment<byte> packet, out AuthoritativeStateReceiveResult receiveResult, out ReconciliationResult reconciliationResult)
        {
            reconciliationResult = default;
            if (!stateReceiver.TryReceivePacket(packet, out AuthoritativePlayerState authoritativeState, out receiveResult))
            {
                return false;
            }

            if (SequenceMath.IsNewer(authoritativeState.LastProcessedInputTick, CurrentState.Tick))
            {
                throw new InvalidOperationException("服务器确认的客户端输入 Tick 不能晚于客户端当前预测 Tick。");
            }

            reconciliationResult = ClientPredictionReconciler.Reconcile(authoritativeState, CurrentState.Tick, CurrentState, inputHistory, stateHistory, simulationConfig, tickDeltaTime, positionErrorThreshold, velocityErrorThreshold);
            if (reconciliationResult.RequiresHardCorrection)
            {
                CurrentState = authoritativeState.State;
                inputHistory.Clear();
                stateHistory.Clear();
                stateHistory.Store(CurrentState.Tick, CurrentState);
                return true;
            }

            CurrentState = reconciliationResult.State;
            return true;
        }
    }
}

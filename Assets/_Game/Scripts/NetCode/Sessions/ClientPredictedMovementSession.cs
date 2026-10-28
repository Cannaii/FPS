using System;
using System.Collections.Generic;
using AFPS.Math;
using AFPS.Core.Collections;
using AFPS.NetCode.InputReplication;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Prediction;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.StateReplication;
using AFPS.NetCode.Transport;
using AFPS.Simulation.Characters;
using AFPS.Simulation.Characters.Collision;

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
        private readonly Fix tickDeltaTime;
        private readonly IFixedCharacterCollisionWorld collisionWorld;
        /// <summary>
        /// 客户端执行完最新本地输入后持有的预测状态。
        /// </summary>
        public PlayerState CurrentState { get; private set; }
        /// <summary>最近通过包序号校验的权威完整状态，用于诊断；不含客户端重放产生的后续状态。</summary>
        public PlayerState LastAuthoritativeState { get; private set; }
        public PlayerInputCommand LastPredictedInput { get; private set; }

        /// <summary>只在断线边界复制最多32条连续输入；缺失历史时停止，不能跨缺口补造命令。</summary>
        internal List<PlayerInputCommand> CaptureRecoveryInputs()
        {
            var result = new List<PlayerInputCommand>();
            uint tick = CurrentState.Tick;
            for (int i = 0; i < 32 && inputHistory.TryGet(tick, out var command); i++)
            {
                result.Add(command);
                tick = unchecked(tick - 1);
            }
            result.Reverse();
            return result;
        }

        public ClientPredictedMovementSession(
            IGameTransport transport,
            TransportConnectionId serverConnectionId,
            in PlayerState initialState,
            in PlayerSimulationConfig simulationConfig,
            float tickDeltaTime,
            int historyCapacity,
            int inputRedundancyCount,
            float positionErrorThreshold,
            float velocityErrorThreshold,
            ICharacterCollisionWorld collisionWorld = null,
            IFixedCharacterCollisionWorld fixedCollisionWorld = null)
        {
            if (tickDeltaTime <= 0f || float.IsNaN(tickDeltaTime) || float.IsInfinity(tickDeltaTime))
            {
                throw new ArgumentOutOfRangeException(nameof(tickDeltaTime));
            }

            if (historyCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(historyCapacity));
            }

            if (positionErrorThreshold < 0f || float.IsNaN(positionErrorThreshold) || float.IsInfinity(positionErrorThreshold))
            {
                throw new ArgumentOutOfRangeException(nameof(positionErrorThreshold), "诊断阈值必须是非负有限值；定点校正始终逐位比较。");
            }

            if (velocityErrorThreshold < 0f || float.IsNaN(velocityErrorThreshold) || float.IsInfinity(velocityErrorThreshold))
            {
                throw new ArgumentOutOfRangeException(nameof(velocityErrorThreshold), "诊断阈值必须是非负有限值；定点校正始终逐位比较。");
            }

            this.simulationConfig = simulationConfig;
            this.tickDeltaTime = SimulationStep.FromSeconds(tickDeltaTime);
            if (collisionWorld != null && fixedCollisionWorld != null)
            {
                throw new ArgumentException("只能指定一种碰撞后端。", nameof(fixedCollisionWorld));
            }

            this.collisionWorld = fixedCollisionWorld ?? LegacyCharacterCollisionWorldAdapter.Adapt(collisionWorld);
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
            LastPredictedInput = command;
            inputHistory.Store(command.Tick, command);
            CurrentState = PlayerSimulation.Simulate(CurrentState, command, simulationConfig, tickDeltaTime, collisionWorld);
            stateHistory.Store(command.Tick, CurrentState);
            inputSender.TrySendLatest(command.Tick, out sendResult);
            return CurrentState;
        }

        /// <summary>
        /// 接收一条服务器权威状态包，并在同 Tick Raw 不一致时回滚到权威状态再重放到当前 Tick。
        /// </summary>
        public bool TryReceiveAuthoritativePacket(
            ArraySegment<byte> packet,
            out AuthoritativeStateReceiveResult receiveResult,
            out ReconciliationResult reconciliationResult)
        {
            reconciliationResult = default;
            if (!stateReceiver.TryReceivePacket(packet, out AuthoritativePlayerState authoritativeState, out receiveResult))
            {
                return false;
            }

            LastAuthoritativeState = authoritativeState.State;
            if (SequenceMath.IsNewer(authoritativeState.LastProcessedInputTick, CurrentState.Tick))
            {
                // A client can fall behind while the server keeps advancing neutral fallback
                // input (for example while joining a host that was already running). There is
                // no local history to replay in that case, so rebase on the trusted server state.
                reconciliationResult = new ReconciliationResult(ReconciliationStatus.MissingPredictionHistory, authoritativeState.State, default, 0);
            }
            else
            {
                reconciliationResult = ClientPredictionReconciler.Reconcile(authoritativeState, CurrentState.Tick, CurrentState, inputHistory, stateHistory, simulationConfig, tickDeltaTime, collisionWorld);
            }

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

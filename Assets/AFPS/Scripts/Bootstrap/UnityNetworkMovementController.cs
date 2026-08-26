using System;
using AFPS.Core.Tick;
using AFPS.Input;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Prediction;
using AFPS.NetCode.Runtime;
using AFPS.NetCode.Sessions;
using AFPS.NetCode.Transport;
using AFPS.Presentation.Characters;
using AFPS.Simulation.Characters;
using UnityEngine;

namespace AFPS.Bootstrap
{
    /// <summary>
    /// 将 Unity 输入、固定 Tick、网络传输事件、预测/权威会话和本地玩家显示串联起来。
    /// 传输事件仍只由 UnityNetworkBootstrap 轮询，本组件只同步消费其回调。
    /// </summary>
    public sealed class UnityNetworkMovementController : MonoBehaviour
    {
        [SerializeField] private UnityNetworkBootstrap networkBootstrap;
        [SerializeField] private SimulationTickRunner tickRunner;
        [SerializeField] private LocalPlayerInputCollector inputCollector;
        [SerializeField] private PlayerView playerView;
        [SerializeField, Min(0f)] private float maxGroundSpeed = 6f;
        [SerializeField, Min(0f)] private float groundAcceleration = 20f;
        [SerializeField, Min(0f)] private float gravity = 20f;
        [SerializeField, Min(0f)] private float jumpSpeed = 8f;
        [SerializeField, Min(1)] private int predictionHistoryCapacity = 256;
        [SerializeField, Range(1, InputCommandBatchCodec.MaxCommandCount)] private int inputRedundancyCount = 3;
        [SerializeField, Min(1)] private int serverInputWindowCapacity = 64;
        [SerializeField, Min(0)] private int maxMissingInputWaitTicks = 2;
        [SerializeField, Min(0)] private int maxRepeatedMovementTicks = 2;
        [SerializeField, Min(0f)] private float positionErrorThreshold = 0.002f;
        [SerializeField, Min(0f)] private float velocityErrorThreshold = 0.02f;
        [SerializeField] private Vector3 serverSpawnPosition = Vector3.zero;

        private NetworkMovementSessionManager sessionManager;
        private bool initialized;

        /// <summary>
        /// 当前连接事件所创建的会话管理器，供运行时诊断读取。
        /// </summary>
        public NetworkMovementSessionManager SessionManager => sessionManager;

        private void Start()
        {
            if (networkBootstrap == null || tickRunner == null || networkBootstrap.Runtime == null)
            {
                Debug.LogError("UnityNetworkMovementController 缺少网络启动器、TickRunner，或网络启动失败。", this);
                enabled = false;
                return;
            }

            bool hasLocalClient = networkBootstrap.Runtime.ClientTransport != null;
            if (hasLocalClient && (inputCollector == null || playerView == null))
            {
                Debug.LogError("Client/Host 模式需要本地输入采集器和 PlayerView。", this);
                enabled = false;
                return;
            }

            PlayerSimulationConfig config = new PlayerSimulationConfig(maxGroundSpeed, groundAcceleration, gravity, jumpSpeed);
            PlayerState serverInitialState = new PlayerState { Tick = 0, Position = serverSpawnPosition, Velocity = Vector3.zero, IsGrounded = true };
            PlayerState clientInitialState = new PlayerState { Tick = 0, Position = hasLocalClient ? playerView.InitialPosition : serverSpawnPosition, Velocity = Vector3.zero, IsGrounded = true };

            try
            {
                sessionManager = new NetworkMovementSessionManager(networkBootstrap.Runtime.ServerTransport, networkBootstrap.Runtime.ClientTransport, serverInitialState, clientInitialState, config, tickRunner.TickDeltaTime, predictionHistoryCapacity, inputRedundancyCount, serverInputWindowCapacity, maxMissingInputWaitTicks, maxRepeatedMovementTicks, positionErrorThreshold, velocityErrorThreshold);
            }
            catch (ArgumentException exception)
            {
                Debug.LogError($"网络移动会话配置无效：{exception.Message}", this);
                enabled = false;
                return;
            }

            networkBootstrap.TransportEventReceived += HandleTransportEvent;
            tickRunner.TickOccurred += HandleTickOccurred;
            initialized = true;
        }

        private void OnDestroy()
        {
            if (networkBootstrap != null)
            {
                networkBootstrap.TransportEventReceived -= HandleTransportEvent;
            }

            if (tickRunner != null)
            {
                tickRunner.TickOccurred -= HandleTickOccurred;
            }
        }

        private void LateUpdate()
        {
            if (initialized && playerView != null && sessionManager.ClientSession != null)
            {
                playerView.Render(tickRunner.TickAlpha, tickRunner.TickDeltaTime, Time.unscaledDeltaTime);
            }
        }

        private void HandleTickOccurred(uint serverWorldTick, float tickDeltaTime)
        {
            if (!initialized)
            {
                return;
            }

            ClientPredictedMovementSession clientSession = sessionManager.ClientSession;
            if (clientSession != null)
            {
                uint inputTick = unchecked(clientSession.CurrentState.Tick + 1);
                PlayerInputCommand command = inputCollector.ConsumeCommand(inputTick);
                if (sessionManager.TryPredictAndSend(command, out PlayerState predictedState, out _))
                {
                    playerView.ApplyState(predictedState);
                }
            }

            sessionManager.AdvanceServerSessions(serverWorldTick);
        }

        private void HandleTransportEvent(NetworkTransportSide side, GameTransportEvent transportEvent, ArraySegment<byte> payload)
        {
            switch (transportEvent.Type)
            {
                case TransportEventType.Connected:
                    sessionManager.HandleConnected(side, transportEvent.ConnectionId);
                    if (side == NetworkTransportSide.Client && playerView != null)
                    {
                        playerView.SnapToState(sessionManager.ClientSession.CurrentState);
                    }
                    break;
                case TransportEventType.Disconnected:
                    sessionManager.HandleDisconnected(side, transportEvent.ConnectionId);
                    break;
                case TransportEventType.Data:
                    HandleData(side, transportEvent.ConnectionId, payload);
                    break;
            }
        }

        private void HandleData(NetworkTransportSide side, TransportConnectionId connectionId, ArraySegment<byte> payload)
        {
            if (!sessionManager.TryHandleData(side, connectionId, payload, out ReconciliationResult reconciliation) || side != NetworkTransportSide.Client)
            {
                return;
            }

            PlayerState state = sessionManager.ClientSession.CurrentState;
            if (reconciliation.RequiresHardCorrection)
            {
                playerView.SnapToState(state);
            }
            else if (reconciliation.Status == ReconciliationStatus.Corrected)
            {
                playerView.ApplyCorrection(state, tickRunner.TickAlpha, tickRunner.TickDeltaTime);
            }
            else
            {
                playerView.ApplyState(state);
            }
        }
    }
}

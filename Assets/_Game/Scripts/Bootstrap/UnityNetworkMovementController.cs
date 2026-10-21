using System;
using AFPS.Core.Tick;
using AFPS.Bootstrap.Physics;
using AFPS.Input;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Match;
using AFPS.NetCode.Prediction;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.Runtime;
using AFPS.NetCode.Sessions;
using AFPS.NetCode.Transport;
using AFPS.Presentation.Characters;
using AFPS.Presentation.Camera;
using AFPS.Presentation.Weapons;
using AFPS.Simulation.Characters;
using AFPS.Simulation.Characters.Collision;
using UnityEngine;
using UnityEngine.SceneManagement;

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
        [SerializeField] private FirstPersonCameraRig firstPersonCameraRig;
        [SerializeField] private FirstPersonWeaponView firstPersonWeaponView;
        [SerializeField] private RemotePlayerWorldView remotePlayerWorldView;
        [SerializeField] private bool hideLocalBodyInFirstPerson = true;
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
        [SerializeField, Min(0f)] private float serverSpawnSpacing = 2.5f;
        [Header("Remote Player Replication")]
        [SerializeField, Min(2)] private int remoteSnapshotCapacity = 32;
        [SerializeField, Min(0f)] private float remoteInterpolationDelayTicks = 2f;
        [SerializeField, Min(0f)] private float remoteMaxExtrapolationTicks = 2f;
        [SerializeField, Min(0.01f)] private float remoteTeleportDistance = 5f;
        [Header("Deathmatch")]
        [SerializeField, Min(1)] private int deathmatchKillLimit = 10;
        [SerializeField, Min(1f)] private float deathmatchDurationSeconds = 300f;
        [SerializeField, Min(0f)] private float deathmatchRespawnSeconds = 3f;
        [SerializeField, Min(0.05f)] private float deathmatchReplicationIntervalSeconds = 0.25f;
        [Header("Predictable Character Collision")]
        [SerializeField] private LayerMask collisionLayers = ~0;
        [SerializeField] private bool collideWithTriggers;
        [SerializeField, Min(0.01f)] private float capsuleRadius = 0.5f;
        [SerializeField, Min(0.02f)] private float capsuleHeight = 2f;
        [SerializeField, Min(0f)] private float collisionSkinWidth = 0.01f;
        [SerializeField, Min(0f)] private float groundProbeDistance = 0.1f;
        [SerializeField, Min(0f)] private float stepHeight = 0.3f;
        [SerializeField, Range(1f, 89f)] private float maxSlopeAngle = 50f;
        [SerializeField, Min(1)] private int maxSlideIterations = 4;
        [SerializeField] private Collider localPlayerCollider;

        private NetworkMovementSessionManager sessionManager;
        private bool initialized;
        private uint observedRemoteSnapshotVersion;
        private uint latestRemoteServerTick;
        private double latestRemoteSnapshotReceiveTime;
        private DeathmatchHud deathmatchHud;
        private bool weaponViewWasActive;

        /// <summary>
        /// 当前连接事件所创建的会话管理器，供运行时诊断读取。
        /// </summary>
        public NetworkMovementSessionManager SessionManager => sessionManager;

        private void Start()
        {
            if (networkBootstrap == null || tickRunner == null)
            {
                Debug.LogError("UnityNetworkMovementController 缺少网络启动器或 TickRunner。", this);
                enabled = false;
                return;
            }

            networkBootstrap.NetworkStarted += HandleNetworkStarted;
            TryInitialize();
        }

        private void HandleNetworkStarted(NetworkLaunchMode mode)
        {
            TryInitialize();
        }

        private void TryInitialize()
        {
            if (initialized || networkBootstrap.Runtime == null)
            {
                return;
            }

            bool hasLocalClient = networkBootstrap.Runtime.ClientTransport != null;
            if (hasLocalClient && (inputCollector == null || playerView == null))
            {
                Debug.LogError("Client/Host 模式需要本地输入采集器和 PlayerView。", this);
                enabled = false;
                return;
            }

            CharacterCollisionConfig collisionConfig;
            try
            {
                collisionConfig = new CharacterCollisionConfig(capsuleRadius, capsuleHeight, collisionSkinWidth, groundProbeDistance, stepHeight, maxSlopeAngle, maxSlideIterations);
            }
            catch (ArgumentException exception)
            {
                Debug.LogError($"角色碰撞配置无效：{exception.Message}", this);
                enabled = false;
                return;
            }

            PlayerSimulationConfig config = new PlayerSimulationConfig(maxGroundSpeed, groundAcceleration, gravity, jumpSpeed, collisionConfig);
            PlayerState serverInitialState = new PlayerState { Tick = 0, Position = serverSpawnPosition, Velocity = Vector3.zero, IsGrounded = true };
            PlayerState clientInitialState = new PlayerState { Tick = 0, Position = hasLocalClient ? playerView.InitialPosition : serverSpawnPosition, Velocity = Vector3.zero, Yaw = hasLocalClient ? playerView.InitialYaw : 0f, IsGrounded = true };
            QueryTriggerInteraction triggerInteraction = collideWithTriggers ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;
            ICharacterCollisionWorld collisionWorld = new UnityPhysicsCharacterCollisionWorld(gameObject.scene.GetPhysicsScene(), collisionLayers.value, triggerInteraction);

            if (hasLocalClient)
            {
                if (localPlayerCollider == null)
                {
                    localPlayerCollider = playerView.GetComponent<Collider>();
                }

                if (localPlayerCollider != null)
                {
                    localPlayerCollider.enabled = false;
                }
            }

            try
            {
                sessionManager = new NetworkMovementSessionManager(networkBootstrap.Runtime.ServerTransport, networkBootstrap.Runtime.ClientTransport, serverInitialState, clientInitialState, config, tickRunner.TickDeltaTime, predictionHistoryCapacity, inputRedundancyCount, serverInputWindowCapacity, maxMissingInputWaitTicks, maxRepeatedMovementTicks, positionErrorThreshold, velocityErrorThreshold, collisionWorld, serverSpawnSpacing, remoteSnapshotCapacity, remoteInterpolationDelayTicks, remoteMaxExtrapolationTicks, remoteTeleportDistance, deathmatchKillLimit: deathmatchKillLimit, deathmatchDurationSeconds: deathmatchDurationSeconds, deathmatchRespawnSeconds: deathmatchRespawnSeconds, deathmatchReplicationIntervalSeconds: deathmatchReplicationIntervalSeconds);
            }
            catch (ArgumentException exception)
            {
                Debug.LogError($"网络移动会话配置无效：{exception.Message}", this);
                enabled = false;
                return;
            }

            networkBootstrap.TransportEventReceived += HandleTransportEvent;
            tickRunner.TickOccurred += HandleTickOccurred;
            if (hasLocalClient)
            {
                inputCollector.SetLookAngles(clientInitialState.Yaw, clientInitialState.Pitch);
                if (firstPersonCameraRig == null && UnityEngine.Camera.main != null)
                {
                    firstPersonCameraRig = UnityEngine.Camera.main.GetComponent<FirstPersonCameraRig>();
                    if (firstPersonCameraRig == null)
                    {
                        firstPersonCameraRig = UnityEngine.Camera.main.gameObject.AddComponent<FirstPersonCameraRig>();
                    }

                    firstPersonCameraRig.Configure(playerView.SimulationTransform, UnityEngine.Camera.main.transform);
                }

                if (hideLocalBodyInFirstPerson && firstPersonCameraRig != null)
                {
                    Renderer[] localRenderers = playerView.SimulationTransform.GetComponentsInChildren<Renderer>(true);
                    for (int i = 0; i < localRenderers.Length; i++)
                    {
                        localRenderers[i].enabled = false;
                    }
                }

                if (firstPersonWeaponView == null && UnityEngine.Camera.main != null)
                {
                    firstPersonWeaponView = UnityEngine.Camera.main.GetComponentInChildren<FirstPersonWeaponView>(true);
                }

                if (remotePlayerWorldView == null)
                {
                    remotePlayerWorldView = GetComponent<RemotePlayerWorldView>();
                }

                if (remotePlayerWorldView == null)
                {
                    remotePlayerWorldView = gameObject.AddComponent<RemotePlayerWorldView>();
                }

                deathmatchHud = GetComponent<DeathmatchHud>();
                if (deathmatchHud == null)
                {
                    deathmatchHud = gameObject.AddComponent<DeathmatchHud>();
                }
                deathmatchHud.Configure(sessionManager, tickRunner.TickDeltaTime);
                weaponViewWasActive = firstPersonWeaponView != null && firstPersonWeaponView.gameObject.activeSelf;
            }

            initialized = true;
            if (hasLocalClient && NetworkSmokeTestDriver.IsRequested)
            {
                NetworkSmokeTestDriver smokeTest = gameObject.AddComponent<NetworkSmokeTestDriver>();
                smokeTest.Configure(networkBootstrap, this, inputCollector);
            }
        }

        private void OnDestroy()
        {
            if (networkBootstrap != null)
            {
                networkBootstrap.NetworkStarted -= HandleNetworkStarted;
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
                if (firstPersonCameraRig != null)
                {
                    firstPersonCameraRig.Render(inputCollector.LookYaw, inputCollector.LookPitch);
                }

                if (firstPersonWeaponView != null && playerView.TryGetLatestState(out PlayerState localState))
                {
                    bool active = IsLocalGameplayActive();
                    if (firstPersonWeaponView.gameObject.activeSelf != active && (active || weaponViewWasActive))
                    {
                        firstPersonWeaponView.gameObject.SetActive(active);
                    }
                    if (active)
                    {
                        firstPersonWeaponView.Render(localState, Time.unscaledDeltaTime);
                    }
                }

                RenderRemotePlayers();
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
                if (!IsLocalGameplayActive())
                {
                    command.MoveX = 0f;
                    command.MoveY = 0f;
                    command.JumpPressed = false;
                    command.FirePressed = false;
                    command.ShotSequence = 0;
                }
                if (command.FirePressed)
                {
                    command.ShotServerTick = sessionManager.RemotePlayers != null && sessionManager.RemotePlayers.LatestServerTick != 0 ? sessionManager.RemotePlayers.LatestServerTick : serverWorldTick;
                    if (firstPersonWeaponView != null)
                    {
                        firstPersonWeaponView.PlayPredictedShot();
                    }
                }

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
                        inputCollector.SetLookAngles(sessionManager.ClientSession.CurrentState.Yaw, sessionManager.ClientSession.CurrentState.Pitch);
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
            if (!sessionManager.TryHandleData(side, connectionId, payload, out ReconciliationResult reconciliation) || side != NetworkTransportSide.Client || !PacketHeaderCodec.TryRead(payload, out PacketHeader header) || header.MessageType != NetworkMessageType.AuthoritativePlayerState)
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

        private void RenderRemotePlayers()
        {
            if (remotePlayerWorldView == null || sessionManager.RemotePlayers == null)
            {
                return;
            }

            if (observedRemoteSnapshotVersion != sessionManager.RemotePlayers.SnapshotVersion)
            {
                observedRemoteSnapshotVersion = sessionManager.RemotePlayers.SnapshotVersion;
                latestRemoteServerTick = sessionManager.RemotePlayers.LatestServerTick;
                latestRemoteSnapshotReceiveTime = Time.realtimeSinceStartupAsDouble;
            }

            if (observedRemoteSnapshotVersion == 0)
            {
                remotePlayerWorldView.Render(sessionManager.RemotePlayers, 0d, sessionManager.ClientDeathmatchState);
                return;
            }

            double elapsedTicks = (Time.realtimeSinceStartupAsDouble - latestRemoteSnapshotReceiveTime) / tickRunner.TickDeltaTime;
            remotePlayerWorldView.Render(sessionManager.RemotePlayers, latestRemoteServerTick + elapsedTicks, sessionManager.ClientDeathmatchState);
        }

        private bool IsLocalGameplayActive()
        {
            DeathmatchState match = sessionManager.ClientDeathmatchState;
            if (match == null)
            {
                return true;
            }

            uint localEntityId = sessionManager.RemotePlayers != null ? sessionManager.RemotePlayers.LocalEntityId : 0;
            return match.Phase == DeathmatchPhase.Playing && match.TryGetPlayer(localEntityId, out DeathmatchPlayerState local) && local.IsAlive;
        }
    }
}

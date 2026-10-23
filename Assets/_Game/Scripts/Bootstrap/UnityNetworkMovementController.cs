using System;
using AFPS.Core.Tick;
using AFPS.Math;
using AFPS.Input;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Match;
using AFPS.NetCode.Prediction;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.Runtime;
using AFPS.NetCode.Sessions;
using AFPS.NetCode.Transport;
using AFPS.NetCode.Weapons;
using AFPS.Presentation.Characters;
using AFPS.Presentation.Camera;
using AFPS.Presentation.Weapons;
using AFPS.Simulation.Characters;
using AFPS.Simulation.Characters.Collision;
using AFPS.Simulation.Weapons;
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
        [Tooltip("所有网络角色共用的规范世界快照；缺失或损坏时禁止启动。")]
        [SerializeField] private TextAsset fixedWorldSnapshot;
        [SerializeField] private LayerMask collisionLayers = ~0;
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
        private ShotResultView shotResultView;
        private ushort inputWeaponId;
        private bool weaponViewWasActive;
        private SimulationWorldContext preparedWorld;
        private ClientPredictedMovementSession presentedClientSession;
        private ConnectionAdmissionStatus observedAdmissionStatus;

        public string ConnectionStatusMessage { get; private set; }

        public bool CanEnterGame => initialized
            && sessionManager.ClientAdmissionStatus == ConnectionAdmissionStatus.Admitted
            && sessionManager.RemotePlayers != null
            && sessionManager.RemotePlayers.LocalEntityId != 0;

        /// <summary>启动传输前检查快照和配置；三个网络角色使用完全相同的加载入口。</summary>
        public bool TryPrepareSimulation(out string error)
        {
            preparedWorld = null;
            if (fixedWorldSnapshot == null || tickRunner == null)
            {
                error = "Missing baked simulation world or TickRunner.";
                return false;
            }

            try
            {
                CharacterCollisionConfig collision = new CharacterCollisionConfig(
                    capsuleRadius, capsuleHeight, collisionSkinWidth,
                    groundProbeDistance, stepHeight, maxSlopeAngle, maxSlideIterations);
                PlayerSimulationConfig configuration = new PlayerSimulationConfig(
                    maxGroundSpeed, groundAcceleration, gravity, jumpSpeed, collision);
                var spatial = new SimulationSessionSpatialConfig(
                    new FixVec3(Fix.FromFloat(serverSpawnPosition.x),
                        Fix.FromFloat(serverSpawnPosition.y), Fix.FromFloat(serverSpawnPosition.z)),
                    Fix.FromFloat(serverSpawnSpacing), SimulationSessionSpatialConfig.Default.InterestRadius);
                preparedWorld = SimulationWorldContext.Load(
                    new ArraySegment<byte>(fixedWorldSnapshot.bytes), configuration,
                    Fix.FromFloat(tickRunner.TickDeltaTime), collisionLayers.value, spatial);
                error = null;
                return true;
            }
            catch (Exception exception) when (exception is ArgumentException
                || exception is FormatException || exception is OverflowException)
            {
                // 配置或资源错误必须显式暴露；禁止退回 PhysX/平地而绕过世界指纹。
                error = "Invalid simulation world: " + exception.Message;
                return false;
            }
        }

        /// <summary>
        /// 当前连接事件所创建的会话管理器，供运行时诊断读取。
        /// </summary>
        public NetworkMovementSessionManager SessionManager => sessionManager;
        public ulong PresentedShotResultCount => shotResultView != null ? shotResultView.ConsumedResultCount : 0;
        public ulong ConfirmedLocalHitCount => shotResultView != null ? shotResultView.LocalHitConfirmationCount : 0;
        public ulong ReceivedDamageCount => shotResultView != null ? shotResultView.ReceivedDamageCount : 0;
        public ulong RejectedShotCount => shotResultView != null ? shotResultView.RejectedShotCount : 0;
        public ulong ProjectileLaunchCount => shotResultView != null ? shotResultView.ConfirmedProjectileLaunchCount : 0;
        public ulong ProjectileImpactCount => shotResultView != null ? shotResultView.ConfirmedProjectileImpactCount : 0;

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

            if (preparedWorld == null && !TryPrepareSimulation(out string preparationError))
            {
                Debug.LogError(preparationError, this);
                enabled = false;
                return;
            }

            PlayerSimulationConfig config = preparedWorld.Configuration;
            PlayerState serverInitialState = new PlayerState
            {
                Tick = 0,
                Position = serverSpawnPosition,
                Velocity = Vector3.zero,
                IsGrounded = true
            };
            PlayerState clientInitialState = new PlayerState
            {
                Tick = 0,
                Position = hasLocalClient ? playerView.InitialPosition : serverSpawnPosition,
                Velocity = Vector3.zero,
                Yaw = hasLocalClient ? playerView.InitialYaw : 0f,
                IsGrounded = true
            };
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
                sessionManager = new NetworkMovementSessionManager(
                    networkBootstrap.Runtime.ServerTransport,
                    networkBootstrap.Runtime.ClientTransport,
                    serverInitialState, clientInitialState, config, tickRunner.TickDeltaTime,
                    predictionHistoryCapacity, inputRedundancyCount, serverInputWindowCapacity,
                    maxMissingInputWaitTicks, maxRepeatedMovementTicks,
                    positionErrorThreshold, velocityErrorThreshold,
                    serverSpawnSpacing: serverSpawnSpacing,
                    remoteSnapshotCapacity: remoteSnapshotCapacity,
                    remoteInterpolationDelayTicks: remoteInterpolationDelayTicks,
                    remoteMaxExtrapolationTicks: remoteMaxExtrapolationTicks,
                    remoteTeleportDistance: remoteTeleportDistance,
                    weaponConfig: preparedWorld.WeaponConfiguration,
                    deathmatchKillLimit: deathmatchKillLimit,
                    deathmatchDurationSeconds: deathmatchDurationSeconds,
                    deathmatchRespawnSeconds: deathmatchRespawnSeconds,
                    deathmatchReplicationIntervalSeconds: deathmatchReplicationIntervalSeconds,
                    simulationWorld: preparedWorld);
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
                inputCollector.ConfigureFireInterval(preparedWorld.WeaponConfiguration.FireIntervalTicks);
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
                shotResultView = GetComponent<ShotResultView>();
                if (shotResultView == null)
                {
                    shotResultView = gameObject.AddComponent<ShotResultView>();
                }
                deathmatchHud.Configure(sessionManager, tickRunner.TickDeltaTime, shotResultView);
                weaponViewWasActive = firstPersonWeaponView != null && firstPersonWeaponView.gameObject.activeSelf;
            }

            Debug.Log($"AFPS 定点世界已装配：{preparedWorld.Identity.WorldHash}，配置={preparedWorld.Identity.ConfigurationHash}。", this);
            initialized = true;
            if (hasLocalClient && CombatSmokeTestDriver.IsRequested)
            {
                var combat = gameObject.AddComponent<CombatSmokeTestDriver>();
                combat.Configure(this, inputCollector);
            }
            else if (hasLocalClient && NetworkSmokeTestDriver.IsRequested)
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
            UpdateTimeSync();
            RefreshClientAdmission();
            ConsumeShotResults();
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

        private void ConsumeShotResults()
        {
            if (!initialized || shotResultView == null)
            {
                return;
            }

            shotResultView.Advance(Time.unscaledDeltaTime);
            uint localEntityId = sessionManager.RemotePlayers != null ? sessionManager.RemotePlayers.LocalEntityId : 0;
            if (sessionManager.ClientSession == null || localEntityId == 0)
            {
                return;
            }
            // 每帧排空队列，而非只读取 LastReceivedShotResult，避免同时命中/连发覆盖反馈。
            double estimatedTick = sessionManager.TryGetSynchronizedServerTick(out double synchronizedTick)
                ? synchronizedTick
                : double.NaN;
            while (sessionManager.TryDequeueShotRejection(out var rejection))
            {
                shotResultView.Reject(rejection);
            }
            while (sessionManager.TryDequeueShotPresentation(out var result))
            {
                shotResultView.Consume(result, localEntityId, estimatedTick, preparedWorld.TickDeltaTime.ToFloat());
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
                if (sessionManager.ClientWeapon.HasState && inputWeaponId != sessionManager.ClientWeapon.State.WeaponId)
                {
                    inputWeaponId = sessionManager.ClientWeapon.State.WeaponId;
                    inputCollector.ConfigureFireInterval(sessionManager.ClientWeaponConfiguration.FireIntervalTicks);
                }
                PlayerInputCommand command = inputCollector.ConsumeCommand(inputTick, sessionManager.ClientWeapon.CanFire);
                if (!IsLocalGameplayActive())
                {
                    command.MoveX = 0f;
                    command.MoveY = 0f;
                    command.JumpPressed = false;
                    command.FirePressed = false;
                    command.ReloadPressed = false;
                    command.SwitchWeaponPressed = false;
                    command.ShotSequence = 0;
                }
                // 换弹优先；只限制本地动作与表现，最终弹量/许可始终由权威快照确认。
                command.ReloadPressed &= sessionManager.ClientWeapon.CanReload && !command.SwitchWeaponPressed;
                if (command.FirePressed && (command.SwitchWeaponPressed || command.ReloadPressed || !sessionManager.ClientWeapon.CanFire))
                {
                    command.FirePressed = false;
                    command.ShotSequence = 0;
                    command.ShotServerTick = 0;
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
                    if (command.FirePressed)
                    {
                        PredictProjectileVisual(command, predictedState, serverWorldTick);
                    }
                }
            }

            sessionManager.AdvanceServerSessions(serverWorldTick);
            RefreshClientAdmission();
        }

        private void PredictProjectileVisual(in PlayerInputCommand command, in PlayerState state, uint serverWorldTick)
        {
            var weapon = sessionManager.ClientWeaponConfiguration;
            uint entityId = sessionManager.RemotePlayers != null ? sessionManager.RemotePlayers.LocalEntityId : 0;
            if (shotResultView == null || entityId == 0 || weapon.FireMode != WeaponFireMode.Projectile
                || !WeaponAim.TryGetDirection(state.YawFixedDegrees, state.PitchFixedDegrees, out FixVec3 direction))
            {
                return;
            }
            // 只为视觉复用共用眼高与瞄准规则；宽 Raw 检查与服务器一致，越域不创建代理。
            long eyeY = (long)state.PositionFixed.Y.Raw + weapon.EyeHeightFixed.Raw;
            if (eyeY < int.MinValue || eyeY > int.MaxValue)
            {
                return;
            }
            var origin = new FixVec3(state.PositionFixed.X, Fix.FromRaw((int)eyeY), state.PositionFixed.Z);
            var shot = new AuthoritativeShot(entityId, command.ShotSequence, serverWorldTick,
                weapon, origin, direction, command.ShotServerTick);
            shotResultView.PredictProjectile(shot);
        }

        private void UpdateTimeSync()
        {
            if (initialized)
            {
                sessionManager.AdvanceTimeSync(tickRunner.CurrentTick, tickRunner.TickAlpha);
            }
        }

        private void HandleTransportEvent(NetworkTransportSide side, GameTransportEvent transportEvent, ArraySegment<byte> payload)
        {
            UpdateTimeSync();
            switch (transportEvent.Type)
            {
                case TransportEventType.Connected:
                    sessionManager.HandleConnected(side, transportEvent.ConnectionId);
                    break;
                case TransportEventType.Disconnected:
                    sessionManager.HandleDisconnected(side, transportEvent.ConnectionId);
                    break;
                case TransportEventType.Data:
                    HandleData(side, transportEvent.ConnectionId, payload);
                    break;
            }
            RefreshClientAdmission();
        }

        private void RefreshClientAdmission()
        {
            if (!initialized)
            {
                return;
            }

            ClientPredictedMovementSession client = sessionManager.ClientSession;
            // 传输 Connected 早于世界准入，直到预测会话真正建立才初始化视图，且只初始化一次。
            if (client != presentedClientSession)
            {
                presentedClientSession = client;
                inputWeaponId = 0;
                if (shotResultView != null)
                {
                    shotResultView.Clear();
                }
                if (client != null && playerView != null)
                {
                    playerView.SnapToState(client.CurrentState);
                    inputCollector.SetLookAngles(client.CurrentState.Yaw, client.CurrentState.Pitch);
                }
            }

            ConnectionAdmissionStatus status = sessionManager.ClientAdmissionStatus;
            if (status == observedAdmissionStatus)
            {
                return;
            }

            ConnectionAdmissionStatus previousStatus = observedAdmissionStatus;
            observedAdmissionStatus = status;
            // 失败原因保留到下一次握手，避免紧接着到达的 Disconnected 将诊断覆盖。
            if (status == ConnectionAdmissionStatus.Disconnected
                && previousStatus >= ConnectionAdmissionStatus.AlgorithmMismatch)
            {
                return;
            }

            ConnectionStatusMessage = status switch
            {
                ConnectionAdmissionStatus.AwaitingHandshake => "Checking simulation world...",
                ConnectionAdmissionStatus.Admitted => "World accepted. Waiting for player assignment...",
                ConnectionAdmissionStatus.AlgorithmMismatch => "Connection rejected: simulation algorithm mismatch.",
                ConnectionAdmissionStatus.WorldMismatch => "Connection rejected: map snapshot mismatch.",
                ConnectionAdmissionStatus.ConfigurationMismatch => "Connection rejected: movement configuration mismatch.",
                ConnectionAdmissionStatus.TimedOut => "Connection rejected: admission timed out.",
                ConnectionAdmissionStatus.SessionCreationFailed => "Connection rejected: player session creation failed.",
                _ => "Connection lost. Restart the game to try again."
            };
            Debug.Log($"AFPS 准入状态：{status}；{ConnectionStatusMessage}；服务器身份={sessionManager.ExpectedServerIdentity}", this);
        }

        private void HandleData(NetworkTransportSide side, TransportConnectionId connectionId, ArraySegment<byte> payload)
        {
            bool handled = sessionManager.TryHandleData(side, connectionId, payload, out ReconciliationResult reconciliation);
            RefreshClientAdmission();
            if (!handled || side != NetworkTransportSide.Client || sessionManager.ClientSession == null
                || !PacketHeaderCodec.TryRead(payload, out PacketHeader header)
                || header.MessageType != NetworkMessageType.AuthoritativePlayerState)
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
            double renderTick = sessionManager.TryGetSynchronizedServerTick(out double synchronizedTick)
                ? synchronizedTick
                : latestRemoteServerTick + elapsedTicks;
            remotePlayerWorldView.Render(sessionManager.RemotePlayers, renderTick, sessionManager.ClientDeathmatchState);
        }

        private bool IsLocalGameplayActive()
        {
            if (!CanEnterGame)
            {
                return false;
            }

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

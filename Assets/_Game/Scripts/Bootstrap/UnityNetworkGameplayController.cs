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
using AFPS.Simulation.World;
using UnityEngine;

namespace AFPS.Bootstrap
{
    /// <summary>
    /// 串联Unity输入、Tick、会话协调器、大厅门禁、角色/射击表现及对局HUD。
    /// 传输事件仍只由 UnityNetworkBootstrap 轮询，本组件只同步消费其回调。
    /// </summary>
    // 保留旧组件类型的迁移信息；文件与.meta成对移动，场景/Prefab继续沿用原GUID。
    [UnityEngine.Scripting.APIUpdating.MovedFrom(
        true, sourceNamespace: "AFPS.Bootstrap", sourceAssembly: "_Game.Bootstrap",
        sourceClassName: "UnityNetworkMovementController")]
    public sealed class UnityNetworkGameplayController : MonoBehaviour
    {
        [SerializeField] private UnityNetworkBootstrap networkBootstrap;
        [SerializeField] private SimulationTickRunner tickRunner;
        [SerializeField] private LocalPlayerInputCollector inputCollector;
        [SerializeField] private LocalPlayerView playerView;
        [SerializeField] private FirstPersonCameraRig firstPersonCameraRig;
        [SerializeField] private FirstPersonWeaponView firstPersonWeaponView;
        private GrappleRopeView grappleRopeView;
        public bool HasPresentedGrappleRope => grappleRopeView != null && grappleRopeView.IsVisible;
        public int PresentedRemoteGrappleRopeCount => remotePlayerWorldView != null
            ? remotePlayerWorldView.VisibleGrappleRopeCount : 0;
        public int PresentedRemoteCharacterCount => remotePlayerWorldView != null
            ? remotePlayerWorldView.CharacterVisualCount : 0;
        [SerializeField] private RemotePlayerViewRegistry remotePlayerWorldView;
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

        private NetworkSessionCoordinator sessionCoordinator;
        private bool initialized;
        private uint observedRemoteSnapshotVersion;
        private uint latestRemoteServerTick;
        private double latestRemoteSnapshotReceiveTime;
        private GameplayHud deathmatchHud;
        private ShotPresentationController shotResultView;
        private ushort inputWeaponId;
        private bool weaponViewWasActive;
        private FootstepAudioView localFootsteps;
        private SimulationWorldContext preparedWorld;
        private ClientPredictedMovementSession presentedClientSession;
        private ConnectionAdmissionStatus observedAdmissionStatus;

        public string ConnectionStatusMessage { get; private set; }
        /// <summary>仅用于启动菜单的发现兼容性提示；实际准入仍由完整握手校验。</summary>
        public SimulationWorldIdentity? PreparedWorldIdentity => preparedWorld?.Identity;
        public ushort PresentedWeaponId => firstPersonWeaponView != null && firstPersonWeaponView.HasOwnedModel
            ? firstPersonWeaponView.WeaponId : (ushort)0;
        public bool PresentedReloading => firstPersonWeaponView != null && firstPersonWeaponView.Reloading;
        public ulong RemoteMuzzleCount => shotResultView != null ? shotResultView.RemoteMuzzleCount : 0;
        public ulong ExplosionEffectCount => shotResultView != null ? shotResultView.ExplosionEffectCount : 0;
        public ulong LocalFootstepCount => localFootsteps != null ? localFootsteps.StepCount : 0;
        public ulong RemoteFootstepCount => remotePlayerWorldView != null ? remotePlayerWorldView.FootstepCount : 0;

        public bool CanEnterGame => initialized
            && sessionCoordinator.ClientAdmissionStatus == ConnectionAdmissionStatus.Admitted
            && sessionCoordinator.RemotePlayers != null
            && sessionCoordinator.RemotePlayers.LocalEntityId != 0;

        /// <summary>启动传输前检查快照和配置；三个网络角色使用完全相同的加载入口。</summary>
        public bool TryPrepareSimulation(out string error)
        {
            preparedWorld = null;
            if (fixedWorldSnapshot == null || tickRunner == null)
            {
                error = "缺少已烘焙地图或逻辑时钟，无法启动。";
                return false;
            }

            try
            {
                CharacterCollisionConfig collision = new CharacterCollisionConfig(
                    capsuleRadius, capsuleHeight, collisionSkinWidth,
                    groundProbeDistance, stepHeight, maxSlopeAngle, maxSlideIterations);
                PlayerMovementConfig configuration = new PlayerMovementConfig(
                    maxGroundSpeed, groundAcceleration, gravity, jumpSpeed, collision);
                var spatial = new SimulationSessionSpatialConfig(
                    new FixVec3(Fix.FromFloat(serverSpawnPosition.x),
                        Fix.FromFloat(serverSpawnPosition.y), Fix.FromFloat(serverSpawnPosition.z)),
                    Fix.FromFloat(serverSpawnSpacing), SimulationSessionSpatialConfig.Default.InterestRadius);
                Fix simulationStep = Fix.FromFloat(tickRunner.TickDeltaTime);
                if (GrappleSmokeTestDriver.IsRequested)
                {
                    preparedWorld = GrappleSmokeTestDriver.CreateFixture(configuration, simulationStep, collisionLayers.value);
                }
                else if (ClimbSmokeTestDriver.IsRequested)
                {
                    preparedWorld = ClimbSmokeTestDriver.CreateFixture(configuration, simulationStep, collisionLayers.value);
                }
                else if (WallRunSmokeTestDriver.IsRequested)
                {
                    preparedWorld = WallRunSmokeTestDriver.CreateFixture(configuration, simulationStep, collisionLayers.value);
                }
                else if (WallSmokeTestDriver.IsRequested)
                {
                    preparedWorld = WallSmokeTestDriver.CreateFixture(configuration, simulationStep, collisionLayers.value);
                }
                else if (StanceSmokeTestDriver.IsRequested)
                {
                    preparedWorld = StanceSmokeTestDriver.CreateFixture(configuration, simulationStep, collisionLayers.value);
                }
                else
                {
                    preparedWorld = SimulationWorldContext.Load(
                        new ArraySegment<byte>(fixedWorldSnapshot.bytes), configuration,
                        simulationStep, collisionLayers.value, spatial);
                }
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
        public NetworkSessionCoordinator SessionCoordinator => sessionCoordinator;
        public SimulationWorldContext PreparedWorld => preparedWorld;
        public SimulationWorldContext RuntimeWorld => sessionCoordinator?.WorldContext;
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
                Debug.LogError("UnityNetworkGameplayController 缺少网络启动器或 TickRunner。", this);
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
                Debug.LogError("Client/Host 模式需要本地输入采集器和 LocalPlayerView。", this);
                enabled = false;
                return;
            }

            if (preparedWorld == null && !TryPrepareSimulation(out string preparationError))
            {
                Debug.LogError(preparationError, this);
                enabled = false;
                return;
            }

            PlayerMovementConfig config = preparedWorld.MovementConfiguration;
            PlayerMovementState serverInitialState = new PlayerMovementState
            {
                Tick = 0,
                PositionFixed = preparedWorld.SessionSpatial.SpawnOrigin,
                Velocity = Vector3.zero,
                IsGrounded = true
            };
            PlayerMovementState clientInitialState = new PlayerMovementState
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
                sessionCoordinator = new NetworkSessionCoordinator(
                    networkBootstrap.Runtime.ServerTransport,
                    networkBootstrap.Runtime.ClientTransport,
                    serverInitialState, clientInitialState, config, tickRunner.TickDeltaTime,
                    predictionHistoryCapacity, inputRedundancyCount, serverInputWindowCapacity,
                    maxMissingInputWaitTicks, maxRepeatedMovementTicks,
                    positionErrorThreshold, velocityErrorThreshold,
                    // 会话与已验证世界共享空间参数，测试夹具也必须经过相同指纹校验。
                    serverSpawnSpacing: preparedWorld.SessionSpatial.SpawnSpacing.ToFloat(),
                    interestRadius: preparedWorld.SessionSpatial.InterestRadius.ToFloat(),
                    remoteSnapshotCapacity: remoteSnapshotCapacity,
                    remoteInterpolationDelayTicks: remoteInterpolationDelayTicks,
                    remoteMaxExtrapolationTicks: remoteMaxExtrapolationTicks,
                    remoteTeleportDistance: remoteTeleportDistance,
                    weaponConfig: preparedWorld.WeaponConfiguration,
                    deathmatchKillLimit: deathmatchKillLimit,
                    deathmatchDurationSeconds: deathmatchDurationSeconds,
                    deathmatchRespawnSeconds: deathmatchRespawnSeconds,
                    deathmatchReplicationIntervalSeconds: deathmatchReplicationIntervalSeconds,
                    simulationWorld: preparedWorld,
                    isLocalPractice: networkBootstrap.ActiveMode == NetworkLaunchMode.Practice);
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
                    if (firstPersonWeaponView == null)
                    {
                        var weaponRoot = new GameObject("First Person Weapon");
                        weaponRoot.transform.SetParent(UnityEngine.Camera.main.transform, false);
                        firstPersonWeaponView = weaponRoot.AddComponent<FirstPersonWeaponView>();
                        firstPersonWeaponView.Configure(UnityEngine.Camera.main);
                    }
                }

                if (remotePlayerWorldView == null)
                {
                    remotePlayerWorldView = GetComponent<RemotePlayerViewRegistry>();
                }

                if (remotePlayerWorldView == null)
                {
                    remotePlayerWorldView = gameObject.AddComponent<RemotePlayerViewRegistry>();
                }

                deathmatchHud = GetComponent<GameplayHud>();
                if (deathmatchHud == null)
                {
                    deathmatchHud = gameObject.AddComponent<GameplayHud>();
                }
                shotResultView = GetComponent<ShotPresentationController>();
                if (shotResultView == null)
                {
                    shotResultView = gameObject.AddComponent<ShotPresentationController>();
                }
                deathmatchHud.Configure(sessionCoordinator, tickRunner.TickDeltaTime, shotResultView);
                weaponViewWasActive = firstPersonWeaponView != null && firstPersonWeaponView.gameObject.activeSelf;
                localFootsteps = gameObject.AddComponent<FootstepAudioView>();
            }

            Debug.Log($"AFPS 定点世界已装配：{preparedWorld.Identity.WorldHash}，配置={preparedWorld.Identity.ConfigurationHash}。", this);
            initialized = true;
            // 显式CLI启动也需要玩家确认准备，不能因绕过连接菜单而静默自动开局。
            if (hasLocalClient && !sessionCoordinator.IsLocalPractice && GetComponent<NetworkLobbyMenu>() == null)
            {
                gameObject.AddComponent<NetworkLobbyMenu>().Initialize(networkBootstrap);
            }
            var debugPanel = GetComponent<NetworkDebugPanel>();
            if (debugPanel == null)
            {
                debugPanel = gameObject.AddComponent<NetworkDebugPanel>();
            }
            debugPanel.Configure(sessionCoordinator, networkBootstrap);
            if (networkBootstrap.Runtime.ServerTransport != null && AuthoritativeMovementTraceRecorder.IsRequested)
            {
                gameObject.AddComponent<AuthoritativeMovementTraceRecorder>().Configure(sessionCoordinator);
            }
            if (hasLocalClient && GrappleSmokeTestDriver.IsRequested)
            {
                var grapple = gameObject.AddComponent<GrappleSmokeTestDriver>();
                grapple.Configure(this, inputCollector);
            }
            else if (hasLocalClient && ClimbSmokeTestDriver.IsRequested)
            {
                var climb = gameObject.AddComponent<ClimbSmokeTestDriver>();
                climb.Configure(this, inputCollector);
            }
            else if (hasLocalClient && WallRunSmokeTestDriver.IsRequested)
            {
                var wallRun = gameObject.AddComponent<WallRunSmokeTestDriver>();
                wallRun.Configure(this, inputCollector);
            }
            else if (hasLocalClient && StanceSmokeTestDriver.IsRequested)
            {
                var stance = gameObject.AddComponent<StanceSmokeTestDriver>();
                stance.Configure(this, inputCollector);
            }
            else if (hasLocalClient && WallSmokeTestDriver.IsRequested)
            {
                var wall = gameObject.AddComponent<WallSmokeTestDriver>();
                wall.Configure(this, inputCollector);
            }
            else if (hasLocalClient && JumpSmokeTestDriver.IsRequested)
            {
                var jump = gameObject.AddComponent<JumpSmokeTestDriver>();
                jump.Configure(this, inputCollector);
            }
            else if (hasLocalClient && DashSmokeTestDriver.IsRequested)
            {
                var dash = gameObject.AddComponent<DashSmokeTestDriver>();
                dash.Configure(this, inputCollector);
            }
            else if (hasLocalClient && SprintSmokeTestDriver.IsRequested)
            {
                var sprint = gameObject.AddComponent<SprintSmokeTestDriver>();
                sprint.Configure(this, inputCollector);
            }
            else if (hasLocalClient && CombatSmokeTestDriver.IsRequested)
            {
                var combat = gameObject.AddComponent<CombatSmokeTestDriver>();
                combat.Configure(this, inputCollector);
            }
            else if (hasLocalClient && NetworkGameplaySmokeTestDriver.IsRequested)
            {
                NetworkGameplaySmokeTestDriver smokeTest = gameObject.AddComponent<NetworkGameplaySmokeTestDriver>();
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
            if (grappleRopeView != null && (!initialized || sessionCoordinator?.ClientSession == null))
            {
                grappleRopeView.Hide();
            }
            UpdateTimeSync();
            RefreshClientAdmission();
            ConsumeShotResults();
            if (initialized && playerView != null && sessionCoordinator.ClientSession != null)
            {
                playerView.Render(tickRunner.TickAlpha, tickRunner.TickDeltaTime, Time.unscaledDeltaTime);
                if (grappleRopeView == null)
                {
                    grappleRopeView = gameObject.AddComponent<GrappleRopeView>();
                }
                grappleRopeView.Render(sessionCoordinator.ClientSession.CurrentState,
                    playerView.SimulationTransform.position, preparedWorld.MovementConfiguration, IsLocalGameplayActive());
                if (firstPersonCameraRig != null && firstPersonCameraRig.isActiveAndEnabled)
                {
                    PlayerMovementState cameraState = sessionCoordinator.ClientSession.CurrentState;
                    var cameraConfig = preparedWorld.MovementConfiguration;
                    float stanceRatio = cameraState.IsCrouched
                        ? (cameraConfig.Stance.Height / cameraConfig.Collision.HeightFixed).ToFloat() : 1f;
                    firstPersonCameraRig.SetStance(stanceRatio);
                    firstPersonCameraRig.Render(inputCollector.LookYaw, inputCollector.LookPitch);
                }

                if (firstPersonWeaponView != null && playerView.TryGetLatestState(out PlayerMovementState localState))
                {
                    bool active = IsLocalGameplayActive();
                    if (firstPersonWeaponView.gameObject.activeSelf != active && (active || weaponViewWasActive))
                    {
                        firstPersonWeaponView.gameObject.SetActive(active);
                    }
                    if (active)
                    {
                        firstPersonWeaponView.SetWeapon(sessionCoordinator.ClientWeaponConfiguration);
                        firstPersonWeaponView.SetReloading(sessionCoordinator.ClientWeapon.IsReloading);
                        firstPersonWeaponView.Render(localState, Time.unscaledDeltaTime);
                    }
                }
                if (firstPersonCameraRig != null && firstPersonCameraRig.isActiveAndEnabled)
                {
                    float lateralSpeed = playerView.TryGetLatestState(out PlayerMovementState cameraState)
                        ? Vector3.Dot(cameraState.Velocity, firstPersonCameraRig.transform.right) : 0f;
                    firstPersonCameraRig.RenderCombat(firstPersonWeaponView != null && firstPersonWeaponView.isActiveAndEnabled
                        ? firstPersonWeaponView.RecoilWeight : 0f, IsLocalGameplayActive() ? lateralSpeed : 0f,
                        Time.unscaledDeltaTime);
                }
                if (sessionCoordinator.ClientDeathmatchState != null
                    && sessionCoordinator.ClientDeathmatchState.Phase == DeathmatchPhase.Finished && shotResultView != null)
                {
                    shotResultView.CancelInFlight();
                }

                RenderRemotePlayers();
                if (localFootsteps != null && playerView.TryGetLatestState(out PlayerMovementState footstepState))
                {
                    localFootsteps.Render(footstepState.Position, footstepState.IsGrounded, IsLocalGameplayActive());
                }
            }
        }

        private void ConsumeShotResults()
        {
            if (!initialized || shotResultView == null)
            {
                return;
            }

            shotResultView.Advance(Time.unscaledDeltaTime);
            uint localEntityId = sessionCoordinator.RemotePlayers != null ? sessionCoordinator.RemotePlayers.LocalEntityId : 0;
            if (sessionCoordinator.ClientSession == null || localEntityId == 0)
            {
                return;
            }
            // 每帧排空队列，而非只读取 LastReceivedShotResult，避免同时命中/连发覆盖反馈。
            double estimatedTick = sessionCoordinator.TryGetSynchronizedServerTick(out double synchronizedTick)
                ? synchronizedTick
                : double.NaN;
            while (sessionCoordinator.TryDequeueShotRejection(out var rejection))
            {
                shotResultView.Reject(rejection);
            }
            while (sessionCoordinator.TryDequeueShotPresentation(out var result))
            {
                shotResultView.Consume(result, localEntityId, estimatedTick, preparedWorld.TickDeltaTime.ToFloat());
                if (result.Shot.EntityId != localEntityId)
                {
                    remotePlayerWorldView?.PresentShot(result.Shot);
                }
            }
        }

        private void HandleTickOccurred(uint serverWorldTick, float tickDeltaTime)
        {
            if (!initialized)
            {
                return;
            }

            ClientPredictedMovementSession clientSession = sessionCoordinator.ClientSession;
            if (clientSession != null && sessionCoordinator.CanPredictClientInput)
            {
                uint inputTick = unchecked(clientSession.CurrentState.Tick + 1);
                if (sessionCoordinator.ClientWeapon.HasState && inputWeaponId != sessionCoordinator.ClientWeapon.State.WeaponId)
                {
                    inputWeaponId = sessionCoordinator.ClientWeapon.State.WeaponId;
                    inputCollector.ConfigureFireInterval(sessionCoordinator.ClientWeaponConfiguration.FireIntervalTicks);
                }
                PlayerInputCommand command = inputCollector.ConsumeCommand(inputTick, sessionCoordinator.ClientWeapon.CanFire);
                if (!IsLocalGameplayActive())
                {
                    command.MoveX = 0f;
                    command.MoveY = 0f;
                    command.JumpPressed = false;
                    command.SprintHeld = false;
                    command.DashPressed = false;
                    command.WallBouncePressed = false;
                    command.CrouchHeld = false;
                    command.GrappleHeld = false;
                    command.FirePressed = false;
                    command.ReloadPressed = false;
                    command.SwitchWeaponPressed = false;
                    command.ShotSequence = 0;
                }
                // 换弹优先；只限制本地动作与表现，最终弹量/许可始终由权威快照确认。
                command.ReloadPressed &= sessionCoordinator.ClientWeapon.CanReload && !command.SwitchWeaponPressed;
                if (command.FirePressed && (command.SwitchWeaponPressed || command.ReloadPressed || !sessionCoordinator.ClientWeapon.CanFire))
                {
                    command.FirePressed = false;
                    command.ShotSequence = 0;
                    command.ShotServerTick = 0;
                }
                if (command.FirePressed)
                {
                    command.ShotServerTick = sessionCoordinator.RemotePlayers != null && sessionCoordinator.RemotePlayers.LatestServerTick != 0 ? sessionCoordinator.RemotePlayers.LatestServerTick : serverWorldTick;
                }

                if (sessionCoordinator.TryPredictAndSend(command, out PlayerMovementState predictedState, out _))
                {
                    playerView.ApplyState(predictedState);
                    if (command.FirePressed)
                    {
                        if (firstPersonWeaponView != null)
                        {
                            if (shotResultView != null && firstPersonWeaponView.TryGetWorldMuzzle(out var muzzle))
                            {
                                uint localEntity = sessionCoordinator.RemotePlayers.LocalEntityId;
                                shotResultView.RememberLocalMuzzle(localEntity, command.ShotSequence, muzzle);
                            }
                            firstPersonWeaponView.PlayPredictedShot();
                        }
                        PredictProjectileVisual(command, predictedState, serverWorldTick);
                    }
                }
            }

            bool wasRecovering = sessionCoordinator.ClientRecoveryPending;
            if (wasRecovering)
            {
                inputCollector.DiscardPendingActions();
            }
            sessionCoordinator.AdvanceServerSessions(serverWorldTick);
            if (wasRecovering && sessionCoordinator.ClientSession != null && playerView != null)
            {
                playerView.ApplyState(sessionCoordinator.ClientSession.CurrentState);
            }
            RefreshClientAdmission();
        }

        private void PredictProjectileVisual(in PlayerInputCommand command, in PlayerMovementState state, uint serverWorldTick)
        {
            var weapon = sessionCoordinator.ClientWeaponConfiguration;
            uint entityId = sessionCoordinator.RemotePlayers != null ? sessionCoordinator.RemotePlayers.LocalEntityId : 0;
            if (shotResultView == null || entityId == 0 || weapon.FireMode != WeaponFireMode.Projectile
                || !WeaponAim.TryGetDirection(state.YawFixedDegrees, state.PitchFixedDegrees, out FixVec3 direction))
            {
                return;
            }
            // 只为视觉复用共用眼高与瞄准规则；宽 Raw 检查与服务器一致，越域不创建代理。
            long eyeY = (long)state.PositionFixed.Y.Raw
                + PlayerStanceSimulation.EyeHeight(state, weapon.EyeHeightFixed, preparedWorld.MovementConfiguration).Raw;
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
                sessionCoordinator.AdvanceTimeSync(tickRunner.CurrentTick, tickRunner.TickAlpha);
            }
        }

        private void HandleTransportEvent(NetworkTransportSide side, GameTransportEvent transportEvent, ArraySegment<byte> payload)
        {
            UpdateTimeSync();
            switch (transportEvent.Type)
            {
                case TransportEventType.Connected:
                    sessionCoordinator.HandleConnected(side, transportEvent.ConnectionId);
                    break;
                case TransportEventType.Disconnected:
                    sessionCoordinator.HandleDisconnected(side, transportEvent.ConnectionId);
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

            ClientPredictedMovementSession client = sessionCoordinator.ClientSession;
            // 传输 Connected 早于世界准入，直到预测会话真正建立才初始化视图，且只初始化一次。
            if (client != presentedClientSession)
            {
                presentedClientSession = client;
                inputWeaponId = 0;
                if (localFootsteps != null)
                {
                    localFootsteps.Clear();
                }
                if (client == null && firstPersonWeaponView != null)
                {
                    firstPersonWeaponView.StopPresentation();
                    firstPersonWeaponView.gameObject.SetActive(false);
                }
                if (client == null && firstPersonCameraRig != null)
                {
                    firstPersonCameraRig.ResetCombat();
                }
                if (shotResultView != null)
                {
                    shotResultView.Clear();
                }
                if (client != null && playerView != null)
                {
                    if (sessionCoordinator.ClientRecoveryPending)
                    {
                        inputCollector.RestoreShotSequence(sessionCoordinator.ClientShotSequenceFloor);
                    }
                    playerView.SnapToState(client.CurrentState);
                    inputCollector.SetLookAngles(client.CurrentState.Yaw, client.CurrentState.Pitch);
                }
            }

            ConnectionAdmissionStatus status = sessionCoordinator.ClientAdmissionStatus;
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
                ConnectionAdmissionStatus.AwaitingHandshake => "正在检查模拟世界…",
                ConnectionAdmissionStatus.Admitted => "地图验证通过，正在分配玩家…",
                ConnectionAdmissionStatus.AlgorithmMismatch => "连接被拒绝：模拟算法版本不一致。",
                ConnectionAdmissionStatus.WorldMismatch => "连接被拒绝：地图快照不一致。",
                ConnectionAdmissionStatus.ConfigurationMismatch => "连接被拒绝：玩法配置不一致。",
                ConnectionAdmissionStatus.TimedOut => "连接被拒绝：入局验证超时。",
                ConnectionAdmissionStatus.SessionCreationFailed => "连接被拒绝：玩家会话创建失败。",
                _ => "连接已断开，请重新启动游戏再试。"
            };
            Debug.Log($"AFPS 准入状态：{status}；{ConnectionStatusMessage}；服务器身份={sessionCoordinator.ExpectedServerIdentity}", this);
        }

        private void HandleData(NetworkTransportSide side, TransportConnectionId connectionId, ArraySegment<byte> payload)
        {
            bool handled = sessionCoordinator.TryHandleData(side, connectionId, payload, out MovementReconciliationResult reconciliation);
            RefreshClientAdmission();
            if (!handled || side != NetworkTransportSide.Client || sessionCoordinator.ClientSession == null
                || !PacketHeaderCodec.TryRead(payload, out PacketHeader header)
                || header.MessageType != NetworkMessageType.AuthoritativeMovementState)
            {
                return;
            }

            PlayerMovementState state = sessionCoordinator.ClientSession.CurrentState;
            if (reconciliation.RequiresHardCorrection)
            {
                playerView.SnapToState(state);
            }
            else if (reconciliation.Status == MovementReconciliationStatus.Corrected)
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
            if (remotePlayerWorldView == null || sessionCoordinator.RemotePlayers == null)
            {
                return;
            }

            if (observedRemoteSnapshotVersion != sessionCoordinator.RemotePlayers.SnapshotVersion)
            {
                observedRemoteSnapshotVersion = sessionCoordinator.RemotePlayers.SnapshotVersion;
                latestRemoteServerTick = sessionCoordinator.RemotePlayers.LatestServerTick;
                latestRemoteSnapshotReceiveTime = Time.realtimeSinceStartupAsDouble;
            }

            if (observedRemoteSnapshotVersion == 0)
            {
                remotePlayerWorldView.Render(sessionCoordinator.RemotePlayers, 0d, sessionCoordinator.ClientDeathmatchState);
                return;
            }

            double elapsedTicks = (Time.realtimeSinceStartupAsDouble - latestRemoteSnapshotReceiveTime) / tickRunner.TickDeltaTime;
            double renderTick = sessionCoordinator.TryGetSynchronizedServerTick(out double synchronizedTick)
                ? synchronizedTick
                : latestRemoteServerTick + elapsedTicks;
            var remoteConfig = preparedWorld.MovementConfiguration;
            remotePlayerWorldView.Render(sessionCoordinator.RemotePlayers, renderTick, sessionCoordinator.ClientDeathmatchState,
                (remoteConfig.Stance.Height / remoteConfig.Collision.HeightFixed).ToFloat(),
                FixMath.Min(remoteConfig.Grapple.OriginHeight, remoteConfig.Collision.HeightFixed).ToFloat(),
                sessionCoordinator.RemoteWeapons, WeaponCatalog.SecondaryFor(preparedWorld.WeaponConfiguration).WeaponId);
        }

        private bool IsLocalGameplayActive()
        {
            if (!CanEnterGame || !sessionCoordinator.CanPredictClientInput || !sessionCoordinator.CanEnterMatch)
            {
                return false;
            }

            DeathmatchState match = sessionCoordinator.ClientDeathmatchState;
            if (match == null)
            {
                return true;
            }

            uint localEntityId = sessionCoordinator.RemotePlayers != null ? sessionCoordinator.RemotePlayers.LocalEntityId : 0;
            return match.Phase == DeathmatchPhase.Playing && match.TryGetPlayer(localEntityId, out DeathmatchPlayerState local) && local.IsAlive;
        }
    }
}

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
        private GrappleRopeView grappleRopeView;
        public bool HasPresentedGrappleRope => grappleRopeView != null && grappleRopeView.IsVisible;
        public int PresentedRemoteGrappleRopeCount => remotePlayerWorldView != null
            ? remotePlayerWorldView.VisibleGrappleRopeCount : 0;
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
        private FootstepAudioView localFootsteps;
        private SimulationWorldContext preparedWorld;
        private ClientPredictedMovementSession presentedClientSession;
        private ConnectionAdmissionStatus observedAdmissionStatus;

        public string ConnectionStatusMessage { get; private set; }
        public ushort PresentedWeaponId => firstPersonWeaponView != null && firstPersonWeaponView.HasOwnedModel
            ? firstPersonWeaponView.WeaponId : (ushort)0;
        public bool PresentedReloading => firstPersonWeaponView != null && firstPersonWeaponView.Reloading;
        public ulong RemoteMuzzleCount => shotResultView != null ? shotResultView.RemoteMuzzleCount : 0;
        public ulong ExplosionEffectCount => shotResultView != null ? shotResultView.ExplosionEffectCount : 0;
        public ulong LocalFootstepCount => localFootsteps != null ? localFootsteps.StepCount : 0;
        public ulong RemoteFootstepCount => remotePlayerWorldView != null ? remotePlayerWorldView.FootstepCount : 0;

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
                error = "缺少已烘焙地图或逻辑时钟，无法启动。";
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
        public NetworkMovementSessionManager SessionManager => sessionManager;
        public SimulationWorldContext PreparedWorld => preparedWorld;
        public SimulationWorldContext RuntimeWorld => sessionManager?.WorldContext;
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
                PositionFixed = preparedWorld.SessionSpatial.SpawnOrigin,
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
                localFootsteps = gameObject.AddComponent<FootstepAudioView>();
            }

            Debug.Log($"AFPS 定点世界已装配：{preparedWorld.Identity.WorldHash}，配置={preparedWorld.Identity.ConfigurationHash}。", this);
            initialized = true;
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
            if (grappleRopeView != null && (!initialized || sessionManager?.ClientSession == null))
            {
                grappleRopeView.Hide();
            }
            UpdateTimeSync();
            RefreshClientAdmission();
            ConsumeShotResults();
            if (initialized && playerView != null && sessionManager.ClientSession != null)
            {
                playerView.Render(tickRunner.TickAlpha, tickRunner.TickDeltaTime, Time.unscaledDeltaTime);
                if (grappleRopeView == null)
                {
                    grappleRopeView = gameObject.AddComponent<GrappleRopeView>();
                }
                grappleRopeView.Render(sessionManager.ClientSession.CurrentState,
                    playerView.SimulationTransform.position, preparedWorld.Configuration, IsLocalGameplayActive());
                if (firstPersonCameraRig != null)
                {
                    PlayerState cameraState = sessionManager.ClientSession.CurrentState;
                    var cameraConfig = preparedWorld.Configuration;
                    float stanceRatio = cameraState.IsCrouched
                        ? (cameraConfig.Stance.Height / cameraConfig.Collision.HeightFixed).ToFloat() : 1f;
                    firstPersonCameraRig.SetStance(stanceRatio);
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
                        firstPersonWeaponView.SetWeapon(sessionManager.ClientWeaponConfiguration);
                        firstPersonWeaponView.SetReloading(sessionManager.ClientWeapon.IsReloading);
                        firstPersonWeaponView.Render(localState, Time.unscaledDeltaTime);
                    }
                }
                if (firstPersonCameraRig != null)
                {
                    float lateralSpeed = playerView.TryGetLatestState(out PlayerState cameraState)
                        ? Vector3.Dot(cameraState.Velocity, firstPersonCameraRig.transform.right) : 0f;
                    firstPersonCameraRig.RenderCombat(firstPersonWeaponView != null && firstPersonWeaponView.isActiveAndEnabled
                        ? firstPersonWeaponView.RecoilWeight : 0f, IsLocalGameplayActive() ? lateralSpeed : 0f);
                }
                if (sessionManager.ClientDeathmatchState != null
                    && sessionManager.ClientDeathmatchState.Phase == DeathmatchPhase.Finished && shotResultView != null)
                {
                    shotResultView.CancelInFlight();
                }

                RenderRemotePlayers();
                if (localFootsteps != null && playerView.TryGetLatestState(out PlayerState footstepState))
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
            if (clientSession != null && sessionManager.CanPredictClientInput)
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
                }

                if (sessionManager.TryPredictAndSend(command, out PlayerState predictedState, out _))
                {
                    playerView.ApplyState(predictedState);
                    if (command.FirePressed)
                    {
                        if (firstPersonWeaponView != null)
                        {
                            if (shotResultView != null && firstPersonWeaponView.TryGetWorldMuzzle(out var muzzle))
                            {
                                uint localEntity = sessionManager.RemotePlayers.LocalEntityId;
                                shotResultView.RememberLocalMuzzle(localEntity, command.ShotSequence, muzzle);
                            }
                            firstPersonWeaponView.PlayPredictedShot();
                        }
                        PredictProjectileVisual(command, predictedState, serverWorldTick);
                    }
                }
            }

            bool wasRecovering = sessionManager.ClientRecoveryPending;
            if (wasRecovering)
            {
                inputCollector.DiscardPendingActions();
            }
            sessionManager.AdvanceServerSessions(serverWorldTick);
            if (wasRecovering && sessionManager.ClientSession != null && playerView != null)
            {
                playerView.ApplyState(sessionManager.ClientSession.CurrentState);
            }
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
            long eyeY = (long)state.PositionFixed.Y.Raw
                + PlayerStanceSimulation.EyeHeight(state, weapon.EyeHeightFixed, preparedWorld.Configuration).Raw;
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
                    firstPersonCameraRig.RenderCombat(0f);
                }
                if (shotResultView != null)
                {
                    shotResultView.Clear();
                }
                if (client != null && playerView != null)
                {
                    if (sessionManager.ClientRecoveryPending)
                    {
                        inputCollector.RestoreShotSequence(sessionManager.ClientShotSequenceFloor);
                    }
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
                ConnectionAdmissionStatus.AwaitingHandshake => "正在检查模拟世界…",
                ConnectionAdmissionStatus.Admitted => "地图验证通过，正在分配玩家…",
                ConnectionAdmissionStatus.AlgorithmMismatch => "连接被拒绝：模拟算法版本不一致。",
                ConnectionAdmissionStatus.WorldMismatch => "连接被拒绝：地图快照不一致。",
                ConnectionAdmissionStatus.ConfigurationMismatch => "连接被拒绝：玩法配置不一致。",
                ConnectionAdmissionStatus.TimedOut => "连接被拒绝：入局验证超时。",
                ConnectionAdmissionStatus.SessionCreationFailed => "连接被拒绝：玩家会话创建失败。",
                _ => "连接已断开，请重新启动游戏再试。"
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
            var remoteConfig = preparedWorld.Configuration;
            remotePlayerWorldView.Render(sessionManager.RemotePlayers, renderTick, sessionManager.ClientDeathmatchState,
                (remoteConfig.Stance.Height / remoteConfig.Collision.HeightFixed).ToFloat(),
                FixMath.Min(remoteConfig.Grapple.OriginHeight, remoteConfig.Collision.HeightFixed).ToFloat());
        }

        private bool IsLocalGameplayActive()
        {
            if (!CanEnterGame || !sessionManager.CanPredictClientInput)
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

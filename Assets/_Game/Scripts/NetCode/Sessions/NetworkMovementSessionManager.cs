using System;
using System.Collections.Generic;
using System.Diagnostics;
using AFPS.Math;
using AFPS.NetCode.InputReplication;
using AFPS.NetCode.LagCompensation;
using AFPS.NetCode.Match;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Metrics;
using AFPS.NetCode.Prediction;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.Runtime;
using AFPS.NetCode.SnapshotInterpolation;
using AFPS.NetCode.StateReplication;
using AFPS.NetCode.Transport;
using AFPS.NetCode.Weapons;
using AFPS.Simulation.Characters;
using AFPS.Simulation.Characters.Collision;
using AFPS.Simulation.Weapons;
using UnityEngine;

namespace AFPS.NetCode.Sessions
{
    /// <summary>
    /// 根据传输连接生命周期维护一个客户端预测会话和每连接一个服务器权威会话。
    /// 调用方仍然是传输事件的唯一消费者，并在回调有效期内把数据包交给本类。
    /// </summary>
    public sealed partial class NetworkMovementSessionManager
    {
        private readonly Dictionary<TransportConnectionId, ServerAuthoritativeMovementSession> serverSessions = new Dictionary<TransportConnectionId, ServerAuthoritativeMovementSession>();
        private readonly Dictionary<TransportConnectionId, uint> serverEntityIds = new Dictionary<TransportConnectionId, uint>();
        private readonly Dictionary<uint, uint> snapshotSequences = new Dictionary<uint, uint>();
        private readonly Dictionary<uint, Fix> serverHealth = new Dictionary<uint, Fix>();
        private readonly Dictionary<uint, ushort> serverKills = new Dictionary<uint, ushort>();
        private readonly Dictionary<uint, ushort> serverDeaths = new Dictionary<uint, ushort>();
        private readonly Dictionary<uint, uint> serverRespawnTicks = new Dictionary<uint, uint>();
        private readonly Dictionary<uint, RewindHitboxHistory> rewindHistories = new Dictionary<uint, RewindHitboxHistory>();
        private readonly Dictionary<TransportConnectionId, Dictionary<uint, RemoteSnapshotSendState>> serverSnapshotStates =
            new Dictionary<TransportConnectionId, Dictionary<uint, RemoteSnapshotSendState>>();
        private readonly Dictionary<TransportConnectionId, HashSet<uint>> serverVisibleEntities = new Dictionary<TransportConnectionId, HashSet<uint>>();
        private readonly Dictionary<uint, RemotePlayerSnapshot> clientSnapshotBaselines = new Dictionary<uint, RemotePlayerSnapshot>();
        private readonly List<WeaponFireResult> pendingWeaponFireResults = new List<WeaponFireResult>();
        private readonly List<ServerCombatTarget> combatTargets = new List<ServerCombatTarget>();
        private readonly List<AuthoritativeShotResult> projectileImpacts = new List<AuthoritativeShotResult>();
        private readonly ServerProjectileSimulation projectileSimulation = new ServerProjectileSimulation();
        private readonly IGameTransport serverTransport;
        private readonly IGameTransport clientTransport;
        private readonly PlayerState serverInitialState;
        private readonly PlayerState clientInitialState;
        private readonly PlayerSimulationConfig simulationConfig;
        private readonly float tickDeltaTime;
        // 弹丸与移动使用同一规范步长（秒），构造边界只转换一次。
        private readonly Fix tickDeltaTimeFixed;
        private readonly int predictionHistoryCapacity;
        private readonly int inputRedundancyCount;
        private readonly int serverInputWindowCapacity;
        private readonly int maxMissingInputWaitTicks;
        private readonly int maxRepeatedMovementTicks;
        private readonly float positionErrorThreshold;
        private readonly float velocityErrorThreshold;
        private readonly ICharacterCollisionWorld collisionWorld;
        private readonly SimulationSessionSpatialConfig sessionSpatial;
        private readonly WeaponSimulationConfig weaponConfig;
        public WeaponSimulationConfig ClientWeaponConfiguration => ClientWeapon.HasState
            && ClientWeapon.State.WeaponId == WeaponCatalog.SecondaryFor(weaponConfig).WeaponId
                ? WeaponCatalog.SecondaryFor(weaponConfig) : weaponConfig;
        private readonly uint maximumRewindTicks;
        private readonly int snapshotBudgetPerClientPerTick;
        private readonly uint fullSnapshotIntervalTicks;
        private readonly int remoteSnapshotRate;
        private readonly int serverTickRate;
        private readonly Fix remoteTeleportDistanceFixed;
        private readonly List<SnapshotCandidate> snapshotCandidates = new List<SnapshotCandidate>();
        private readonly HashSet<uint> interestedEntityIds = new HashSet<uint>();
        private readonly List<uint> noLongerVisibleEntityIds = new List<uint>();
        private readonly List<uint> deathmatchEntityIds = new List<uint>();
        private readonly byte[] assignmentBuffer = new byte[PlayerSessionAssignmentCodec.PacketSize];
        private readonly byte[] despawnBuffer = new byte[PlayerDespawnCodec.PacketSize];
        private readonly byte[] snapshotBuffer = new byte[RemotePlayerSnapshotCodec.PacketSize];
        private readonly byte[] snapshotDeltaBuffer = new byte[RemotePlayerSnapshotDeltaCodec.MaximumPacketSize];
        private readonly byte[] shotResultBuffer = new byte[AuthoritativeShotResultCodec.PacketSize];
        private readonly byte[] deathmatchStateBuffer = new byte[DeathmatchStateCodec.MaximumPacketSize];
        private readonly int deathmatchKillLimit;
        private readonly uint deathmatchDurationTicks;
        private readonly uint deathmatchRespawnTicks;
        private readonly uint deathmatchReplicationIntervalTicks;
        private TransportConnectionId clientConnectionId;
        private uint nextEntityId = 1;
        private uint lifecycleSequence = 1;
        private uint shotResultPacketSequence = 1;
        private bool hasReceivedShotResultSequence;
        private uint lastReceivedShotResultSequence;
        private uint deathmatchPacketSequence = 1;
        private uint lastDeathmatchReplicationTick;
        private uint deathmatchStartTick;
        private uint deathmatchWinnerEntityId;
        private uint latestServerWorldTick;
        private bool deathmatchStateDirty = true;
        private bool hasReceivedDeathmatchSequence;
        private uint lastReceivedDeathmatchSequence;
        private DeathmatchPhase deathmatchPhase = DeathmatchPhase.WaitingForPlayers;

        /// <summary>
        /// 当前服务器侧维护的已连接玩家权威会话数量。
        /// </summary>
        public int ServerSessionCount => serverSessions.Count;
        /// <summary>只发布实际推进的步骤；等待输入时不生成伪帧，观察者不得修改对局状态。</summary>
        public event Action<AFPS.NetCode.Replay.AuthoritativeSimulationStep> AuthoritativeSimulationStepCompleted;
        public bool IsLocalPractice { get; }

        /// <summary>
        /// 当前客户端与服务器连接成功后创建的本地预测会话。
        /// </summary>
        public ClientPredictedMovementSession ClientSession { get; private set; }

        /// <summary>客户端连接期间保存的远端玩家快照集合；纯服务器进程中为 null。</summary>
        public RemotePlayerReplicaSet RemotePlayers { get; private set; }

        public AuthoritativeShotResult LastReceivedShotResult { get; private set; }

        public uint ShotResultVersion { get; private set; }

        public DeathmatchState ClientDeathmatchState { get; private set; }

        public uint DeathmatchStateVersion { get; private set; }

        public ClientWeaponPrediction ClientWeapon { get; } = new ClientWeaponPrediction();
        private bool hasWeaponStateSequence;
        private uint lastWeaponStateSequence;

        public NetworkRuntimeMetrics Metrics { get; } = new NetworkRuntimeMetrics();

        private readonly struct SnapshotCandidate
        {
            public readonly uint EntityId;
            public readonly PlayerState State;
            public readonly ulong DistanceSquared;
            public readonly bool IsUrgent;

            public SnapshotCandidate(uint entityId, in PlayerState state, ulong distanceSquared, bool isUrgent)
            {
                EntityId = entityId;
                State = state;
                DistanceSquared = distanceSquared;
                IsUrgent = isUrgent;
            }
        }

        public NetworkMovementSessionManager(
            IGameTransport serverTransport,
            IGameTransport clientTransport,
            in PlayerState serverInitialState,
            in PlayerState clientInitialState,
            in PlayerSimulationConfig simulationConfig,
            float tickDeltaTime,
            int predictionHistoryCapacity,
            int inputRedundancyCount,
            int serverInputWindowCapacity,
            int maxMissingInputWaitTicks,
            int maxRepeatedMovementTicks,
            float positionErrorThreshold,
            float velocityErrorThreshold,
            ICharacterCollisionWorld collisionWorld = null,
            float serverSpawnSpacing = 2.5f,
            int remoteSnapshotCapacity = 32,
            double remoteInterpolationDelayTicks = 2.0,
            double remoteMaxExtrapolationTicks = 2.0,
            float remoteTeleportDistance = 5f,
            WeaponSimulationConfig? weaponConfig = null,
            uint maximumRewindTicks = 10,
            float interestRadius = 150f,
            int snapshotBudgetPerClientPerTick = 32,
            uint fullSnapshotIntervalTicks = 10,
            int deathmatchKillLimit = 10,
            float deathmatchDurationSeconds = 300f,
            float deathmatchRespawnSeconds = 3f,
            float deathmatchReplicationIntervalSeconds = 0.25f,
            SimulationWorldContext simulationWorld = null,
            // 本地模拟 Tick；50Hz 默认等待 5 秒，每 0.5 秒重试，等待连接期间也必须推进时钟。
            uint admissionTimeoutTicks = 250,
            uint admissionRetryTicks = 25,
            Func<ulong> monotonicMicroseconds = null,
            bool isLocalPractice = false,
            // 仅远端表现复制频率，单位Hz；不改变权威模拟步长/结果，最高每Tick一包。
            int remoteSnapshotRate = 20)
        {
            if (isLocalPractice && (!(serverTransport is LocalGameTransport) || !(clientTransport is LocalGameTransport)))
            {
                throw new ArgumentException("单人练习只能使用本地传输对，不能改变联网对局规则。", nameof(isLocalPractice));
            }
            IsLocalPractice = isLocalPractice;
            if (serverTransport == null && clientTransport == null)
            {
                throw new ArgumentException("至少需要一个服务器或客户端传输实例。");
            }

            if (tickDeltaTime <= 0f || float.IsNaN(tickDeltaTime) || float.IsInfinity(tickDeltaTime))
            {
                throw new ArgumentOutOfRangeException(nameof(tickDeltaTime));
            }

            if (predictionHistoryCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(predictionHistoryCapacity));
            }

            if (inputRedundancyCount <= 0 || inputRedundancyCount > InputCommandBatchCodec.MaxCommandCount)
            {
                throw new ArgumentOutOfRangeException(nameof(inputRedundancyCount));
            }

            if (serverInputWindowCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(serverInputWindowCapacity));
            }

            if (maxMissingInputWaitTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxMissingInputWaitTicks));
            }

            if (maxRepeatedMovementTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxRepeatedMovementTicks));
            }

            if (positionErrorThreshold < AuthoritativePlayerStateCodec.MaximumPositionQuantizationError)
            {
                throw new ArgumentOutOfRangeException(nameof(positionErrorThreshold));
            }

            if (velocityErrorThreshold < AuthoritativePlayerStateCodec.MaximumVelocityQuantizationError)
            {
                throw new ArgumentOutOfRangeException(nameof(velocityErrorThreshold));
            }

            if (maximumRewindTicks > 2048)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumRewindTicks), "最大回溯窗口不能超过 2048 Tick。");
            }

            if (interestRadius <= 0f || float.IsNaN(interestRadius) || float.IsInfinity(interestRadius))
            {
                throw new ArgumentOutOfRangeException(nameof(interestRadius));
            }
            if (serverSpawnSpacing < 0f || float.IsNaN(serverSpawnSpacing) || float.IsInfinity(serverSpawnSpacing))
            {
                throw new ArgumentOutOfRangeException(nameof(serverSpawnSpacing));
            }
            if (snapshotBudgetPerClientPerTick <= 0) throw new ArgumentOutOfRangeException(nameof(snapshotBudgetPerClientPerTick));
            if (fullSnapshotIntervalTicks == 0) throw new ArgumentOutOfRangeException(nameof(fullSnapshotIntervalTicks));
            if (deathmatchKillLimit <= 0) throw new ArgumentOutOfRangeException(nameof(deathmatchKillLimit));
            if (deathmatchDurationSeconds <= 0f) throw new ArgumentOutOfRangeException(nameof(deathmatchDurationSeconds));
            if (deathmatchRespawnSeconds < 0f) throw new ArgumentOutOfRangeException(nameof(deathmatchRespawnSeconds));
            if (deathmatchReplicationIntervalSeconds <= 0f) throw new ArgumentOutOfRangeException(nameof(deathmatchReplicationIntervalSeconds));

            this.serverTransport = serverTransport;
            this.clientTransport = clientTransport;
            this.serverInitialState = serverInitialState;
            this.clientInitialState = clientInitialState;
            this.simulationConfig = simulationConfig;
            this.tickDeltaTime = tickDeltaTime;
            timeSyncClock = monotonicMicroseconds ?? ReadMonotonicMicroseconds;
            tickDeltaTimeFixed = SimulationStep.FromSeconds(tickDeltaTime);
            this.predictionHistoryCapacity = predictionHistoryCapacity;
            this.inputRedundancyCount = inputRedundancyCount;
            this.serverInputWindowCapacity = serverInputWindowCapacity;
            this.maxMissingInputWaitTicks = maxMissingInputWaitTicks;
            this.maxRepeatedMovementTicks = maxRepeatedMovementTicks;
            this.positionErrorThreshold = positionErrorThreshold;
            this.velocityErrorThreshold = velocityErrorThreshold;
            sessionSpatial = new SimulationSessionSpatialConfig(
                serverInitialState.PositionFixed, Fix.FromFloat(serverSpawnSpacing), Fix.FromFloat(interestRadius));
            this.weaponConfig = weaponConfig ?? WeaponSimulationConfig.DefaultRifle;
            if (!this.weaponConfig.IsValid)
            {
                throw new ArgumentException("武器配置无效。", nameof(weaponConfig));
            }
            // 指纹与实际后端必须绑定，不能声明一个世界却继续使用另一个 PhysX 世界。
            WorldContext = simulationWorld;
            if (simulationWorld != null)
            {
                if (collisionWorld != null)
                {
                    throw new ArgumentException("定点世界不能与旧碰撞后端同时指定。", nameof(collisionWorld));
                }

                var actual = new SimulationWorldContext(simulationWorld.World, simulationConfig,
                    SimulationStep.FromSeconds(tickDeltaTime), simulationWorld.CollisionLayerMask,
                    sessionSpatial, this.weaponConfig);
                if (!simulationWorld.Identity.Equals(actual.Identity))
                {
                    throw new ArgumentException("移动、空间或武器配置与世界上下文不一致。", nameof(simulationWorld));
                }
            }

            this.collisionWorld = simulationWorld == null ? collisionWorld ?? FlatGroundCollisionWorld.Instance : null;
            this.maximumRewindTicks = maximumRewindTicks;
            this.snapshotBudgetPerClientPerTick = snapshotBudgetPerClientPerTick;
            this.fullSnapshotIntervalTicks = fullSnapshotIntervalTicks;
            if (remoteSnapshotRate <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(remoteSnapshotRate));
            }
            this.remoteSnapshotRate = remoteSnapshotRate;
            serverTickRate = System.Math.Max(1, (int)System.Math.Round(1d / tickDeltaTime));
            if (remoteTeleportDistance <= 0f || float.IsNaN(remoteTeleportDistance)
                || float.IsInfinity(remoteTeleportDistance) || remoteTeleportDistance > Fix.MaxValue.ToDouble())
            {
                throw new ArgumentOutOfRangeException(nameof(remoteTeleportDistance));
            }
            remoteTeleportDistanceFixed = Fix.FromFloat(remoteTeleportDistance);
            if (remoteTeleportDistanceFixed == Fix.Zero)
            {
                remoteTeleportDistanceFixed = Fix.Epsilon;
            }
            this.deathmatchKillLimit = deathmatchKillLimit;
            deathmatchDurationTicks = (uint)Mathf.Max(1, Mathf.CeilToInt(deathmatchDurationSeconds / tickDeltaTime));
            deathmatchRespawnTicks = (uint)Mathf.Max(0, Mathf.CeilToInt(deathmatchRespawnSeconds / tickDeltaTime));
            deathmatchReplicationIntervalTicks = (uint)Mathf.Max(1, Mathf.CeilToInt(deathmatchReplicationIntervalSeconds / tickDeltaTime));
            if (clientTransport != null)
            {
                RemotePlayers = new RemotePlayerReplicaSet(remoteSnapshotCapacity, tickDeltaTime, remoteInterpolationDelayTicks, remoteMaxExtrapolationTicks, remoteTeleportDistance);
            }

            if (simulationWorld != null)
            {
                admission = new SimulationSessionAdmission(simulationWorld.Identity, serverTransport, clientTransport,
                    CreateConnectedSession, RemoveConnectedSession,
                    () => RemotePlayers != null && RemotePlayers.LocalEntityId != 0,
                    ResendSessionAssignment, admissionTimeoutTicks, admissionRetryTicks,
                    RestoreServerSession, SendResumeGrant, ReceiveResumeGrant, () => ClientResumeCredential);
            }
        }

        /// <summary>
        /// 准入后创建对应会话。客户端输入 Tick 从零开始，不依赖本地世界 Tick 的当前值。
        /// </summary>
        private bool CreateConnectedSession(NetworkTransportSide side, TransportConnectionId connectionId)
        {
            if (!connectionId.IsValid)
            {
                return false;
            }

            if (side == NetworkTransportSide.Server)
            {
                ExpireRetainedSessions();
                if (serverTransport == null || serverSessions.ContainsKey(connectionId)
                    || allServerSessions.Count >= DeathmatchStateCodec.MaximumPlayerCount)
                {
                    return false;
                }

                uint entityId = nextEntityId;
                // 越域出生点在分配实体和建立会话前拒绝，不能留下半个玩家或消耗实体序号。
                if (!sessionSpatial.TryGetSpawnPosition(entityId, out FixVec3 spawnPosition))
                {
                    return false;
                }
                if (admission != null && !serverCredentials.TryIssue(entityId, out _))
                {
                    return false;
                }
                AllocateEntityId();
                PlayerState spawnedState = serverInitialState;
                spawnedState.PositionFixed = spawnPosition;
                serverSessions.Add(connectionId, new ServerAuthoritativeMovementSession(
                    serverTransport, connectionId, spawnedState, simulationConfig, tickDeltaTime,
                    serverInputWindowCapacity, maxMissingInputWaitTicks, maxRepeatedMovementTicks,
                    collisionWorld, entityId, weaponConfig, WorldContext?.CollisionWorld));
                serverEntityIds.Add(connectionId, entityId);
                allServerSessions.Add(entityId, serverSessions[connectionId]);
                serverSessions[connectionId].StateDrift.Compared += OnStateHashCompared;
                timeSyncResponders[connectionId] = new AFPS.NetCode.TimeSync.ServerTimeSyncResponder(serverTransport, connectionId);
                serverHealth.Add(entityId, Fix.FromInt(100));
                serverKills.Add(entityId, 0);
                serverDeaths.Add(entityId, 0);
                rewindHistories.Add(entityId, new RewindHitboxHistory((int)maximumRewindTicks + 2));
                serverSnapshotStates.Add(connectionId, new Dictionary<uint, RemoteSnapshotSendState>());
                serverVisibleEntities.Add(connectionId, new HashSet<uint>());
                SendAssignment(connectionId, entityId);
                deathmatchStateDirty = true;
                return true;
            }

            if (side != NetworkTransportSide.Client || clientTransport == null)
            {
                return false;
            }

            ClientWeapon.Clear();
            ResetClientShotResults();
            hasWeaponStateSequence = false;
            clientConnectionId = connectionId;
            ResetClientStateHash();
            clientRecoveryConfirmed = false;
            clientRecoveryEpoch = 0;
            ResetClientTimeSync(connectionId);
            ClientSession = new ClientPredictedMovementSession(
                clientTransport, connectionId, clientInitialState, simulationConfig, tickDeltaTime,
                predictionHistoryCapacity, inputRedundancyCount, positionErrorThreshold, velocityErrorThreshold,
                collisionWorld, WorldContext?.CollisionWorld);
            RemotePlayers?.Clear();
            clientSnapshotBaselines.Clear();
            ClientDeathmatchState = null;
            DeathmatchStateVersion = 0;
            hasReceivedDeathmatchSequence = false;
            return true;
        }

        /// <summary>
        /// 定点服务器断线保留权威会话；客户端预测与旧连接复制基线仍立即清理。
        /// </summary>
        private bool RemoveConnectedSession(NetworkTransportSide side, TransportConnectionId connectionId)
        {
            if (side == NetworkTransportSide.Server)
            {
                ExpireRetainedSessions();
                if (!serverSessions.TryGetValue(connectionId, out var session)
                    || !serverEntityIds.TryGetValue(connectionId, out uint entityId))
                {
                    return false;
                }

                bool retained = admission != null && retainedSessions.TryRetain(session, latestServerWorldTick);
                serverSessions.Remove(connectionId);
                serverEntityIds.Remove(connectionId);
                timeSyncResponders.Remove(connectionId);
                serverSnapshotStates.Remove(connectionId);
                serverVisibleEntities.Remove(connectionId);
                recoveringConnections.Remove(connectionId);
                recoveryTransfers.Remove(connectionId);
                recoveringEntityIds.Remove(entityId);
                if (!retained)
                {
                    RemoveServerEntity(entityId);
                }
                deathmatchStateDirty = true;
                return true;
            }

            if (side != NetworkTransportSide.Client || ClientSession == null || connectionId != clientConnectionId)
            {
                return false;
            }

            CaptureClientRecoveryInputs();
            ClientWeapon.Clear();
            ResetClientShotResults();
            hasWeaponStateSequence = false;
            clientConnectionId = default;
            ResetClientTimeSync(default);
            ClientSession = null;
            RemotePlayers?.Clear();
            clientSnapshotBaselines.Clear();
            ClientDeathmatchState = null;
            DeathmatchStateVersion = 0;
            hasReceivedDeathmatchSequence = false;
            return true;
        }

        /// <summary>
        /// 按包头消息类型把输入包交给服务器会话，或把权威状态交给客户端会话。
        /// </summary>
        private bool HandleSessionData(NetworkTransportSide side, TransportConnectionId connectionId, ArraySegment<byte> packet, out ReconciliationResult reconciliationResult)
        {
            reconciliationResult = default;
            if (!PacketHeaderCodec.TryRead(packet, out PacketHeader header))
            {
                return false;
            }

            if (side == NetworkTransportSide.Server)
            {
                if (header.MessageType == NetworkMessageType.ClientStateHashReport)
                {
                    return ReceiveClientStateHash(connectionId, packet);
                }
                if (header.MessageType == NetworkMessageType.SessionRecoveryAck)
                {
                    return ReceiveRecoveryAck(connectionId, packet);
                }
                if (header.MessageType == NetworkMessageType.InputCommandBatch
                    && recoveringConnections.ContainsKey(connectionId))
                {
                    // 恢复种子入队不等于客户端已应用；11C接ACK前，不接受任何旧/新输入。
                    return false;
                }
                if (header.MessageType == NetworkMessageType.TimeSyncRequest
                    && timeSyncResponders.TryGetValue(connectionId, out var responder))
                {
                    ulong received = timeSyncClock();
                    return responder.TryRespond(packet, received, timeSyncClock(),
                        timeSyncWorldTick, timeSyncTickAlpha, out _);
                }
                return header.MessageType == NetworkMessageType.InputCommandBatch && serverSessions.TryGetValue(connectionId, out ServerAuthoritativeMovementSession serverSession) && serverSession.TryReceiveInputPacket(packet, out _);
            }

            if (side != NetworkTransportSide.Client || connectionId != clientConnectionId)
            {
                return false;
            }

            switch (header.MessageType)
            {
                case NetworkMessageType.SessionRecoverySeed:
                    return ReceiveRecoverySeed(packet);
                case NetworkMessageType.SessionRecoveryConfirmed:
                    return ReceiveRecoveryConfirmed(packet);
                case NetworkMessageType.ShotRejection:
                    return ReceiveShotRejection(packet);
                case NetworkMessageType.AuthoritativeWeaponState:
                    if (ClientRecoveryPending)
                    {
                        return false;
                    }
                    if (!AuthoritativeWeaponStateCodec.TryDeserialize(packet, out var weaponHeader, out var weaponState)
                        || RemotePlayers == null || weaponState.EntityId != RemotePlayers.LocalEntityId
                        || !IsCompatibleWeaponState(weaponState)
                        || (hasWeaponStateSequence && !SequenceMath.IsNewer(weaponHeader.Sequence, lastWeaponStateSequence))
                        || !ClientWeapon.Apply(weaponState))
                    {
                        return false;
                    }
                    lastWeaponStateSequence = weaponHeader.Sequence;
                    hasWeaponStateSequence = true;
                    return true;
                case NetworkMessageType.TimeSyncResponse:
                    return ReceiveTimeSync(packet);
                case NetworkMessageType.AuthoritativePlayerState:
                    if (ClientRecoveryPending)
                    {
                        return false;
                    }
                    bool handled = ClientSession != null && ClientSession.TryReceiveAuthoritativePacket(packet, out _, out reconciliationResult);
                    if (handled && reconciliationResult.Status == ReconciliationStatus.Corrected) Metrics.CorrectionsApplied++;
                    return handled;
                case NetworkMessageType.PlayerSessionAssignment:
                    return RemotePlayers != null && RemotePlayers.TryApplyAssignment(packet);
                case NetworkMessageType.RemotePlayerSnapshot:
                    if (RemotePlayers == null || !RemotePlayers.TryInsertSnapshot(packet, out RemotePlayerSnapshot fullSnapshot)) return false;
                    clientSnapshotBaselines[fullSnapshot.EntityId] = fullSnapshot;
                    return true;
                case NetworkMessageType.RemotePlayerSnapshotDelta:
                    if (RemotePlayers == null || !TryReadDeltaEntityId(packet, out uint deltaEntityId) || !clientSnapshotBaselines.TryGetValue(deltaEntityId, out RemotePlayerSnapshot baseline) || !RemotePlayerSnapshotDeltaCodec.TryDeserialize(packet, baseline, out PacketHeader deltaHeader, out RemotePlayerSnapshot deltaSnapshot)) return false;
                    return RemotePlayers.TryInsertSnapshot(deltaSnapshot, deltaHeader.Sequence, out _);
                case NetworkMessageType.PlayerDespawn:
                    if (RemotePlayers == null || !RemotePlayers.TryApplyDespawn(packet, out uint despawnedEntityId)) return false;
                    clientSnapshotBaselines.Remove(despawnedEntityId);
                    return true;
                case NetworkMessageType.AuthoritativeShotResult:
                    if (!AuthoritativeShotResultCodec.TryDeserialize(packet, out PacketHeader shotHeader, out AuthoritativeShotResult shotResult) || (hasReceivedShotResultSequence && !SequenceMath.IsNewer(shotHeader.Sequence, lastReceivedShotResultSequence)))
                    {
                        return false;
                    }

                    hasReceivedShotResultSequence = true;
                    lastReceivedShotResultSequence = shotHeader.Sequence;
                    LastReceivedShotResult = shotResult;
                    ShotResultVersion = unchecked(ShotResultVersion + 1);
                    EnqueueShotPresentation(shotResult);
                    return true;
                case NetworkMessageType.DeathmatchState:
                    if (!DeathmatchStateCodec.TryDeserialize(packet, out PacketHeader matchHeader, out DeathmatchState matchState) || (hasReceivedDeathmatchSequence && !SequenceMath.IsNewer(matchHeader.Sequence, lastReceivedDeathmatchSequence)))
                    {
                        return false;
                    }

                    hasReceivedDeathmatchSequence = true;
                    lastReceivedDeathmatchSequence = matchHeader.Sequence;
                    ClientDeathmatchState = matchState;
                    DeathmatchStateVersion = unchecked(DeathmatchStateVersion + 1);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 执行并发送本地玩家的下一条客户端输入。
        /// </summary>
        public bool TryPredictAndSend(in PlayerInputCommand command, out PlayerState state, out InputBatchSendResult sendResult)
        {
            if (!CanPredictClientInput)
            {
                state = default;
                sendResult = default;
                return false;
            }

            state = ClientSession.PredictAndSend(command, out sendResult);
            ObserveClientStateHash(state, ClientSession.LastPredictedInput);
            ClientWeapon.Observe(command);
            return true;
        }

        /// <summary>
        /// 在同一个服务器世界 Tick 中推进所有已连接玩家的权威会话。
        /// </summary>
        public int AdvanceServerSessions(uint serverWorldTick)
        {
            long tickStarted = Stopwatch.GetTimestamp();
            // 普通 Client 也调用此入口推进准入时钟；等待握手时不存在预测会话。
            admission?.Advance(serverWorldTick);
            latestServerWorldTick = serverWorldTick;
            AdvanceClientRecovery();
            ExpireRetainedSessions();
            AdvanceRecoveringConnections();
            int advancedCount = 0;
            UpdateDeathmatchBeforeSimulation(serverWorldTick);
            pendingWeaponFireResults.Clear();
            foreach (KeyValuePair<TransportConnectionId, ServerAuthoritativeMovementSession> pair in serverSessions)
            {
                uint entityId = serverEntityIds[pair.Key];
                if (recoveringConnections.ContainsKey(pair.Key))
                {
                    continue;
                }
                pair.Value.GameplayEnabled = deathmatchPhase != DeathmatchPhase.Finished && IsServerPlayerAlive(entityId);
                pair.Value.WeaponEnabled = deathmatchPhase == DeathmatchPhase.Playing && IsServerPlayerAlive(entityId);
                if (pair.Value.TryAdvance(serverWorldTick, out _, out _))
                {
                    advancedCount++;
                    AuthoritativeSimulationStepCompleted?.Invoke(new AFPS.NetCode.Replay.AuthoritativeSimulationStep(
                        entityId, serverWorldTick, pair.Value.PreviousSimulationState,
                        pair.Value.LastAppliedInput, pair.Value.CurrentState));
                    if (pair.Value.LastWeaponFireResult.Accepted)
                    {
                        pendingWeaponFireResults.Add(pair.Value.LastWeaponFireResult);
                    }
                }
            }

            ReplicateRemoteSnapshots(serverWorldTick);
            RecordCurrentHitboxes(serverWorldTick);
            ResolveCombatTick(serverWorldTick);
            ReplicateDeathmatchState(serverWorldTick);

            Metrics.LastServerTickMicroseconds = ElapsedMicroseconds(tickStarted);
            if (Metrics.LastServerTickMicroseconds > Metrics.MaximumServerTickMicroseconds) Metrics.MaximumServerTickMicroseconds = Metrics.LastServerTickMicroseconds;

            return advancedCount;
        }

        public bool TryGetServerSession(TransportConnectionId connectionId, out ServerAuthoritativeMovementSession session) => serverSessions.TryGetValue(connectionId, out session);

        public bool TryGetServerEntityId(TransportConnectionId connectionId, out uint entityId) => serverEntityIds.TryGetValue(connectionId, out entityId);

        public bool TryGetServerHealthFixed(uint entityId, out Fix health) => serverHealth.TryGetValue(entityId, out health);

        public bool TryGetServerHealth(uint entityId, out float health)
        {
            bool found = serverHealth.TryGetValue(entityId, out Fix fixedHealth);
            health = fixedHealth.ToFloat();
            return found;
        }

        public bool TryGetServerDeathmatchPlayer(uint entityId, out DeathmatchPlayerState state)
        {
            if (!serverHealth.ContainsKey(entityId))
            {
                state = default;
                return false;
            }

            state = CreateDeathmatchPlayerState(entityId, latestServerWorldTick);
            return true;
        }

        public DeathmatchPhase ServerDeathmatchPhase => deathmatchPhase;

        private void ResolveCombatTick(uint serverWorldTick)
        {
            if (deathmatchPhase != DeathmatchPhase.Playing)
            {
                return;
            }
            if (pendingWeaponFireResults.Count == 0 && projectileSimulation.Count == 0)
            {
                return;
            }

            BuildCombatTargets(serverWorldTick, serverWorldTick);

            pendingWeaponFireResults.Sort((left, right) => left.Shot.EntityId.CompareTo(right.Shot.EntityId));
            for (int i = 0; i < pendingWeaponFireResults.Count; i++)
            {
                if (deathmatchPhase != DeathmatchPhase.Playing)
                {
                    break;
                }
                WeaponFireResult fireResult = pendingWeaponFireResults[i];
                AuthoritativeShot shot = fireResult.Shot;
                if (shot.FireMode == WeaponFireMode.Projectile)
                {
                    projectileSimulation.Spawn(shot, fireResult.RemainingAmmo);
                    BroadcastShotResult(new AuthoritativeShotResult(
                        shot, fireResult.RemainingAmmo, false, 0, Fix.Zero,
                        ServerHitscanResolver.PointAtDistance(
                            shot.OriginFixed, shot.DirectionFixed, shot.RangeFixed.Raw)));
                    continue;
                }

                ServerCombatTarget target = default;
                FixVec3 hitPoint = ServerHitscanResolver.PointAtDistance(
                    shot.OriginFixed, shot.DirectionFixed, shot.RangeFixed.Raw);
                BuildCombatTargets(shot.RewindTick, serverWorldTick);
                long rewindStarted = Stopwatch.GetTimestamp();
                bool didHit = ServerWorldShotResolver.TryResolve(shot, combatTargets, WorldContext?.World,
                    WorldContext?.CollisionLayerMask ?? ~0, out target, out hitPoint, out bool hitEnvironment);
                Metrics.RewindQueries++;
                Metrics.LastRewindMicroseconds = ElapsedMicroseconds(rewindStarted);
                uint targetEntityId = 0;
                Fix targetHealth = Fix.Zero;
                if (didHit)
                {
                    targetEntityId = target.EntityId;
                    targetHealth = ApplyDamage(shot.EntityId, targetEntityId, shot.DamageFixed, serverWorldTick);
                    for (int targetIndex = 0; targetIndex < combatTargets.Count; targetIndex++)
                    {
                        if (combatTargets[targetIndex].EntityId == targetEntityId)
                        {
                            combatTargets[targetIndex] = new ServerCombatTarget(
                                targetEntityId, target.PositionFixed,
                                target.RadiusFixed, target.HeightFixed, targetHealth);
                            break;
                        }
                    }
                }
                BroadcastShotResult(new AuthoritativeShotResult(
                    shot, fireResult.RemainingAmmo, didHit, targetEntityId, targetHealth, hitPoint, hitEnvironment));
            }

            BuildCombatTargets(serverWorldTick, serverWorldTick);
            projectileImpacts.Clear();
            if (deathmatchPhase != DeathmatchPhase.Playing)
            {
                return;
            }
            projectileSimulation.Advance(tickDeltaTimeFixed, combatTargets, null,
                WorldContext?.World, WorldContext?.CollisionLayerMask ?? ~0, ResolveProjectileImpact);
        }

        private void RecordCurrentHitboxes(uint serverWorldTick)
        {
            foreach (KeyValuePair<uint, ServerAuthoritativeMovementSession> pair in allServerSessions)
            {
                uint entityId = pair.Key;
                if (!IsServerPlayerAlive(entityId))
                {
                    continue;
                }
                // 每Tick记录真实姿态尺寸；回溯射击使用历史尺寸，不能用当前站高覆盖历史下蹲。
                var collision = PlayerStanceSimulation.Collision(pair.Value.CurrentState, simulationConfig);
                ServerCombatTarget target = new ServerCombatTarget(
                    entityId, pair.Value.CurrentState.PositionFixed,
                    collision.RadiusFixed, collision.HeightFixed, serverHealth[entityId]);
                rewindHistories[entityId].Record(serverWorldTick, target);
            }
        }

        private void BuildCombatTargets(uint requestedTick, uint currentTick)
        {
            combatTargets.Clear();
            foreach (KeyValuePair<uint, RewindHitboxHistory> pair in rewindHistories)
            {
                if (!IsServerPlayerAlive(pair.Key))
                {
                    continue;
                }
                if (pair.Value.TryGetAtOrBefore(requestedTick, currentTick, maximumRewindTicks, out ServerCombatTarget historical, out _))
                {
                    // 回溯几何，但用当前生命值过滤死亡目标，不能把历史血量写回当前会话。
                    combatTargets.Add(new ServerCombatTarget(
                        pair.Key, historical.PositionFixed,
                        historical.RadiusFixed, historical.HeightFixed, serverHealth[pair.Key]));
                }
            }
        }

        private void ResetEmptyDeathmatch()
        {
            // 一人离开不打断当前比赛；最后一人离开才恢复等待，供常驻服务器开启下一局。
            deathmatchPhase = DeathmatchPhase.WaitingForPlayers;
            deathmatchStartTick = 0;
            deathmatchWinnerEntityId = 0;
            serverRespawnTicks.Clear();
            projectileSimulation.Clear();
            pendingWeaponFireResults.Clear();
            projectileImpacts.Clear();
            combatTargets.Clear();
            deathmatchEntityIds.Clear();
            // 保留实体 ID 分配器和包序号的单调性；迟到的旧包不能与新玩家身份碰撞。
        }

        private void UpdateDeathmatchBeforeSimulation(uint serverWorldTick)
        {
            if (deathmatchPhase == DeathmatchPhase.WaitingForPlayers && serverSessions.Count >= (IsLocalPractice ? 1 : 2))
            {
                deathmatchPhase = DeathmatchPhase.Playing;
                deathmatchStartTick = serverWorldTick;
                deathmatchStateDirty = true;
            }

            if (deathmatchPhase != DeathmatchPhase.Playing)
            {
                return;
            }

            // 到时优先于复活和当 Tick 战斗，避免终局边界先恢复生命再冻结。
            if (!IsLocalPractice && unchecked(serverWorldTick - deathmatchStartTick) >= deathmatchDurationTicks)
            {
                FinishDeathmatch(FindLeadingPlayer());
                return;
            }

            deathmatchEntityIds.Clear();
            foreach (uint entityId in serverRespawnTicks.Keys)
            {
                deathmatchEntityIds.Add(entityId);
            }

            for (int i = 0; i < deathmatchEntityIds.Count; i++)
            {
                uint entityId = deathmatchEntityIds[i];
                uint respawnTick = serverRespawnTicks[entityId];
                if (!HasReached(serverWorldTick, respawnTick))
                {
                    continue;
                }

                serverRespawnTicks.Remove(entityId);
                serverHealth[entityId] = Fix.FromInt(100);
                if (allServerSessions.TryGetValue(entityId, out ServerAuthoritativeMovementSession session))
                {
                    // 离线死亡也按世界期限重生，不以恢复连接刷新生命或重置计时起点。
                    session.Respawn(sessionSpatial.GetSpawnPosition(entityId));
                }

                deathmatchStateDirty = true;
            }

        }

        private Fix ApplyDamage(uint attackerEntityId, uint targetEntityId, Fix damage, uint serverWorldTick)
        {
            if (deathmatchPhase != DeathmatchPhase.Playing || !IsServerPlayerAlive(targetEntityId))
            {
                return serverHealth.TryGetValue(targetEntityId, out Fix existingHealth) ? existingHealth : Fix.Zero;
            }

            Fix targetHealth = ServerCombatHealth.ApplyDamage(serverHealth[targetEntityId], damage);
            serverHealth[targetEntityId] = targetHealth;
            deathmatchStateDirty = true;
            if (targetHealth.Raw > 0)
            {
                return targetHealth;
            }

            serverDeaths[targetEntityId] = (ushort)Mathf.Min(ushort.MaxValue, serverDeaths[targetEntityId] + 1);
            // 同实体 ID 会用于重生；死亡即清空历史，禁止延迟射击把上一条生命的位置命中新生命。
            rewindHistories[targetEntityId].Clear();
            serverRespawnTicks[targetEntityId] = unchecked(serverWorldTick + deathmatchRespawnTicks);
            if (attackerEntityId != targetEntityId && serverKills.ContainsKey(attackerEntityId))
            {
                serverKills[attackerEntityId] = (ushort)Mathf.Min(ushort.MaxValue, serverKills[attackerEntityId] + 1);
                if (serverKills[attackerEntityId] >= deathmatchKillLimit)
                {
                    FinishDeathmatch(attackerEntityId);
                }
            }

            return targetHealth;
        }

        private bool IsServerPlayerAlive(uint entityId)
        {
            return serverHealth.TryGetValue(entityId, out Fix health) && health.Raw > 0 && !serverRespawnTicks.ContainsKey(entityId);
        }

        private void FinishDeathmatch(uint winnerEntityId)
        {
            deathmatchPhase = DeathmatchPhase.Finished;
            deathmatchWinnerEntityId = winnerEntityId;
            deathmatchStateDirty = true;
        }

        private uint FindLeadingPlayer()
        {
            uint winner = 0;
            ushort bestKills = 0;
            bool tied = false;
            foreach (KeyValuePair<uint, ushort> pair in serverKills)
            {
                if (winner == 0 || pair.Value > bestKills)
                {
                    winner = pair.Key;
                    bestKills = pair.Value;
                    tied = false;
                }
                else if (pair.Value == bestKills)
                {
                    tied = true;
                }
            }

            return tied ? 0u : winner;
        }

        private void ReplicateDeathmatchState(uint serverWorldTick)
        {
            if (serverTransport == null || serverSessions.Count == 0 || (!deathmatchStateDirty && unchecked(serverWorldTick - lastDeathmatchReplicationTick) < deathmatchReplicationIntervalTicks))
            {
                return;
            }

            deathmatchEntityIds.Clear();
            foreach (uint entityId in serverHealth.Keys)
            {
                deathmatchEntityIds.Add(entityId);
            }
            deathmatchEntityIds.Sort();

            DeathmatchPlayerState[] players = new DeathmatchPlayerState[deathmatchEntityIds.Count];
            for (int i = 0; i < players.Length; i++)
            {
                players[i] = CreateDeathmatchPlayerState(deathmatchEntityIds[i], serverWorldTick);
            }

            uint remainingTicks = 0;
            if (deathmatchPhase == DeathmatchPhase.Playing && !IsLocalPractice)
            {
                uint elapsed = unchecked(serverWorldTick - deathmatchStartTick);
                remainingTicks = elapsed >= deathmatchDurationTicks ? 0 : deathmatchDurationTicks - elapsed;
            }

            DeathmatchState state = new DeathmatchState(deathmatchPhase, serverWorldTick, remainingTicks, deathmatchWinnerEntityId, players);
            uint sequence = deathmatchPacketSequence++;
            if (deathmatchPacketSequence == 0)
            {
                deathmatchPacketSequence = 1;
            }
            if (!DeathmatchStateCodec.TrySerialize(state, sequence, new ArraySegment<byte>(deathmatchStateBuffer), out int packetBytes))
            {
                return;
            }

            foreach (TransportConnectionId connectionId in serverSessions.Keys)
            {
                serverTransport.Send(connectionId, TransportDelivery.ReliableSequenced, new ArraySegment<byte>(deathmatchStateBuffer, 0, packetBytes));
            }

            deathmatchStateDirty = false;
            lastDeathmatchReplicationTick = serverWorldTick;
        }

        private DeathmatchPlayerState CreateDeathmatchPlayerState(uint entityId, uint serverWorldTick)
        {
            uint respawnTicks = 0;
            if (serverRespawnTicks.TryGetValue(entityId, out uint respawnTick) && !HasReached(serverWorldTick, respawnTick))
            {
                respawnTicks = unchecked(respawnTick - serverWorldTick);
            }

            return new DeathmatchPlayerState(
                entityId,
                serverHealth[entityId],
                serverKills[entityId],
                serverDeaths[entityId],
                (ushort)Mathf.Min(ushort.MaxValue, respawnTicks),
                IsServerPlayerAlive(entityId));
        }

        private static bool HasReached(uint current, uint target)
        {
            return current == target || SequenceMath.IsNewer(current, target);
        }

        private void BroadcastShotResult(in AuthoritativeShotResult result)
        {
            uint sequence = shotResultPacketSequence++;
            if (shotResultPacketSequence == 0)
            {
                shotResultPacketSequence = 1;
            }

            if (!AuthoritativeShotResultCodec.TrySerialize(result, sequence, new ArraySegment<byte>(shotResultBuffer), out int packetBytes))
            {
                return;
            }

            foreach (TransportConnectionId connectionId in serverSessions.Keys)
            {
                serverTransport.Send(connectionId, TransportDelivery.ReliableSequenced, new ArraySegment<byte>(shotResultBuffer, 0, packetBytes));
                Metrics.ShotResultsSent++;
            }
        }

        private void ReplicateRemoteSnapshots(uint serverWorldTick)
        {
            foreach (KeyValuePair<TransportConnectionId, ServerAuthoritativeMovementSession> targetPair in serverSessions)
            {
                snapshotCandidates.Clear();
                interestedEntityIds.Clear();
                FixVec3 observerPosition = targetPair.Value.CurrentState.PositionFixed;
                foreach (KeyValuePair<uint, ServerAuthoritativeMovementSession> ownerPair in allServerSessions)
                {
                    if (ownerPair.Key == serverEntityIds[targetPair.Key])
                    {
                        continue;
                    }
                    if (sessionSpatial.TryMeasureInterestDistance(
                        observerPosition, ownerPair.Value.CurrentState.PositionFixed, out ulong distanceSquared))
                    {
                        uint entityId = ownerPair.Key;
                        interestedEntityIds.Add(entityId);
                        PlayerState visualState = ownerPair.Value.CurrentState;
                        if (ownerPair.Value.IsRetainedOffline || IsRecoveringEntity(entityId))
                        {
                            // 冻结实体仍可见/受击；快照速度归零，避免远端按旧速度外推，权威Raw不变。
                            visualState.VelocityFixed = FixVec3.Zero;
                        }
                        var states = serverSnapshotStates[targetPair.Key];
                        bool hasState = states.TryGetValue(entityId, out var sendState);
                        // 兴趣差集仍每Tick计算；未到发送时间的实体不能占用其他实体的发送预算。
                        if (!hasState || sendState.IsDue(serverWorldTick, visualState.PositionFixed))
                        {
                            bool urgent = !hasState || sendState.IsUrgent(visualState.PositionFixed);
                            snapshotCandidates.Add(new SnapshotCandidate(entityId, visualState, distanceSquared, urgent));
                        }
                    }
                }

                HashSet<uint> visibleEntities = serverVisibleEntities[targetPair.Key];
                noLongerVisibleEntityIds.Clear();
                foreach (uint entityId in visibleEntities)
                {
                    if (!interestedEntityIds.Contains(entityId))
                    {
                        noLongerVisibleEntityIds.Add(entityId);
                    }
                }

                for (int i = 0; i < noLongerVisibleEntityIds.Count; i++)
                {
                    uint entityId = noLongerVisibleEntityIds[i];
                    if (SendDespawn(targetPair.Key, entityId))
                    {
                        visibleEntities.Remove(entityId);
                        serverSnapshotStates[targetPair.Key].Remove(entityId);
                    }
                }

                // 预算截断前给等距离实体唯一顺序，避免字典插入顺序决定谁能收到快照。
                snapshotCandidates.Sort((left, right) =>
                {
                    int urgency = right.IsUrgent.CompareTo(left.IsUrgent);
                    if (urgency != 0)
                    {
                        return urgency;
                    }
                    int comparison = left.DistanceSquared.CompareTo(right.DistanceSquared);
                    return comparison != 0 ? comparison : left.EntityId.CompareTo(right.EntityId);
                });
                int count = System.Math.Min(snapshotBudgetPerClientPerTick, snapshotCandidates.Count);
                for (int i = 0; i < count; i++)
                {
                    if (SendRemoteSnapshot(targetPair.Key, snapshotCandidates[i], serverWorldTick))
                    {
                        visibleEntities.Add(snapshotCandidates[i].EntityId);
                    }
                }
            }
        }

        private bool SendRemoteSnapshot(
            TransportConnectionId targetConnectionId,
            in SnapshotCandidate candidate,
            uint serverWorldTick)
        {
            var states = serverSnapshotStates[targetConnectionId];
            if (!states.TryGetValue(candidate.EntityId, out var sendState))
            {
                sendState = new RemoteSnapshotSendState(serverTickRate, remoteSnapshotRate, remoteTeleportDistanceFixed);
                states.Add(candidate.EntityId, sendState);
            }
            bool teleport = sendState.HasSent && sendState.IsUrgent(candidate.State.PositionFixed);
            uint sequence = snapshotSequences.TryGetValue(candidate.EntityId, out uint lastSequence)
                ? unchecked(lastSequence + 1) : 1;
            RemotePlayerSnapshot snapshot = new RemotePlayerSnapshot(
                candidate.EntityId, serverWorldTick, candidate.State.PositionFixed,
                candidate.State.YawFixedDegrees, candidate.State.PitchFixedDegrees,
                candidate.State.VelocityFixed, isTeleport: teleport, isGrounded: candidate.State.IsGrounded,
                isCrouched: candidate.State.IsCrouched,
                isGrappling: candidate.State.IsGrappling, grapplePoint: candidate.State.GrapplePointFixed);
            RemotePlayerSnapshot baseline = sendState.FullBaseline;
            bool sendFull = !sendState.HasSent || teleport
                || unchecked(serverWorldTick - baseline.ServerTick) >= fullSnapshotIntervalTicks;
            int packetBytes;
            ArraySegment<byte> packet;
            if (sendFull)
            {
                if (!RemotePlayerSnapshotCodec.TrySerialize(
                    snapshot, sequence, new ArraySegment<byte>(snapshotBuffer), out packetBytes))
                {
                    return false;
                }
                packet = new ArraySegment<byte>(snapshotBuffer, 0, packetBytes);
            }
            else
            {
                if (!RemotePlayerSnapshotDeltaCodec.TrySerialize(
                    snapshot, baseline, sequence, new ArraySegment<byte>(snapshotDeltaBuffer), out packetBytes))
                {
                    return false;
                }
                packet = new ArraySegment<byte>(snapshotDeltaBuffer, 0, packetBytes);
            }

            snapshotSequences[candidate.EntityId] = sequence;
            if (serverTransport.Send(targetConnectionId, TransportDelivery.Unreliable, packet)
                == TransportSendResult.Success)
            {
                sendState.RecordSuccessfulSend(snapshot, sendFull);
                Metrics.SnapshotPacketsSent++;
                Metrics.SnapshotBytesSent += (ulong)packetBytes;
                return true;
            }

            return false;
        }
        private static bool TryReadDeltaEntityId(ArraySegment<byte> packet, out uint entityId)
        {
            entityId = 0;
            if (!PacketHeaderCodec.TryRead(packet, out PacketHeader header) || header.MessageType != NetworkMessageType.RemotePlayerSnapshotDelta || header.PayloadLength < RemotePlayerSnapshotDeltaCodec.FixedPayloadSize) return false;
            int offset = packet.Offset + PacketHeader.Size;
            entityId = (uint)(packet.Array[offset] | packet.Array[offset + 1] << 8 | packet.Array[offset + 2] << 16 | packet.Array[offset + 3] << 24);
            return entityId != 0;
        }

        private static long ElapsedMicroseconds(long started)
        {
            return (long)((Stopwatch.GetTimestamp() - started) * 1000000.0 / Stopwatch.Frequency);
        }

        private uint AllocateEntityId()
        {
            uint entityId = nextEntityId;
            nextEntityId = unchecked(nextEntityId + 1);
            if (nextEntityId == 0)
            {
                nextEntityId = 1;
            }

            return entityId;
        }

        private bool SendAssignment(TransportConnectionId connectionId, uint entityId)
        {
            uint sequence = lifecycleSequence++;
            if (PlayerSessionAssignmentCodec.TrySerialize(new PlayerSessionAssignment(entityId), sequence, new ArraySegment<byte>(assignmentBuffer), out int packetBytes))
            {
                return serverTransport.Send(connectionId, TransportDelivery.ReliableSequenced,
                    new ArraySegment<byte>(assignmentBuffer, 0, packetBytes)) == TransportSendResult.Success;
            }
            return false;
        }

        private bool SendDespawn(TransportConnectionId targetConnectionId, uint entityId)
        {
            uint sequence = lifecycleSequence++;
            if (PlayerDespawnCodec.TrySerialize(entityId, sequence, new ArraySegment<byte>(despawnBuffer), out int packetBytes))
            {
                return serverTransport.Send(targetConnectionId, TransportDelivery.ReliableSequenced, new ArraySegment<byte>(despawnBuffer, 0, packetBytes)) == TransportSendResult.Success;
            }

            return false;
        }

        private void BroadcastDespawn(uint entityId)
        {
            uint sequence = lifecycleSequence++;
            if (!PlayerDespawnCodec.TrySerialize(entityId, sequence, new ArraySegment<byte>(despawnBuffer), out int packetBytes))
            {
                return;
            }

            foreach (TransportConnectionId targetConnectionId in serverSessions.Keys)
            {
                if (serverTransport.Send(targetConnectionId, TransportDelivery.ReliableSequenced,
                    new ArraySegment<byte>(despawnBuffer, 0, packetBytes)) == TransportSendResult.Success)
                {
                    serverVisibleEntities[targetConnectionId].Remove(entityId);
                }
            }
        }
    }
}

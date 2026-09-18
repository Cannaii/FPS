using System;
using System.Collections.Generic;
using System.Diagnostics;
using AFPS.NetCode.InputReplication;
using AFPS.NetCode.LagCompensation;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Metrics;
using AFPS.NetCode.Prediction;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.Runtime;
using AFPS.NetCode.SnapshotInterpolation;
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
    public sealed class NetworkMovementSessionManager
    {
        private readonly Dictionary<TransportConnectionId, ServerAuthoritativeMovementSession> serverSessions = new Dictionary<TransportConnectionId, ServerAuthoritativeMovementSession>();
        private readonly Dictionary<TransportConnectionId, uint> serverEntityIds = new Dictionary<TransportConnectionId, uint>();
        private readonly Dictionary<uint, uint> snapshotSequences = new Dictionary<uint, uint>();
        private readonly Dictionary<uint, float> serverHealth = new Dictionary<uint, float>();
        private readonly Dictionary<uint, RewindHitboxHistory> rewindHistories = new Dictionary<uint, RewindHitboxHistory>();
        private readonly Dictionary<TransportConnectionId, Dictionary<uint, RemotePlayerSnapshot>> serverSnapshotBaselines = new Dictionary<TransportConnectionId, Dictionary<uint, RemotePlayerSnapshot>>();
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
        private readonly int predictionHistoryCapacity;
        private readonly int inputRedundancyCount;
        private readonly int serverInputWindowCapacity;
        private readonly int maxMissingInputWaitTicks;
        private readonly int maxRepeatedMovementTicks;
        private readonly float positionErrorThreshold;
        private readonly float velocityErrorThreshold;
        private readonly ICharacterCollisionWorld collisionWorld;
        private readonly float serverSpawnSpacing;
        private readonly WeaponSimulationConfig weaponConfig;
        private readonly uint maximumRewindTicks;
        private readonly float interestRadiusSquared;
        private readonly int snapshotBudgetPerClientPerTick;
        private readonly uint fullSnapshotIntervalTicks;
        private readonly List<SnapshotCandidate> snapshotCandidates = new List<SnapshotCandidate>();
        private readonly HashSet<uint> interestedEntityIds = new HashSet<uint>();
        private readonly List<uint> noLongerVisibleEntityIds = new List<uint>();
        private readonly byte[] assignmentBuffer = new byte[PlayerSessionAssignmentCodec.PacketSize];
        private readonly byte[] despawnBuffer = new byte[PlayerDespawnCodec.PacketSize];
        private readonly byte[] snapshotBuffer = new byte[RemotePlayerSnapshotCodec.PacketSize];
        private readonly byte[] snapshotDeltaBuffer = new byte[RemotePlayerSnapshotDeltaCodec.MaximumPacketSize];
        private readonly byte[] shotResultBuffer = new byte[AuthoritativeShotResultCodec.PacketSize];
        private TransportConnectionId clientConnectionId;
        private uint nextEntityId = 1;
        private uint lifecycleSequence = 1;
        private uint shotResultPacketSequence = 1;
        private bool hasReceivedShotResultSequence;
        private uint lastReceivedShotResultSequence;

        /// <summary>
        /// 当前服务器侧维护的已连接玩家权威会话数量。
        /// </summary>
        public int ServerSessionCount => serverSessions.Count;

        /// <summary>
        /// 当前客户端与服务器连接成功后创建的本地预测会话。
        /// </summary>
        public ClientPredictedMovementSession ClientSession { get; private set; }

        /// <summary>客户端连接期间保存的远端玩家快照集合；纯服务器进程中为 null。</summary>
        public RemotePlayerReplicaSet RemotePlayers { get; private set; }

        public AuthoritativeShotResult LastReceivedShotResult { get; private set; }

        public uint ShotResultVersion { get; private set; }

        public NetworkRuntimeMetrics Metrics { get; } = new NetworkRuntimeMetrics();

        private readonly struct SnapshotCandidate
        {
            public readonly uint EntityId;
            public readonly PlayerState State;
            public readonly float DistanceSquared;

            public SnapshotCandidate(uint entityId, in PlayerState state, float distanceSquared)
            {
                EntityId = entityId;
                State = state;
                DistanceSquared = distanceSquared;
            }
        }

        public NetworkMovementSessionManager(IGameTransport serverTransport, IGameTransport clientTransport, in PlayerState serverInitialState, in PlayerState clientInitialState, in PlayerSimulationConfig simulationConfig, float tickDeltaTime, int predictionHistoryCapacity, int inputRedundancyCount, int serverInputWindowCapacity, int maxMissingInputWaitTicks, int maxRepeatedMovementTicks, float positionErrorThreshold, float velocityErrorThreshold, ICharacterCollisionWorld collisionWorld = null, float serverSpawnSpacing = 2.5f, int remoteSnapshotCapacity = 32, double remoteInterpolationDelayTicks = 2.0, double remoteMaxExtrapolationTicks = 2.0, float remoteTeleportDistance = 5f, WeaponSimulationConfig? weaponConfig = null, uint maximumRewindTicks = 10, float interestRadius = 150f, int snapshotBudgetPerClientPerTick = 32, uint fullSnapshotIntervalTicks = 10)
        {
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

            if (interestRadius <= 0f || float.IsNaN(interestRadius) || float.IsInfinity(interestRadius)) throw new ArgumentOutOfRangeException(nameof(interestRadius));
            if (snapshotBudgetPerClientPerTick <= 0) throw new ArgumentOutOfRangeException(nameof(snapshotBudgetPerClientPerTick));
            if (fullSnapshotIntervalTicks == 0) throw new ArgumentOutOfRangeException(nameof(fullSnapshotIntervalTicks));

            this.serverTransport = serverTransport;
            this.clientTransport = clientTransport;
            this.serverInitialState = serverInitialState;
            this.clientInitialState = clientInitialState;
            this.simulationConfig = simulationConfig;
            this.tickDeltaTime = tickDeltaTime;
            this.predictionHistoryCapacity = predictionHistoryCapacity;
            this.inputRedundancyCount = inputRedundancyCount;
            this.serverInputWindowCapacity = serverInputWindowCapacity;
            this.maxMissingInputWaitTicks = maxMissingInputWaitTicks;
            this.maxRepeatedMovementTicks = maxRepeatedMovementTicks;
            this.positionErrorThreshold = positionErrorThreshold;
            this.velocityErrorThreshold = velocityErrorThreshold;
            this.collisionWorld = collisionWorld ?? FlatGroundCollisionWorld.Instance;
            this.serverSpawnSpacing = Mathf.Max(0f, serverSpawnSpacing);
            this.weaponConfig = weaponConfig ?? WeaponSimulationConfig.DefaultRifle;
            if (!this.weaponConfig.IsValid)
            {
                throw new ArgumentException("武器配置无效。", nameof(weaponConfig));
            }
            this.maximumRewindTicks = maximumRewindTicks;
            interestRadiusSquared = interestRadius * interestRadius;
            this.snapshotBudgetPerClientPerTick = snapshotBudgetPerClientPerTick;
            this.fullSnapshotIntervalTicks = fullSnapshotIntervalTicks;
            if (clientTransport != null)
            {
                RemotePlayers = new RemotePlayerReplicaSet(remoteSnapshotCapacity, tickDeltaTime, remoteInterpolationDelayTicks, remoteMaxExtrapolationTicks, remoteTeleportDistance);
            }
        }

        /// <summary>
        /// 连接建立时创建对应会话。客户端输入 Tick 从零开始，不依赖本地世界 Tick 的当前值。
        /// </summary>
        public bool HandleConnected(NetworkTransportSide side, TransportConnectionId connectionId)
        {
            if (!connectionId.IsValid)
            {
                return false;
            }

            if (side == NetworkTransportSide.Server)
            {
                if (serverTransport == null || serverSessions.ContainsKey(connectionId))
                {
                    return false;
                }

                uint entityId = AllocateEntityId();
                PlayerState spawnedState = serverInitialState;
                spawnedState.Position += Vector3.right * (entityId - 1) * serverSpawnSpacing;
                serverSessions.Add(connectionId, new ServerAuthoritativeMovementSession(serverTransport, connectionId, spawnedState, simulationConfig, tickDeltaTime, serverInputWindowCapacity, maxMissingInputWaitTicks, maxRepeatedMovementTicks, collisionWorld, entityId, weaponConfig));
                serverEntityIds.Add(connectionId, entityId);
                serverHealth.Add(entityId, 100f);
                rewindHistories.Add(entityId, new RewindHitboxHistory((int)maximumRewindTicks + 2));
                serverSnapshotBaselines.Add(connectionId, new Dictionary<uint, RemotePlayerSnapshot>());
                serverVisibleEntities.Add(connectionId, new HashSet<uint>());
                SendAssignment(connectionId, entityId);
                return true;
            }

            if (side != NetworkTransportSide.Client || clientTransport == null)
            {
                return false;
            }

            clientConnectionId = connectionId;
            ClientSession = new ClientPredictedMovementSession(clientTransport, connectionId, clientInitialState, simulationConfig, tickDeltaTime, predictionHistoryCapacity, inputRedundancyCount, positionErrorThreshold, velocityErrorThreshold, collisionWorld);
            RemotePlayers?.Clear();
            clientSnapshotBaselines.Clear();
            return true;
        }

        /// <summary>
        /// 断开连接时丢弃对应会话及其序号、输入窗口和预测历史。
        /// </summary>
        public bool HandleDisconnected(NetworkTransportSide side, TransportConnectionId connectionId)
        {
            if (side == NetworkTransportSide.Server)
            {
                if (!serverSessions.Remove(connectionId) || !serverEntityIds.TryGetValue(connectionId, out uint entityId))
                {
                    return false;
                }

                serverEntityIds.Remove(connectionId);
                serverHealth.Remove(entityId);
                rewindHistories.Remove(entityId);
                serverSnapshotBaselines.Remove(connectionId);
                serverVisibleEntities.Remove(connectionId);
                foreach (Dictionary<uint, RemotePlayerSnapshot> baselines in serverSnapshotBaselines.Values)
                {
                    baselines.Remove(entityId);
                }
                foreach (HashSet<uint> visibleEntities in serverVisibleEntities.Values)
                {
                    visibleEntities.Remove(entityId);
                }
                snapshotSequences.Remove(entityId);
                BroadcastDespawn(entityId);
                return true;
            }

            if (side != NetworkTransportSide.Client || ClientSession == null || connectionId != clientConnectionId)
            {
                return false;
            }

            clientConnectionId = default;
            ClientSession = null;
            RemotePlayers?.Clear();
            clientSnapshotBaselines.Clear();
            return true;
        }

        /// <summary>
        /// 按包头消息类型把输入包交给服务器会话，或把权威状态交给客户端会话。
        /// </summary>
        public bool TryHandleData(NetworkTransportSide side, TransportConnectionId connectionId, ArraySegment<byte> packet, out ReconciliationResult reconciliationResult)
        {
            reconciliationResult = default;
            if (!PacketHeaderCodec.TryRead(packet, out PacketHeader header))
            {
                return false;
            }

            if (side == NetworkTransportSide.Server)
            {
                return header.MessageType == NetworkMessageType.InputCommandBatch && serverSessions.TryGetValue(connectionId, out ServerAuthoritativeMovementSession serverSession) && serverSession.TryReceiveInputPacket(packet, out _);
            }

            if (side != NetworkTransportSide.Client || connectionId != clientConnectionId)
            {
                return false;
            }

            switch (header.MessageType)
            {
                case NetworkMessageType.AuthoritativePlayerState:
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
            if (ClientSession == null)
            {
                state = default;
                sendResult = default;
                return false;
            }

            state = ClientSession.PredictAndSend(command, out sendResult);
            return true;
        }

        /// <summary>
        /// 在同一个服务器世界 Tick 中推进所有已连接玩家的权威会话。
        /// </summary>
        public int AdvanceServerSessions(uint serverWorldTick)
        {
            long tickStarted = Stopwatch.GetTimestamp();
            int advancedCount = 0;
            pendingWeaponFireResults.Clear();
            foreach (KeyValuePair<TransportConnectionId, ServerAuthoritativeMovementSession> pair in serverSessions)
            {
                if (pair.Value.TryAdvance(serverWorldTick, out _, out _))
                {
                    advancedCount++;
                    if (pair.Value.LastWeaponFireResult.Accepted)
                    {
                        pendingWeaponFireResults.Add(pair.Value.LastWeaponFireResult);
                    }
                }
            }

            ReplicateRemoteSnapshots(serverWorldTick);
            RecordCurrentHitboxes(serverWorldTick);
            ResolveCombatTick(serverWorldTick);

            Metrics.LastServerTickMicroseconds = ElapsedMicroseconds(tickStarted);
            if (Metrics.LastServerTickMicroseconds > Metrics.MaximumServerTickMicroseconds) Metrics.MaximumServerTickMicroseconds = Metrics.LastServerTickMicroseconds;

            return advancedCount;
        }

        public bool TryGetServerSession(TransportConnectionId connectionId, out ServerAuthoritativeMovementSession session) => serverSessions.TryGetValue(connectionId, out session);

        public bool TryGetServerEntityId(TransportConnectionId connectionId, out uint entityId) => serverEntityIds.TryGetValue(connectionId, out entityId);

        public bool TryGetServerHealth(uint entityId, out float health) => serverHealth.TryGetValue(entityId, out health);

        private void ResolveCombatTick(uint serverWorldTick)
        {
            if (pendingWeaponFireResults.Count == 0 && projectileSimulation.Count == 0)
            {
                return;
            }

            BuildCombatTargets(serverWorldTick, serverWorldTick);

            for (int i = 0; i < pendingWeaponFireResults.Count; i++)
            {
                WeaponFireResult fireResult = pendingWeaponFireResults[i];
                AuthoritativeShot shot = fireResult.Shot;
                if (shot.FireMode == WeaponFireMode.Projectile)
                {
                    projectileSimulation.Spawn(shot, fireResult.RemainingAmmo);
                    BroadcastShotResult(new AuthoritativeShotResult(shot, fireResult.RemainingAmmo, false, 0, 0f, shot.Origin + shot.Direction * shot.Range));
                    continue;
                }

                ServerCombatTarget target = default;
                Vector3 hitPoint = shot.Origin + shot.Direction * shot.Range;
                BuildCombatTargets(shot.RewindTick, serverWorldTick);
                long rewindStarted = Stopwatch.GetTimestamp();
                bool didHit = shot.FireMode == WeaponFireMode.Hitscan && ServerHitscanResolver.TryResolve(shot, combatTargets, out target, out hitPoint);
                Metrics.RewindQueries++;
                Metrics.LastRewindMicroseconds = ElapsedMicroseconds(rewindStarted);
                uint targetEntityId = 0;
                float targetHealth = 0f;
                if (didHit)
                {
                    targetEntityId = target.EntityId;
                    targetHealth = Mathf.Max(0f, serverHealth[targetEntityId] - shot.Damage);
                    serverHealth[targetEntityId] = targetHealth;
                    for (int targetIndex = 0; targetIndex < combatTargets.Count; targetIndex++)
                    {
                        if (combatTargets[targetIndex].EntityId == targetEntityId)
                        {
                            combatTargets[targetIndex] = new ServerCombatTarget(targetEntityId, target.Position, target.Radius, target.Height, targetHealth);
                            break;
                        }
                    }
                }
                BroadcastShotResult(new AuthoritativeShotResult(shot, fireResult.RemainingAmmo, didHit, targetEntityId, targetHealth, hitPoint));
            }

            BuildCombatTargets(serverWorldTick, serverWorldTick);
            projectileImpacts.Clear();
            projectileSimulation.Advance(tickDeltaTime, combatTargets, projectileImpacts);
            for (int i = 0; i < projectileImpacts.Count; i++)
            {
                AuthoritativeShotResult impact = projectileImpacts[i];
                float targetHealth = Mathf.Max(0f, serverHealth[impact.TargetEntityId] - impact.Shot.Damage);
                serverHealth[impact.TargetEntityId] = targetHealth;
                BroadcastShotResult(new AuthoritativeShotResult(impact.Shot, impact.RemainingAmmo, true, impact.TargetEntityId, targetHealth, impact.HitPoint));
            }
        }

        private void RecordCurrentHitboxes(uint serverWorldTick)
        {
            foreach (KeyValuePair<TransportConnectionId, ServerAuthoritativeMovementSession> pair in serverSessions)
            {
                uint entityId = serverEntityIds[pair.Key];
                ServerCombatTarget target = new ServerCombatTarget(entityId, pair.Value.CurrentState.Position, 0.5f, 2f, serverHealth[entityId]);
                rewindHistories[entityId].Record(serverWorldTick, target);
            }
        }

        private void BuildCombatTargets(uint requestedTick, uint currentTick)
        {
            combatTargets.Clear();
            foreach (KeyValuePair<uint, RewindHitboxHistory> pair in rewindHistories)
            {
                if (pair.Value.TryGetAtOrBefore(requestedTick, currentTick, maximumRewindTicks, out ServerCombatTarget historical, out _))
                {
                    combatTargets.Add(new ServerCombatTarget(pair.Key, historical.Position, historical.Radius, historical.Height, serverHealth[pair.Key]));
                }
            }
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
                Vector3 observerPosition = targetPair.Value.CurrentState.Position;
                foreach (KeyValuePair<TransportConnectionId, ServerAuthoritativeMovementSession> ownerPair in serverSessions)
                {
                    if (ownerPair.Key == targetPair.Key) continue;
                    float distanceSquared = (ownerPair.Value.CurrentState.Position - observerPosition).sqrMagnitude;
                    if (distanceSquared <= interestRadiusSquared)
                    {
                        uint entityId = serverEntityIds[ownerPair.Key];
                        interestedEntityIds.Add(entityId);
                        snapshotCandidates.Add(new SnapshotCandidate(entityId, ownerPair.Value.CurrentState, distanceSquared));
                    }
                }

                HashSet<uint> visibleEntities = serverVisibleEntities[targetPair.Key];
                noLongerVisibleEntityIds.Clear();
                foreach (uint entityId in visibleEntities)
                {
                    if (!interestedEntityIds.Contains(entityId)) noLongerVisibleEntityIds.Add(entityId);
                }

                for (int i = 0; i < noLongerVisibleEntityIds.Count; i++)
                {
                    uint entityId = noLongerVisibleEntityIds[i];
                    if (SendDespawn(targetPair.Key, entityId))
                    {
                        visibleEntities.Remove(entityId);
                        serverSnapshotBaselines[targetPair.Key].Remove(entityId);
                    }
                }

                snapshotCandidates.Sort((left, right) => left.DistanceSquared.CompareTo(right.DistanceSquared));
                int count = Mathf.Min(snapshotBudgetPerClientPerTick, snapshotCandidates.Count);
                for (int i = 0; i < count; i++)
                {
                    if (SendRemoteSnapshot(targetPair.Key, snapshotCandidates[i], serverWorldTick))
                    {
                        visibleEntities.Add(snapshotCandidates[i].EntityId);
                    }
                }
            }
        }

        private bool SendRemoteSnapshot(TransportConnectionId targetConnectionId, in SnapshotCandidate candidate, uint serverWorldTick)
        {
            uint sequence = snapshotSequences.TryGetValue(candidate.EntityId, out uint lastSequence) ? unchecked(lastSequence + 1) : 1;
            RemotePlayerSnapshot snapshot = new RemotePlayerSnapshot(candidate.EntityId, serverWorldTick, candidate.State.Position, Quaternion.Euler(candidate.State.Pitch, candidate.State.Yaw, 0f), candidate.State.Velocity);
            Dictionary<uint, RemotePlayerSnapshot> baselines = serverSnapshotBaselines[targetConnectionId];
            bool sendFull = !baselines.TryGetValue(candidate.EntityId, out RemotePlayerSnapshot baseline) || unchecked(serverWorldTick - baseline.ServerTick) >= fullSnapshotIntervalTicks;
            RemotePlayerSnapshot canonicalFullBaseline = default;
            int packetBytes;
            ArraySegment<byte> packet;
            if (sendFull)
            {
                if (!RemotePlayerSnapshotCodec.TrySerialize(snapshot, sequence, new ArraySegment<byte>(snapshotBuffer), out packetBytes)) return false;
                if (!RemotePlayerSnapshotCodec.TryDeserialize(new ArraySegment<byte>(snapshotBuffer, 0, packetBytes), out _, out canonicalFullBaseline)) return false;
                packet = new ArraySegment<byte>(snapshotBuffer, 0, packetBytes);
            }
            else
            {
                if (!RemotePlayerSnapshotDeltaCodec.TrySerialize(snapshot, baseline, sequence, new ArraySegment<byte>(snapshotDeltaBuffer), out packetBytes)) return false;
                packet = new ArraySegment<byte>(snapshotDeltaBuffer, 0, packetBytes);
            }
            snapshotSequences[candidate.EntityId] = sequence;
            if (serverTransport.Send(targetConnectionId, TransportDelivery.Unreliable, packet) == TransportSendResult.Success)
            {
                if (sendFull) baselines[candidate.EntityId] = canonicalFullBaseline;
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

        private void SendAssignment(TransportConnectionId connectionId, uint entityId)
        {
            uint sequence = lifecycleSequence++;
            if (PlayerSessionAssignmentCodec.TrySerialize(new PlayerSessionAssignment(entityId), sequence, new ArraySegment<byte>(assignmentBuffer), out int packetBytes))
            {
                serverTransport.Send(connectionId, TransportDelivery.ReliableSequenced, new ArraySegment<byte>(assignmentBuffer, 0, packetBytes));
            }
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
                serverTransport.Send(targetConnectionId, TransportDelivery.ReliableSequenced, new ArraySegment<byte>(despawnBuffer, 0, packetBytes));
            }
        }
    }
}

using System;
using System.Collections.Generic;
using AFPS.NetCode.InputReplication;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Prediction;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.Runtime;
using AFPS.NetCode.SnapshotInterpolation;
using AFPS.NetCode.Transport;
using AFPS.Simulation.Characters;
using AFPS.Simulation.Characters.Collision;
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
        private readonly byte[] assignmentBuffer = new byte[PlayerSessionAssignmentCodec.PacketSize];
        private readonly byte[] despawnBuffer = new byte[PlayerDespawnCodec.PacketSize];
        private readonly byte[] snapshotBuffer = new byte[RemotePlayerSnapshotCodec.PacketSize];
        private TransportConnectionId clientConnectionId;
        private uint nextEntityId = 1;
        private uint lifecycleSequence = 1;

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

        public NetworkMovementSessionManager(IGameTransport serverTransport, IGameTransport clientTransport, in PlayerState serverInitialState, in PlayerState clientInitialState, in PlayerSimulationConfig simulationConfig, float tickDeltaTime, int predictionHistoryCapacity, int inputRedundancyCount, int serverInputWindowCapacity, int maxMissingInputWaitTicks, int maxRepeatedMovementTicks, float positionErrorThreshold, float velocityErrorThreshold, ICharacterCollisionWorld collisionWorld = null, float serverSpawnSpacing = 2.5f, int remoteSnapshotCapacity = 32, double remoteInterpolationDelayTicks = 2.0, double remoteMaxExtrapolationTicks = 2.0, float remoteTeleportDistance = 5f)
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
                serverSessions.Add(connectionId, new ServerAuthoritativeMovementSession(serverTransport, connectionId, spawnedState, simulationConfig, tickDeltaTime, serverInputWindowCapacity, maxMissingInputWaitTicks, maxRepeatedMovementTicks, collisionWorld));
                serverEntityIds.Add(connectionId, entityId);
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
                    return ClientSession != null && ClientSession.TryReceiveAuthoritativePacket(packet, out _, out reconciliationResult);
                case NetworkMessageType.PlayerSessionAssignment:
                    return RemotePlayers != null && RemotePlayers.TryApplyAssignment(packet);
                case NetworkMessageType.RemotePlayerSnapshot:
                    return RemotePlayers != null && RemotePlayers.TryInsertSnapshot(packet, out _);
                case NetworkMessageType.PlayerDespawn:
                    return RemotePlayers != null && RemotePlayers.TryApplyDespawn(packet, out _);
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
            int advancedCount = 0;
            foreach (KeyValuePair<TransportConnectionId, ServerAuthoritativeMovementSession> pair in serverSessions)
            {
                if (pair.Value.TryAdvance(serverWorldTick, out _, out _))
                {
                    advancedCount++;
                    BroadcastRemoteSnapshot(pair.Key, serverEntityIds[pair.Key], serverWorldTick, pair.Value.CurrentState);
                }
            }

            return advancedCount;
        }

        public bool TryGetServerSession(TransportConnectionId connectionId, out ServerAuthoritativeMovementSession session) => serverSessions.TryGetValue(connectionId, out session);

        public bool TryGetServerEntityId(TransportConnectionId connectionId, out uint entityId) => serverEntityIds.TryGetValue(connectionId, out entityId);

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

        private void BroadcastRemoteSnapshot(TransportConnectionId ownerConnectionId, uint entityId, uint serverWorldTick, in PlayerState state)
        {
            uint sequence = snapshotSequences.TryGetValue(entityId, out uint lastSequence) ? unchecked(lastSequence + 1) : 1;
            RemotePlayerSnapshot snapshot = new RemotePlayerSnapshot(entityId, serverWorldTick, state.Position, Quaternion.Euler(state.Pitch, state.Yaw, 0f), state.Velocity);
            if (!RemotePlayerSnapshotCodec.TrySerialize(snapshot, sequence, new ArraySegment<byte>(snapshotBuffer), out int packetBytes))
            {
                return;
            }

            snapshotSequences[entityId] = sequence;
            foreach (TransportConnectionId targetConnectionId in serverSessions.Keys)
            {
                if (targetConnectionId != ownerConnectionId)
                {
                    serverTransport.Send(targetConnectionId, TransportDelivery.Unreliable, new ArraySegment<byte>(snapshotBuffer, 0, packetBytes));
                }
            }
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

using System;
using System.Collections.Generic;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.Runtime;
using AFPS.NetCode.SnapshotInterpolation;
using AFPS.NetCode.StateReplication;
using AFPS.NetCode.TimeSync;
using AFPS.NetCode.Transport;

namespace AFPS.NetCode.Sessions
{
    public sealed partial class NetworkMovementSessionManager
    {
        // 单位为服务器世界Tick；50Hz下分别保留30秒、等待恢复确认5秒，不随重复请求续期。
        private const uint SessionRetentionTicks = 1500;
        private const uint RecoveryReadyTimeoutTicks = 250;
        private readonly RetainedServerSessionStore retainedSessions = new RetainedServerSessionStore(64, SessionRetentionTicks);
        private readonly ServerSessionCredentialStore serverCredentials = new ServerSessionCredentialStore();
        private readonly Dictionary<uint, ServerAuthoritativeMovementSession> allServerSessions
            = new Dictionary<uint, ServerAuthoritativeMovementSession>();
        private readonly Dictionary<TransportConnectionId, uint> recoveringConnections
            = new Dictionary<TransportConnectionId, uint>();
        private readonly HashSet<uint> recoveringEntityIds = new HashSet<uint>();
        private readonly List<uint> expiredRetainedEntities = new List<uint>();
        private readonly List<TransportConnectionId> expiredRecoveryConnections = new List<TransportConnectionId>();
        private readonly byte[] resumeGrantBuffer = new byte[SessionResumeCodec.GrantPacketSize];
        private readonly Dictionary<TransportConnectionId, RecoveryTransfer> recoveryTransfers
            = new Dictionary<TransportConnectionId, RecoveryTransfer>();
        private uint nextRecoveryEpoch = 1;

        private sealed class RecoveryTransfer
        {
            internal uint Epoch;
            internal SessionRecoverySeed Seed;
            internal byte[] Packet;
            internal bool HasSent;
            internal uint LastSendTick;
            internal ushort Deaths;
            internal AFPS.NetCode.Match.DeathmatchPhase Phase;
        }

        public int RetainedServerSessionCount => retainedSessions.Count;

        /// <summary>仅当前管理器内存保存；新连接用此凭据恢复，禁止记录原文。</summary>
        public SessionResumeCredential ClientResumeCredential { get; private set; }

        public bool IsServerRecoveryPending(TransportConnectionId connectionId)
        {
            return recoveringConnections.ContainsKey(connectionId);
        }

        private bool IsRecoveringEntity(uint entityId) => recoveringEntityIds.Contains(entityId);

        private bool ReceiveResumeGrant(uint entityId, SessionResumeCredential credential)
        {
            if (RemotePlayers == null || RemotePlayers.LocalEntityId != entityId)
            {
                return false;
            }
            ClientResumeCredential = credential;
            clientResumeEntityId = entityId;
            return true;
        }

        private void SendResumeGrant(TransportConnectionId connectionId, uint attempt)
        {
            if (serverEntityIds.TryGetValue(connectionId, out uint entityId)
                && serverCredentials.TryGetIssued(entityId, out var credential)
                && SessionResumeCodec.TryWriteGrant(entityId, credential, attempt,
                    new ArraySegment<byte>(resumeGrantBuffer), out int bytes))
            {
                serverTransport.Send(connectionId, TransportDelivery.ReliableSequenced,
                    new ArraySegment<byte>(resumeGrantBuffer, 0, bytes));
            }
        }

        private bool RestoreServerSession(TransportConnectionId connectionId, SessionResumeCredential credential)
        {
            ExpireRetainedSessions();
            if (serverSessions.ContainsKey(connectionId) || !serverCredentials.TryResolve(credential, out uint entityId)
                || !retainedSessions.TryRestore(entityId, latestServerWorldTick,
                    serverTransport, connectionId, out var session))
            {
                // 无效/到期/仍在线的凭据一律失败，不降级创建新玩家，也不删除已在线的原连接。
                return false;
            }
            serverSessions.Add(connectionId, session);
            serverEntityIds.Add(connectionId, entityId);
            RegisterLobbyMember(entityId, recovering: true);
            serverSnapshotStates.Add(connectionId, new Dictionary<uint, RemoteSnapshotSendState>());
            serverVisibleEntities.Add(connectionId, new HashSet<uint>());
            timeSyncResponders.Add(connectionId, new ServerTimeSyncResponder(serverTransport, connectionId));
            recoveringConnections.Add(connectionId, latestServerWorldTick);
            recoveringEntityIds.Add(entityId);
            if (retainedSessions.Count == 0)
            {
                retainedSessions.Clear();
            }
            TrySendRecoverySeed(connectionId, session);
            deathmatchStateDirty = true;
            return true;
        }

        private void TrySendRecoverySeed(TransportConnectionId connectionId, ServerAuthoritativeMovementSession session)
        {
            // 分配必须先入同一可靠通道，武器种子才有所属实体；失败保持门禁并在下一Tick重试。
            if (session.RecoverySnapshotPending && SendAssignment(connectionId, session.EntityId))
            {
                session.TrySendRecoverySnapshot(latestServerWorldTick, out _);
            }
            if (!recoveryTransfers.TryGetValue(connectionId, out var transfer)
                || !IsCurrentRecoveryTransfer(transfer, session))
            {
                // 等待期间可能死亡/重生；Raw改变后生成新世代，旧ACK不能打开新状态的门禁。
                uint epoch = nextRecoveryEpoch;
                nextRecoveryEpoch = unchecked(nextRecoveryEpoch + 1);
                if (nextRecoveryEpoch == 0)
                {
                    nextRecoveryEpoch = 1;
                }
                var seed = session.CreateRecoverySeed(admission.ServerAttempt(connectionId), latestServerWorldTick);
                byte[] packet = new byte[SessionRecoveryCodec.SeedPacketSize];
                if (!SessionRecoveryCodec.TryWriteSeed(seed, epoch, new ArraySegment<byte>(packet), out _))
                {
                    return;
                }
                transfer = new RecoveryTransfer
                {
                    Epoch = epoch,
                    Seed = seed,
                    Packet = packet,
                    Deaths = serverDeaths[session.EntityId],
                    Phase = deathmatchPhase
                };
                recoveryTransfers[connectionId] = transfer;
            }
            if (!transfer.HasSent || unchecked(latestServerWorldTick - transfer.LastSendTick) >= 25)
            {
                // 同世代重试复用原字节，不能让重复种子把客户端反复回滚。
                if (SendAssignment(connectionId, session.EntityId)
                    && serverTransport.Send(connectionId, TransportDelivery.ReliableSequenced,
                        new ArraySegment<byte>(transfer.Packet)) == TransportSendResult.Success)
                {
                    transfer.HasSent = true;
                    transfer.LastSendTick = latestServerWorldTick;
                }
            }
        }

        private bool ReceiveRecoveryAck(TransportConnectionId connectionId, ArraySegment<byte> packet)
        {
            if (!SessionRecoveryCodec.TryReadReceipt(packet, NetworkMessageType.SessionRecoveryAck,
                    out uint attempt, out uint epoch, out uint entityId)
                || !recoveryTransfers.TryGetValue(connectionId, out var transfer) || !transfer.HasSent
                || transfer.Epoch != epoch || transfer.Seed.Attempt != attempt || transfer.Seed.EntityId != entityId
                || !serverSessions.TryGetValue(connectionId, out var session))
            {
                return false;
            }
            if (recoveringConnections.ContainsKey(connectionId))
            {
                if (session.RecoverySnapshotPending || !IsCurrentRecoveryTransfer(transfer, session))
                {
                    TrySendRecoverySeed(connectionId, session);
                    return false;
                }
                recoveringConnections.Remove(connectionId);
                recoveringEntityIds.Remove(entityId);
                var snapshot = lobby.Snapshot();
                for (int index = 0; index < snapshot.PlayerCount; index++)
                {
                    var member = snapshot.GetPlayer(index);
                    if (member.EntityId == entityId)
                    {
                        lobby.ConfirmRecovery(entityId, member.MembershipEpoch);
                        lobbyDirty = true;
                        break;
                    }
                }
            }
            // 已放行后的重复ACK只重发确认；不能再次创建、重置或补发旧种子。
            SendRecoveryReceipt(serverTransport, connectionId, NetworkMessageType.SessionRecoveryConfirmed,
                attempt, epoch, entityId);
            return true;
        }

        private bool IsCurrentRecoveryTransfer(RecoveryTransfer transfer, ServerAuthoritativeMovementSession session)
        {
            // 在同出生点立即重生时Raw可能完全相同；生命计数和对局阶段也必须属于当前世代。
            return transfer.Seed.Movement.State.RawEquals(session.CurrentState)
                && transfer.Deaths == serverDeaths[session.EntityId] && transfer.Phase == deathmatchPhase;
        }

        private static bool SendRecoveryReceipt(IGameTransport transport, TransportConnectionId connection,
            NetworkMessageType type, uint attempt, uint epoch, uint entityId)
        {
            byte[] buffer = new byte[SessionRecoveryCodec.ReceiptPacketSize];
            return SessionRecoveryCodec.TryWriteReceipt(type, attempt, epoch, entityId,
                new ArraySegment<byte>(buffer), out int count)
                && transport.Send(connection, TransportDelivery.ReliableSequenced,
                    new ArraySegment<byte>(buffer, 0, count)) == TransportSendResult.Success;
        }

        private void AdvanceRecoveringConnections()
        {
            expiredRecoveryConnections.Clear();
            foreach (var pair in recoveringConnections)
            {
                if (unchecked(latestServerWorldTick - pair.Value) >= RecoveryReadyTimeoutTicks)
                {
                    expiredRecoveryConnections.Add(pair.Key);
                }
                else
                {
                    TrySendRecoverySeed(pair.Key, serverSessions[pair.Key]);
                }
            }
            foreach (var connectionId in expiredRecoveryConnections)
            {
                // 11C接客户端ACK前，恢复请求停在待确认态；不能无限占用活跃连接或提前处理输入。
                admission.HandleDisconnected(NetworkTransportSide.Server, connectionId);
                serverTransport.Disconnect(connectionId);
            }
        }

        private void ExpireRetainedSessions()
        {
            if (retainedSessions.Count == 0)
            {
                return;
            }
            expiredRetainedEntities.Clear();
            retainedSessions.Expire(latestServerWorldTick, expiredRetainedEntities);
            if (retainedSessions.Count == 0)
            {
                // 没有租约时重置保留区时钟原点，长期空服后首个离线租约仍可从当前Tick建立。
                retainedSessions.Clear();
            }
            foreach (uint entityId in expiredRetainedEntities)
            {
                RemoveServerEntity(entityId);
            }
        }

        private void RemoveServerEntity(uint entityId)
        {
            lobby.Remove(entityId);
            lobbyDirty = true;
            if (allServerSessions.TryGetValue(entityId, out var removedSession))
            {
                removedSession.StateDrift.Compared -= OnStateHashCompared;
            }
            allServerSessions.Remove(entityId);
            serverCredentials.Revoke(entityId);
            serverHealth.Remove(entityId);
            serverKills.Remove(entityId);
            serverDeaths.Remove(entityId);
            serverRespawnTicks.Remove(entityId);
            rewindHistories.Remove(entityId);
            recoveringEntityIds.Remove(entityId);
            snapshotSequences.Remove(entityId);
            foreach (var states in serverSnapshotStates.Values)
            {
                states.Remove(entityId);
            }
            // Despawn发送失败时保留可见集合项，下一Tick的兴趣差集会继续重试，避免远端永久留下占位体。
            BroadcastDespawn(entityId);
            if (allServerSessions.Count == 0)
            {
                // 最后一个保留实体也到期才复位对局；无人联网时弹丸和终局时钟仍照常推进。
                ResetEmptyDeathmatch();
            }
            deathmatchStateDirty = true;
        }
    }
}

using System;
using System.Collections.Generic;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.Runtime;
using AFPS.NetCode.SnapshotInterpolation;
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

        public int RetainedServerSessionCount => retainedSessions.Count;

        /// <summary>仅当前管理器内存保存；11C接客户端重试前不会自动发送恢复请求，禁止记录原文。</summary>
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
            serverSnapshotBaselines.Add(connectionId, new Dictionary<uint, RemotePlayerSnapshot>());
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
            allServerSessions.Remove(entityId);
            serverCredentials.Revoke(entityId);
            serverHealth.Remove(entityId);
            serverKills.Remove(entityId);
            serverDeaths.Remove(entityId);
            serverRespawnTicks.Remove(entityId);
            rewindHistories.Remove(entityId);
            recoveringEntityIds.Remove(entityId);
            snapshotSequences.Remove(entityId);
            foreach (var baselines in serverSnapshotBaselines.Values)
            {
                baselines.Remove(entityId);
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

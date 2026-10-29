using System;
using System.Collections.Generic;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.Transport;

namespace AFPS.NetCode.Sessions
{
    /// <summary>
    /// 服务器进程内的有界会话保留区。只负责保存/过期/迁移，不提供网络身份认证，也不保存对局级生命与分数。
    /// 管理器完成世界准入与恢复凭据校验后才能调用TryRestore。
    /// </summary>
    public sealed class RetainedServerSessionStore
    {
        private readonly struct Entry
        {
            public readonly ServerAuthoritativePlayerSession Session;
            public readonly uint SuspendedTick;

            public Entry(ServerAuthoritativePlayerSession session, uint suspendedTick)
            {
                Session = session;
                SuspendedTick = suspendedTick;
            }
        }

        private readonly Dictionary<uint, Entry> entries = new Dictionary<uint, Entry>();
        private readonly List<uint> expired = new List<uint>();
        private readonly int capacity;
        private readonly uint retentionTicks;
        private bool hasClock;
        private uint lastTick;
        public int Count => entries.Count;

        /// <param name="capacity">最多保留的离线会话数，[1,1024]，满时拒绝新条目，不驱逐其他玩家。</param>
        /// <param name="retentionTicks">有效区间为[挂起Tick,挂起Tick+此值)，[1,2^31-1]；默认1500Tick在50Hz下为30秒。</param>
        public RetainedServerSessionStore(int capacity = 32, uint retentionTicks = 1500)
        {
            if (capacity < 1 || capacity > 1024)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }
            if (retentionTicks == 0 || retentionTicks > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(retentionTicks));
            }
            this.capacity = capacity;
            this.retentionTicks = retentionTicks;
        }

        public bool TryRetain(ServerAuthoritativePlayerSession session, uint serverWorldTick)
        {
            if (session == null || session.EntityId == 0 || session.IsRetainedOffline)
            {
                return false;
            }
            Expire(serverWorldTick);
            if (entries.Count >= capacity || entries.ContainsKey(session.EntityId))
            {
                // 重复断线事件不能重置保留起点，让会话无限续期。
                return false;
            }
            entries.Add(session.EntityId, new Entry(session, serverWorldTick));
            session.SuspendForRecovery();
            return true;
        }

        /// <param name="authenticatedEntityId">协议层已完成凭据校验及世界准入的实体ID，不能直接使用客户端自报ID。</param>
        public bool TryRestore(uint authenticatedEntityId, uint serverWorldTick, IGameTransport transport,
            TransportConnectionId connection, out ServerAuthoritativePlayerSession session)
        {
            session = null;
            Expire(serverWorldTick);
            if (!entries.TryGetValue(authenticatedEntityId, out var entry)
                || !entry.Session.TryRebindConnection(transport, connection))
            {
                return false;
            }
            entries.Remove(authenticatedEntityId);
            session = entry.Session;
            return true;
        }

        public int Expire(uint serverWorldTick, ICollection<uint> expiredEntityIds = null)
        {
            // 按半个uint域解释连续世界时钟；调用方须定期维护，不能跨越半个域后才访问。
            if (hasClock && serverWorldTick != lastTick && !SequenceMath.IsNewer(serverWorldTick, lastTick))
            {
                throw new ArgumentOutOfRangeException(nameof(serverWorldTick), "会话保留区的世界时钟不能回退。");
            }
            hasClock = true;
            lastTick = serverWorldTick;
            expired.Clear();
            foreach (var pair in entries)
            {
                if (unchecked(serverWorldTick - pair.Value.SuspendedTick) >= retentionTicks)
                {
                    expired.Add(pair.Key);
                }
            }
            foreach (uint entityId in expired)
            {
                entries.Remove(entityId);
                expiredEntityIds?.Add(entityId);
            }
            return expired.Count;
        }

        public void Clear()
        {
            entries.Clear();
            expired.Clear();
            hasClock = false;
            lastTick = 0;
        }
    }
}

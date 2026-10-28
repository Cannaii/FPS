using System;
using System.Collections.Generic;

namespace AFPS.NetCode.Sessions
{
    /// <summary>
    /// 有界的服务器恢复身份索引。只验证持有凭据，不负责准入、离线期限或消费会话。
    /// 调度层须先核对世界指纹，再确认实体已离线且仍在RetainedServerSessionStore有效期内。
    /// </summary>
    public sealed class ServerSessionCredentialStore
    {
        private readonly Dictionary<uint, SessionResumeCredential> credentials
            = new Dictionary<uint, SessionResumeCredential>();
        private readonly int capacity;

        public int Count => credentials.Count;

        /// <param name="capacity">活动与离线玩家共用容量，[1,64]，与对局状态协议人数上限一致。</param>
        public ServerSessionCredentialStore(int capacity = 64)
        {
            if (capacity < 1 || capacity > 64)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }
            this.capacity = capacity;
        }

        public bool TryIssue(uint entityId, out SessionResumeCredential credential)
        {
            credential = default;
            if (entityId == 0 || credentials.Count >= capacity || credentials.ContainsKey(entityId))
            {
                // 重复分配不能静默轮换凭据，否则已可靠入队的旧凭据会立即失效。
                return false;
            }

            for (int attempt = 0; attempt < 4; attempt++)
            {
                SessionResumeCredential candidate = SessionResumeCredential.Create();
                if (!TryResolve(candidate, out _))
                {
                    credentials.Add(entityId, candidate);
                    credential = candidate;
                    return true;
                }
            }
            // 随机源异常不能覆盖另一个玩家的凭据；调用方应中止本次会话创建。
            throw new System.Security.Cryptography.CryptographicException("会话恢复凭据发生重复。");
        }

        public bool TryResolve(SessionResumeCredential credential, out uint entityId)
        {
            entityId = 0;
            if (!credential.IsValid)
            {
                return false;
            }

            // 容量最多64；比较全部条目后再返回，不因命中在字典中的位置提前结束遍历。
            foreach (KeyValuePair<uint, SessionResumeCredential> pair in credentials)
            {
                if (pair.Value.Equals(credential))
                {
                    entityId = pair.Key;
                }
            }
            return entityId != 0;
        }

        /// <summary>仅供服务器向已准入的所属连接重发Grant；不能把任意客户端自报ID用于此查询。</summary>
        public bool TryGetIssued(uint entityId, out SessionResumeCredential credential)
        {
            return credentials.TryGetValue(entityId, out credential);
        }

        /// <summary>到期/离场时撤销；普通连接迁移不撤销，响应重试需要沿用同一凭据。</summary>
        public bool Revoke(uint entityId) => credentials.Remove(entityId);

        /// <summary>服务器停机或更换对局时失效全部凭据，禁止跨对局复用。</summary>
        public void Clear() => credentials.Clear();
    }
}

using System;
using AFPS.NetCode.Sessions;
using AFPS.NetCode.Transport;

namespace AFPS.NetCode.Runtime
{
    public enum ClientReconnectStatus
    {
        Idle,
        Waiting,
        Connecting,
        Recovering,
        Failed,
        Cancelled
    }

    /// <summary>基于单调实时时钟的有界重拨；不重建管理器，不把恢复失败静默降级成新玩家。</summary>
    public sealed class ClientReconnectController
    {
        // 秒；总窗口小于服务器50Hz下30秒租约。单次含传输连接/准入的预算为5秒。
        private const double TotalTimeoutSeconds = 25;
        private const double AttemptTimeoutSeconds = 5;
        private const double RetryDelaySeconds = 0.5;
        private readonly GameNetworkRuntime runtime;
        private readonly NetworkMovementSessionManager manager;
        private TransportConnectionId connection;
        private double startedAt;
        private double nextAttemptAt;
        private double attemptStartedAt;
        private double lastTime;
        private bool hasTime;
        private bool freshJoin;

        public ClientReconnectStatus Status { get; private set; }
        public int AttemptCount { get; private set; }
        public uint CompletedRecoveryCount { get; private set; }
        public string Error { get; private set; }
        public bool IsActive => Status == ClientReconnectStatus.Waiting
            || Status == ClientReconnectStatus.Connecting || Status == ClientReconnectStatus.Recovering;
        public string Message => Status switch
        {
            ClientReconnectStatus.Waiting => "连接中断，正在等待重连…原角色仍可能受到伤害。",
            ClientReconnectStatus.Connecting => $"正在重新连接服务器（第 {AttemptCount} 次）…",
            ClientReconnectStatus.Recovering => manager.PendingRecoveryInputCount > 0
                ? $"正在同步原角色状态，待补输入 {manager.PendingRecoveryInputCount} 条…"
                : "正在确认原角色状态…",
            ClientReconnectStatus.Failed => Error,
            ClientReconnectStatus.Cancelled => "已取消自动重连。可重试原会话，或作为新玩家加入。",
            _ => string.Empty
        };

        public ClientReconnectController(GameNetworkRuntime runtime, NetworkMovementSessionManager manager)
        {
            this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
        }

        public void NotifyConnected(TransportConnectionId id)
        {
            connection = id;
        }

        public void NotifyDisconnected(TransportConnectionId id, double now)
        {
            CheckTime(now);
            if (connection.IsValid && id != connection)
            {
                return;
            }
            connection = default;
            if (Status == ClientReconnectStatus.Cancelled || Status == ClientReconnectStatus.Failed)
            {
                return;
            }
            if (IsMismatch())
            {
                Fail("无法恢复：服务器的地图、模拟算法或玩法配置与本机不一致。");
                return;
            }
            if (!manager.ClientResumeCredential.IsValid || freshJoin)
            {
                Fail("连接未建立或恢复凭据不可用，可选择作为新玩家重新加入。");
                return;
            }
            if (!IsActive)
            {
                Begin(now, false);
            }
            Status = ClientReconnectStatus.Waiting;
            nextAttemptAt = now + RetryDelaySeconds;
        }

        public void Advance(double now)
        {
            CheckTime(now);
            if (!IsActive)
            {
                return;
            }
            if (manager.CanPredictClientInput && manager.RemotePlayers.LocalEntityId != 0
                && manager.ClientResumeCredential.IsValid)
            {
                if (!freshJoin)
                {
                    CompletedRecoveryCount++;
                }
                Status = ClientReconnectStatus.Idle;
                Error = null;
                freshJoin = false;
                return;
            }
            if (now - startedAt >= TotalTimeoutSeconds)
            {
                Fail("重连超时。原会话可能已到期或服务器已重启，可重试或作为新玩家加入。");
                return;
            }
            if (connection.IsValid && IsMismatch())
            {
                Fail("无法恢复：服务器的地图、模拟算法或玩法配置与本机不一致。");
                return;
            }
            if (Status == ClientReconnectStatus.Waiting)
            {
                if (now < nextAttemptAt)
                {
                    return;
                }
                AttemptCount++;
                attemptStartedAt = now;
                if (runtime.TryRestartClient(out string error))
                {
                    Status = ClientReconnectStatus.Connecting;
                }
                else
                {
                    Error = error;
                    nextAttemptAt = now + RetryDelaySeconds;
                }
                return;
            }
            if (now - attemptStartedAt >= AttemptTimeoutSeconds)
            {
                CloseClient();
                Status = ClientReconnectStatus.Waiting;
                nextAttemptAt = now + RetryDelaySeconds;
                return;
            }
            if (manager.ClientRecoveryPending)
            {
                Status = ClientReconnectStatus.Recovering;
            }
        }

        public void Retry(double now)
        {
            CheckTime(now);
            CloseClient();
            if (!manager.ClientResumeCredential.IsValid)
            {
                Fail("没有可恢复的会话，请选择作为新玩家加入。");
                return;
            }
            Begin(now, false);
        }

        public void JoinAsNewPlayer(double now)
        {
            CheckTime(now);
            CloseClient();
            manager.ForgetClientRecovery();
            Begin(now, true);
        }

        public void Cancel()
        {
            Status = ClientReconnectStatus.Cancelled;
            CloseClient();
        }

        /// <summary>UTP不为本地主动Disconnect生成回调，调试中断也要显式撤销客户端会话。</summary>
        public void SimulateDisconnect(double now)
        {
            CheckTime(now);
            var id = connection;
            CloseClient();
            NotifyDisconnected(id, now);
        }

        private void Begin(double now, bool asNewPlayer)
        {
            startedAt = now;
            nextAttemptAt = now + RetryDelaySeconds;
            AttemptCount = 0;
            Error = null;
            freshJoin = asNewPlayer;
            Status = ClientReconnectStatus.Waiting;
        }

        private bool IsMismatch()
        {
            var admission = manager.ClientAdmissionStatus;
            return admission == ConnectionAdmissionStatus.AlgorithmMismatch
                || admission == ConnectionAdmissionStatus.WorldMismatch
                || admission == ConnectionAdmissionStatus.ConfigurationMismatch;
        }

        private void Fail(string message)
        {
            Status = ClientReconnectStatus.Failed;
            Error = message;
            CloseClient();
        }

        private void CloseClient()
        {
            if (connection.IsValid)
            {
                runtime.ClientTransport?.Disconnect(connection);
                runtime.ClientTransport?.Pump();
                manager.HandleDisconnected(NetworkTransportSide.Client, connection);
                connection = default;
            }
            runtime.ClientTransport?.Stop();
        }

        private void CheckTime(double now)
        {
            if (double.IsNaN(now) || double.IsInfinity(now) || now < 0 || (hasTime && now < lastTime))
            {
                throw new ArgumentOutOfRangeException(nameof(now), "重连时钟须为非负单调实秒。");
            }
            hasTime = true;
            lastTime = now;
        }
    }
}

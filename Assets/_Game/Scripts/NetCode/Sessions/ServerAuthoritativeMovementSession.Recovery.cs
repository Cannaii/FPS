using AFPS.NetCode.Messages;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.StateReplication;
using AFPS.NetCode.Transport;
using AFPS.NetCode.InputReplication;
using AFPS.Simulation.Characters;

namespace AFPS.NetCode.Sessions
{
    public sealed partial class ServerAuthoritativeMovementSession
    {
        public uint EntityId => entityId;
        public uint NextExpectedInputTick => inputReceiver.NextExpectedTick;
        public uint LastObservedShotSequence => hasObservedGlobalShot ? lastGlobalShotSequence : 0;
        public bool RecoverySnapshotPending { get; private set; }
        public bool IsRetainedOffline { get; private set; }

        internal void SuspendForRecovery()
        {
            IsRetainedOffline = true;
        }

        /// <summary>
        /// 服务器内部迁移入口。调用者必须先完成恢复身份认证和世界准入，不能直接信任网络传来的实体ID。
        /// 不重建玩家、武器或计时器；只更换输出连接并丢弃旧输入窗口，等待客户端补发未确认输入。
        /// </summary>
        public bool TryRebindConnection(IGameTransport transport, TransportConnectionId connection)
        {
            if (transport == null || !transport.IsRunning || transport.Role != TransportRole.Server || !connection.IsValid)
            {
                return false;
            }

            // 先完成校验和分配再提交引用；失败不能留下移动/武器分别指向不同连接的半迁移状态。
            var receiver = new ServerInputCommandReceiver(unchecked(CurrentState.Tick + 1), inputReceiver.WindowCapacity);
            var sender = new ServerAuthoritativeStateSender(transport, connection);
            inputReceiver = receiver;
            stateSender = sender;
            weaponTransport = transport;
            weaponConnection = connection;
            hasLastReceivedInput = false;
            lastReceivedInput = default;
            missingInputWaitTicks = 0;
            consecutiveSubstitutedInputTicks = 0;
            pendingRejections.Clear();
            LastWeaponFireResult = default;
            hasSentWeaponState = false;
            IsRetainedOffline = false;
            RecoverySnapshotPending = true;
            // 全局/分武器射击序号、弹匣、换弹/切枪起始Tick均保留，不能靠重连刷资源或重放旧射击。
            return true;
        }

        /// <summary>
        /// 可靠发送当前移动和活动武器种子；成功只表示全部入队，客户端收到/确认的追帧门禁由后续恢复协议管理。
        /// serverWorldTick使用仍在运行的世界时钟；允许同Tick重试，不允许回退。
        /// </summary>
        /// <param name="result">仅记录移动包的发送结果；两份种子是否都入队必须看方法返回值。</param>
        public bool TrySendRecoverySnapshot(uint serverWorldTick, out AuthoritativeStateSendResult result)
        {
            result = new AuthoritativeStateSendResult(TransportSendResult.TransportError,
                0, serverWorldTick, CurrentState.Tick, 0);
            if (IsRetainedOffline || !RecoverySnapshotPending)
            {
                return false;
            }
            if (hasAdvancedServerTick && serverWorldTick != lastServerTick
                && !SequenceMath.IsNewer(serverWorldTick, lastServerTick))
            {
                return false;
            }
            hasAdvancedServerTick = true;
            lastServerTick = serverWorldTick;
            var state = new AuthoritativePlayerState(serverWorldTick, CurrentState.Tick, CurrentState);
            if (!stateSender.TrySend(state, out result, TransportDelivery.ReliableSequenced)
                || !SendWeaponState(serverWorldTick, new PlayerInputCommand { Tick = CurrentState.Tick }, force: true))
            {
                // 可能已有移动种子入队；保持门禁，下一次可靠重发两份状态，不推进任何玩法状态。
                return false;
            }
            RecoverySnapshotPending = false;
            return true;
        }
    }
}

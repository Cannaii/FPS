using System;
using System.Collections.Generic;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Protocol;
using AFPS.Simulation.Characters;

namespace AFPS.NetCode.Sessions
{
    public sealed partial class NetworkMovementSessionManager
    {
        private List<PlayerInputCommand> recoveryInputs = new List<PlayerInputCommand>();
        private bool clientRecoveryConfirmed;
        private uint clientRecoveryEpoch;
        private uint clientResumeEntityId;
        private uint lastRecoveryAckTick;
        private bool hasRecoveryAck;
        private uint recoverySeedWorldTick;
        private ushort capturedDeaths;
        private bool hasCapturedLife;

        public uint ClientShotSequenceFloor { get; private set; }
        public bool ClientRecoveryPending => admission != null && admission.ClientIsResuming
            && (!clientRecoveryConfirmed || recoveryInputs.Count != 0);
        public bool CanPredictClientInput => ClientSession != null && !ClientRecoveryPending;
        public int PendingRecoveryInputCount => recoveryInputs.Count;

        /// <summary>仅显式选择新加入时清除旧身份；自动重试绝不能调用此方法。</summary>
        public void ForgetClientRecovery()
        {
            if (ClientSession != null)
            {
                throw new InvalidOperationException("清除恢复身份前必须先断开客户端会话。");
            }
            ClientResumeCredential = default;
            clientResumeEntityId = 0;
            ClientShotSequenceFloor = 0;
            clientRecoveryEpoch = 0;
            clientRecoveryConfirmed = false;
            recoveryInputs.Clear();
            hasCapturedLife = false;
        }

        private void CaptureClientRecoveryInputs()
        {
            if (ClientSession == null)
            {
                return;
            }
            // 恢复未完成再次断线时保留原缓存，不用尚未接收完整种子的空会话覆盖历史。
            if (!ClientRecoveryPending)
            {
                recoveryInputs = ClientSession.CaptureRecoveryInputs();
                hasCapturedLife = ClientDeathmatchState != null
                    && ClientDeathmatchState.TryGetPlayer(clientResumeEntityId, out _);
                if (hasCapturedLife)
                {
                    ClientDeathmatchState.TryGetPlayer(clientResumeEntityId, out var player);
                    capturedDeaths = player.Deaths;
                }
            }
            foreach (var command in recoveryInputs)
            {
                ObserveRecoveryShotSequence(command.ShotSequence);
            }
        }

        private void ObserveRecoveryShotSequence(uint sequence)
        {
            if (sequence != 0 && (ClientShotSequenceFloor == 0 || SequenceMath.IsNewer(sequence, ClientShotSequenceFloor)))
            {
                ClientShotSequenceFloor = sequence;
            }
        }

        private bool ReceiveRecoverySeed(ArraySegment<byte> packet)
        {
            if (admission == null || !admission.ClientIsResuming
                || !SessionRecoveryCodec.TryReadSeed(packet, out uint epoch, out var seed)
                || seed.Attempt != admission.ClientAttempt || seed.EntityId != clientResumeEntityId
                || RemotePlayers.LocalEntityId != seed.EntityId || !IsCompatibleWeaponState(seed.Weapon))
            {
                return false;
            }
            if (clientRecoveryEpoch != 0 && !SequenceMath.IsNewer(epoch, clientRecoveryEpoch))
            {
                // 重复种子不再次重建会话；ACK仍按本地Tick定期重试，避免恶意重复包触发发送风暴。
                return epoch == clientRecoveryEpoch;
            }
            ClientSession = new ClientPredictedMovementSession(clientTransport, clientConnectionId,
                seed.Movement.State, simulationConfig, tickDeltaTime, predictionHistoryCapacity,
                inputRedundancyCount, positionErrorThreshold, velocityErrorThreshold,
                collisionWorld, WorldContext.CollisionWorld);
            ClientWeapon.Clear();
            ClientWeapon.Apply(seed.Weapon);
            hasWeaponStateSequence = false;
            ObserveRecoveryShotSequence(seed.LastShotSequence);
            uint expected = unchecked(seed.Movement.LastProcessedInputTick + 1);
            recoveryInputs.RemoveAll(command => !SequenceMath.IsNewer(command.Tick, seed.Movement.LastProcessedInputTick));
            if (recoveryInputs.Count > seed.ReplayWindow
                || (recoveryInputs.Count != 0 && recoveryInputs[0].Tick != expected))
            {
                // 历史窗口已覆盖：硬对齐可信种子，不跨缺口补造输入。
                recoveryInputs.Clear();
            }
            clientRecoveryEpoch = epoch;
            recoverySeedWorldTick = seed.Movement.ServerTick;
            clientRecoveryConfirmed = false;
            hasRecoveryAck = false;
            return true;
        }

        private bool ReceiveRecoveryConfirmed(ArraySegment<byte> packet)
        {
            if (admission == null || !admission.ClientIsResuming || clientRecoveryEpoch == 0
                || !SessionRecoveryCodec.TryReadReceipt(packet, NetworkMessageType.SessionRecoveryConfirmed,
                    out uint attempt, out uint epoch, out uint entityId)
                || attempt != admission.ClientAttempt || epoch != clientRecoveryEpoch || entityId != clientResumeEntityId)
            {
                return false;
            }
            clientRecoveryConfirmed = true;
            return true;
        }

        private void AdvanceClientRecovery()
        {
            if (admission == null || !admission.ClientIsResuming || clientRecoveryEpoch == 0 || ClientSession == null)
            {
                return;
            }
            if (!clientRecoveryConfirmed)
            {
                // 对局状态也必须到达后才确认，避免恢复已死亡/已终局的生命时先采集动作。
                if (ClientDeathmatchState != null
                    && !SequenceMath.IsOlder(ClientDeathmatchState.ServerTick, recoverySeedWorldTick)
                    && ClientDeathmatchState.TryGetPlayer(clientResumeEntityId, out var player)
                    && (!hasRecoveryAck || unchecked(latestServerWorldTick - lastRecoveryAckTick) >= 25))
                {
                    if (!hasCapturedLife || player.Deaths != capturedDeaths || !player.IsAlive
                        || ClientDeathmatchState.Phase != AFPS.NetCode.Match.DeathmatchPhase.Playing)
                    {
                        // 不把上一条生命或终局前的缓存动作补到新生命；身份恢复不意味着动作仍然有效。
                        recoveryInputs.Clear();
                    }
                    SendRecoveryReceipt(clientTransport, clientConnectionId, NetworkMessageType.SessionRecoveryAck,
                        admission.ClientAttempt, clientRecoveryEpoch, clientResumeEntityId);
                    hasRecoveryAck = true;
                    lastRecoveryAckTick = latestServerWorldTick;
                }
                return;
            }
            // 总缓存最多32条，每个本地世界Tick最多补4条；追帧期间停止采集新输入和本地开火表现。
            for (int count = 0; count < 4 && recoveryInputs.Count != 0; count++)
            {
                var command = recoveryInputs[0];
                if (SequenceMath.IsNewer(command.Tick, ClientSession.CurrentState.Tick))
                {
                    if (command.Tick != unchecked(ClientSession.CurrentState.Tick + 1))
                    {
                        recoveryInputs.Clear();
                        break;
                    }
                    ClientSession.PredictAndSend(command, out _);
                    ClientWeapon.Observe(command);
                }
                recoveryInputs.RemoveAt(0);
            }
        }
    }
}

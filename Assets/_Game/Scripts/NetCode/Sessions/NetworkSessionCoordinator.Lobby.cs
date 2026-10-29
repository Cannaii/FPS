using System;
using AFPS.NetCode.Match;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.Runtime;
using AFPS.NetCode.Transport;

namespace AFPS.NetCode.Sessions
{
    public sealed partial class NetworkSessionCoordinator
    {
        private readonly LobbyStartGate lobby;
        private readonly byte[] lobbySnapshotBuffer = new byte[LobbyMessageCodec.MaximumSnapshotPacketSize];
        private readonly byte[] lobbyReadyBuffer = new byte[LobbyMessageCodec.ReadyPacketSize];
        private uint lobbySnapshotSequence = 1;
        private uint lastLobbySendTick;
        private bool lobbyDirty = true;
        private uint lastReceivedLobbySequence;
        private bool hasLobbySequence;
        private uint nextReadySequence = 1;
        private LobbyReadyCommand pendingReady;
        private bool hasPendingReady;
        private bool hasSentReady;
        private uint lastReadySendTick;

        public LobbySnapshot ClientLobby { get; private set; }
        public bool LobbyReadyPending => hasPendingReady;
        public bool SpawnSelectionDeclined { get; private set; }
        public bool CanEnterMatch => IsLocalPractice || ClientLobby?.Phase == LobbyPhase.Playing;
        public uint LobbyCountdownSeconds => ClientLobby?.Phase == LobbyPhase.Countdown
            ? (ClientLobby.RemainingTicks + (uint)serverTickRate - 1) / (uint)serverTickRate : 0;

        public bool TryGetLocalLobbyPlayer(out LobbyPlayerState player)
        {
            player = default;
            uint id = RemotePlayers?.LocalEntityId ?? 0;
            if (ClientLobby == null || id == 0)
            {
                return false;
            }
            for (int index = 0; index < ClientLobby.PlayerCount; index++)
            {
                if (ClientLobby.GetPlayer(index).EntityId == id)
                {
                    player = ClientLobby.GetPlayer(index);
                    return true;
                }
            }
            return false;
        }

        public bool TrySetLobbyReady(bool ready)
        {
            return TrySetLobbyIntent(ready, 0);
        }

        public bool TrySelectSpawnSlot(byte slot)
        {
            return slot >= 1 && slot <= LobbyStartGate.MaximumPlayers
                && TryGetLocalLobbyPlayer(out var player) && !player.Ready && !hasPendingReady
                && IsClientSpawnSlotAvailable(slot)
                && sessionSpatial.TryGetSpawnPosition(slot, out _)
                && TrySetLobbyIntent(false, slot);
        }

        public bool IsClientSpawnSlotAvailable(byte slot)
        {
            if (ClientLobby == null || slot == 0 || slot > LobbyStartGate.MaximumPlayers)
            {
                return false;
            }
            uint local = RemotePlayers?.LocalEntityId ?? 0;
            for (int index = 0; index < ClientLobby.PlayerCount; index++)
            {
                var player = ClientLobby.GetPlayer(index);
                if (player.EntityId != local && player.SpawnSlot == slot)
                {
                    return false;
                }
            }
            return IsSupportedSpawnSlot(slot);
        }

        private bool IsSupportedSpawnSlot(byte slot)
        {
            if (!sessionSpatial.TryGetSpawnPosition(slot, out var position))
            {
                return false;
            }
            if (WorldContext == null)
            {
                // 旧测试/显式碰撞适配器没有世界查询；正式游戏均装配不可变FixedWorld。
                return true;
            }
            var collision = simulationConfig.Collision;
            var halfHeight = collision.HeightFixed / AFPS.Math.Fix.FromInt(2);
            var center = position + AFPS.Math.FixVec3.Up * halfHeight;
            // 不能让出生线编号把玩家送到地图外；只接受当前世界在探地距离内可支撑的位置。
            return WorldContext.CollisionWorld.GroundCheck(center, collision.RadiusFixed,
                halfHeight, collision.GroundProbeDistanceFixed).DidHit;
        }

        public bool TryGetSpawnPosition(byte slot, out AFPS.Math.FixVec3 position)
        {
            return sessionSpatial.TryGetSpawnPosition(slot, out position);
        }

        private bool TrySetLobbyIntent(bool ready, byte slot)
        {
            if (IsLocalPractice || !CanPredictClientInput || ClientAdmissionStatus != ConnectionAdmissionStatus.Admitted
                || !TryGetLocalLobbyPlayer(out var player) || !player.Connected || player.Recovering
                || (ClientLobby.Phase != LobbyPhase.Waiting && ClientLobby.Phase != LobbyPhase.Countdown))
            {
                return false;
            }
            if (hasPendingReady && pendingReady.Ready == ready && pendingReady.SpawnSlot == slot)
            {
                return true;
            }
            pendingReady = new LobbyReadyCommand(
                ClientLobby.RoundEpoch, player.MembershipEpoch, nextReadySequence, ready, slot);
            nextReadySequence = unchecked(nextReadySequence + 1);
            if (nextReadySequence == 0)
            {
                nextReadySequence = 1;
            }
            hasPendingReady = true;
            SpawnSelectionDeclined = false;
            hasSentReady = false;
            FlushLobbyReady();
            return true;
        }

        private void ResetClientLobby()
        {
            ClientLobby = null;
            hasLobbySequence = false;
            hasPendingReady = false;
            hasSentReady = false;
            nextReadySequence = 1;
            SpawnSelectionDeclined = false;
        }

        private void FlushLobbyReady()
        {
            if (!hasPendingReady || ClientSession == null || ClientRecoveryPending
                || (hasSentReady && unchecked(latestServerWorldTick - lastReadySendTick) < deathmatchReplicationIntervalTicks))
            {
                return;
            }
            if (LobbyMessageCodec.TryWriteReady(pendingReady, new ArraySegment<byte>(lobbyReadyBuffer))
                && clientTransport.Send(clientConnectionId, TransportDelivery.ReliableSequenced,
                    new ArraySegment<byte>(lobbyReadyBuffer)) == TransportSendResult.Success)
            {
                hasSentReady = true;
                lastReadySendTick = latestServerWorldTick;
            }
        }

        private bool ReceiveLobbyReady(TransportConnectionId connection, ArraySegment<byte> packet)
        {
            if (IsLocalPractice || recoveringConnections.ContainsKey(connection)
                || !serverEntityIds.TryGetValue(connection, out uint entity)
                || !LobbyMessageCodec.TryReadReady(packet, out var command)
                || (command.SpawnSlot != 0 && !sessionSpatial.TryGetSpawnPosition(command.SpawnSlot, out _)))
            {
                return false;
            }
            var result = lobby.Apply(entity, command,
                command.SpawnSlot == 0 || IsSupportedSpawnSlot(command.SpawnSlot));
            if (result == LobbyCommandResult.Rejected)
            {
                return false;
            }
            lobbyDirty = true;
            if (result == LobbyCommandResult.Applied && command.SpawnSlot != 0
                && serverSessions.TryGetValue(connection, out var session))
            {
                // 只在未准备时允许改点；权威传送保留输入Tick，普通状态复制负责校正客户端。
                session.Teleport(sessionSpatial.GetSpawnPosition(command.SpawnSlot));
            }
            return true;
        }

        private bool ReceiveLobbySnapshot(ArraySegment<byte> packet)
        {
            if (!LobbyMessageCodec.TryReadSnapshot(packet, out uint sequence, out var snapshot)
                || (hasLobbySequence && !SequenceMath.IsNewer(sequence, lastReceivedLobbySequence)))
            {
                return false;
            }
            uint local = RemotePlayers?.LocalEntityId ?? 0;
            bool containsLocal = false;
            for (int index = 0; index < snapshot.PlayerCount; index++)
            {
                containsLocal |= snapshot.GetPlayer(index).EntityId == local;
            }
            if (!containsLocal || local == 0)
            {
                return false;
            }
            ClientLobby = snapshot;
            hasLobbySequence = true;
            lastReceivedLobbySequence = sequence;
            if (hasPendingReady && TryGetLocalLobbyPlayer(out var player))
            {
                // 同值旧快照不能确认新意图；世代、权威确认序号和Ready必须同时匹配。
                bool reset = pendingReady.RoundEpoch != snapshot.RoundEpoch
                    || pendingReady.MembershipEpoch != player.MembershipEpoch
                    || !player.Connected || player.Recovering
                    || snapshot.Phase == LobbyPhase.Playing || snapshot.Phase == LobbyPhase.Finished;
                // 新序号已被处理但点位被占用时也结束等待，由菜单保留权威点位供玩家重选。
                bool superseded = player.ReadySequence == pendingReady.Sequence
                    || SequenceMath.IsNewer(player.ReadySequence, pendingReady.Sequence);
                if (!reset && superseded && pendingReady.SpawnSlot != 0)
                {
                    SpawnSelectionDeclined = player.SpawnSlot != pendingReady.SpawnSlot;
                }
                if (reset || superseded)
                {
                    hasPendingReady = false;
                }
            }
            return true;
        }

        private void RegisterLobbyMember(uint entity, bool recovering = false)
        {
            if (!IsLocalPractice)
            {
                lobby.Connect(entity, recovering);
                lobbyDirty = true;
            }
        }

        private void ReplicateLobby(uint tick)
        {
            if (IsLocalPractice || serverTransport == null || serverSessions.Count == 0
                || (!lobbyDirty && unchecked(tick - lastLobbySendTick) < deathmatchReplicationIntervalTicks))
            {
                return;
            }
            if (!LobbyMessageCodec.TryWriteSnapshot(lobby.Snapshot(), lobbySnapshotSequence,
                new ArraySegment<byte>(lobbySnapshotBuffer), out int written))
            {
                return;
            }
            lobbySnapshotSequence = unchecked(lobbySnapshotSequence + 1);
            if (lobbySnapshotSequence == 0)
            {
                lobbySnapshotSequence = 1;
            }
            bool failed = false;
            foreach (var connection in serverSessions.Keys)
            {
                failed |= serverTransport.Send(connection, TransportDelivery.ReliableSequenced,
                    new ArraySegment<byte>(lobbySnapshotBuffer, 0, written)) != TransportSendResult.Success;
            }
            lobbyDirty = failed;
            lastLobbySendTick = tick;
        }
    }
}

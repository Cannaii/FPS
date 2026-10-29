using System;
using AFPS.NetCode.Match;
using AFPS.NetCode.Serialization;

namespace AFPS.NetCode.Messages
{
    /// <summary>大厅负载规范；尚未分配线上消息类型，不把这些字节当成已接入的游戏数据包。</summary>
    public static class LobbyPayloadCodec
    {
        public const int ReadyPayloadSize = 13;
        public const int SnapshotFixedSize = 14;
        public const int PlayerSize = 13;
        public const int MaximumSnapshotSize = SnapshotFixedSize + LobbyStartGate.MaximumPlayers * PlayerSize;

        public static bool TryWriteReady(in LobbyReadyCommand command, ArraySegment<byte> destination)
        {
            if (command.RoundEpoch == 0 || command.MembershipEpoch == 0 || command.Sequence == 0
                || destination.Array == null || destination.Count < ReadyPayloadSize)
            {
                return false;
            }
            var writer = new PacketBufferWriter(destination);
            return writer.TryWriteUInt32(command.RoundEpoch) && writer.TryWriteUInt32(command.MembershipEpoch)
                && writer.TryWriteUInt32(command.Sequence) && writer.TryWriteByte(command.Ready ? (byte)1 : (byte)0);
        }

        public static bool TryReadReady(ArraySegment<byte> payload, out LobbyReadyCommand command)
        {
            command = default;
            if (payload.Array == null || payload.Count != ReadyPayloadSize)
            {
                return false;
            }
            var reader = new PacketBufferReader(payload);
            if (!reader.TryReadUInt32(out uint round) || round == 0
                || !reader.TryReadUInt32(out uint membership) || membership == 0
                || !reader.TryReadUInt32(out uint sequence) || sequence == 0
                || !reader.TryReadByte(out byte ready) || ready > 1)
            {
                return false;
            }
            command = new LobbyReadyCommand(round, membership, sequence, ready == 1);
            return true;
        }

        public static bool TryWriteSnapshot(LobbySnapshot snapshot, ArraySegment<byte> destination, out int written)
        {
            written = 0;
            if (!ValidSnapshot(snapshot) || destination.Array == null)
            {
                return false;
            }
            int size = SnapshotFixedSize + snapshot.PlayerCount * PlayerSize;
            if (destination.Count < size)
            {
                return false;
            }
            var writer = new PacketBufferWriter(destination);
            if (!writer.TryWriteUInt32(snapshot.RoundEpoch) || !writer.TryWriteUInt32(snapshot.ServerTick)
                || !writer.TryWriteUInt32(snapshot.RemainingTicks) || !writer.TryWriteByte((byte)snapshot.Phase)
                || !writer.TryWriteByte((byte)snapshot.PlayerCount))
            {
                return false;
            }
            for (int index = 0; index < snapshot.PlayerCount; index++)
            {
                var player = snapshot.GetPlayer(index);
                byte flags = (byte)((player.Connected ? 1 : 0) | (player.Recovering ? 2 : 0) | (player.Ready ? 4 : 0));
                if (!writer.TryWriteUInt32(player.EntityId) || !writer.TryWriteUInt32(player.MembershipEpoch)
                    || !writer.TryWriteUInt32(player.ReadySequence)
                    || !writer.TryWriteByte(flags))
                {
                    return false;
                }
            }
            written = size;
            return true;
        }

        public static bool TryReadSnapshot(ArraySegment<byte> payload, out LobbySnapshot snapshot)
        {
            snapshot = null;
            if (payload.Array == null || payload.Count < SnapshotFixedSize || payload.Count > MaximumSnapshotSize)
            {
                return false;
            }
            var reader = new PacketBufferReader(payload);
            if (!reader.TryReadUInt32(out uint round) || !reader.TryReadUInt32(out uint tick)
                || !reader.TryReadUInt32(out uint remaining) || !reader.TryReadByte(out byte phase)
                || !reader.TryReadByte(out byte count) || count > LobbyStartGate.MaximumPlayers
                || payload.Count != SnapshotFixedSize + count * PlayerSize)
            {
                return false;
            }
            var players = new LobbyPlayerState[count];
            for (int index = 0; index < count; index++)
            {
                if (!reader.TryReadUInt32(out uint entity) || !reader.TryReadUInt32(out uint membership)
                    || !reader.TryReadUInt32(out uint readySequence)
                    || !reader.TryReadByte(out byte flags) || flags > 7)
                {
                    return false;
                }
                players[index] = new LobbyPlayerState(entity, membership,
                    (flags & 1) != 0, (flags & 2) != 0, (flags & 4) != 0, readySequence);
            }
            var decoded = new LobbySnapshot(round, tick, remaining, (LobbyPhase)phase, players);
            if (!ValidSnapshot(decoded))
            {
                return false;
            }
            snapshot = decoded;
            return true;
        }

        private static bool ValidSnapshot(LobbySnapshot snapshot)
        {
            if (snapshot == null || snapshot.RoundEpoch == 0 || snapshot.Phase > LobbyPhase.Finished
                || snapshot.PlayerCount > LobbyStartGate.MaximumPlayers)
            {
                return false;
            }
            uint previousEntity = 0;
            int connected = 0;
            bool allReady = true;
            for (int index = 0; index < snapshot.PlayerCount; index++)
            {
                var player = snapshot.GetPlayer(index);
                // 严格递增的实体顺序同时排除重复，避免同一成员出现两个互相矛盾的Ready。
                if (!player.IsValid || player.EntityId <= previousEntity
                    || (snapshot.Phase == LobbyPhase.Finished && player.Ready))
                {
                    return false;
                }
                previousEntity = player.EntityId;
                if (player.Connected)
                {
                    connected++;
                    allReady &= player.Ready && !player.Recovering;
                }
            }
            return snapshot.Phase == LobbyPhase.Countdown
                ? snapshot.RemainingTicks > 0 && snapshot.RemainingTicks < 0x80000000U
                    && connected >= LobbyStartGate.MinimumPlayers && allReady
                : snapshot.RemainingTicks == 0;
        }
    }
}

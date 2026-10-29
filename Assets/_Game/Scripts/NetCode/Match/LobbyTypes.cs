using System;

namespace AFPS.NetCode.Match
{
    public enum LobbyPhase : byte
    {
        Waiting = 0,
        Countdown = 1,
        Playing = 2,
        Finished = 3
    }

    public readonly struct LobbyReadyCommand
    {
        public readonly uint RoundEpoch;
        public readonly uint MembershipEpoch;
        public readonly uint Sequence;
        public readonly bool Ready;
        /// <summary>1～64的出生点编号；0表示仅改变准备状态，保留原出生点。</summary>
        public readonly byte SpawnSlot;

        public LobbyReadyCommand(uint roundEpoch, uint membershipEpoch, uint sequence, bool ready, byte spawnSlot = 0)
        {
            RoundEpoch = roundEpoch;
            MembershipEpoch = membershipEpoch;
            Sequence = sequence;
            Ready = ready;
            SpawnSlot = spawnSlot;
        }
    }

    public readonly struct LobbyPlayerState
    {
        public readonly uint EntityId;
        /// <summary>本次连接成员世代；重连更换，防止原连接的迟到准备请求影响新连接。</summary>
        public readonly uint MembershipEpoch;
        /// <summary>权威已接受的准备请求序号；客户端不能仅凭Ready布尔值确认最新意图。</summary>
        public readonly uint ReadySequence;
        public readonly bool Connected;
        public readonly bool Recovering;
        public readonly bool Ready;
        /// <summary>服务器分配的出生点编号，1～64；离线保留期间继续占用。</summary>
        public readonly byte SpawnSlot;

        public LobbyPlayerState(uint entityId, uint membershipEpoch, bool connected, bool recovering,
            bool ready, uint readySequence = 0, byte spawnSlot = 0)
        {
            EntityId = entityId;
            MembershipEpoch = membershipEpoch;
            ReadySequence = readySequence;
            Connected = connected;
            Recovering = recovering;
            Ready = ready;
            SpawnSlot = spawnSlot == 0 ? (byte)(unchecked(entityId - 1) % 64 + 1) : spawnSlot;
        }

        public bool IsValid => EntityId != 0 && MembershipEpoch != 0
            && SpawnSlot >= 1 && SpawnSlot <= LobbyStartGate.MaximumPlayers
            && (!Recovering || Connected) && (!Ready || (Connected && !Recovering && ReadySequence != 0));
    }

    /// <summary>权威大厅快照，按值读取成员；外部数组不能反写状态机。</summary>
    public sealed class LobbySnapshot
    {
        private readonly LobbyPlayerState[] players;
        public readonly uint RoundEpoch;
        public readonly uint ServerTick;
        public readonly uint RemainingTicks;
        public readonly LobbyPhase Phase;
        public int PlayerCount => players.Length;

        public LobbySnapshot(uint roundEpoch, uint serverTick, uint remainingTicks, LobbyPhase phase,
            LobbyPlayerState[] players)
        {
            RoundEpoch = roundEpoch;
            ServerTick = serverTick;
            RemainingTicks = remainingTicks;
            Phase = phase;
            this.players = players != null ? (LobbyPlayerState[])players.Clone() : Array.Empty<LobbyPlayerState>();
        }

        public LobbyPlayerState GetPlayer(int index) => players[index];
    }
}

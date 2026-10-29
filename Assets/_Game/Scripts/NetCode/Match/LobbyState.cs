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

        public LobbyReadyCommand(uint roundEpoch, uint membershipEpoch, uint sequence, bool ready)
        {
            RoundEpoch = roundEpoch;
            MembershipEpoch = membershipEpoch;
            Sequence = sequence;
            Ready = ready;
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

        public LobbyPlayerState(uint entityId, uint membershipEpoch, bool connected, bool recovering,
            bool ready, uint readySequence = 0)
        {
            EntityId = entityId;
            MembershipEpoch = membershipEpoch;
            ReadySequence = readySequence;
            Connected = connected;
            Recovering = recovering;
            Ready = ready;
        }

        public bool IsValid => EntityId != 0 && MembershipEpoch != 0
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

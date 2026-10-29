using System;
using System.Collections.Generic;
using AFPS.NetCode.Protocol;

namespace AFPS.NetCode.Match
{
    public enum LobbyCommandResult
    {
        Rejected,
        Applied,
        Duplicate
    }

    /// <summary>纯权威准备状态机；不读客户端时钟，不推进模拟，不自行重开已结束对局。</summary>
    public sealed class LobbyStartGate
    {
        public const int MinimumPlayers = 2;
        public const int MaximumPlayers = 64;
        private readonly uint countdownTicks;
        private readonly Dictionary<uint, Member> members = new Dictionary<uint, Member>();
        private uint membershipSerial;
        private uint countdownDeadline;
        private uint lastTick;
        private bool hasTick;

        public uint RoundEpoch { get; private set; } = 1;
        public LobbyPhase Phase { get; private set; }
        public uint PlayingStartedTick { get; private set; }

        /// <param name="countdownTicks">服务器世界Tick，范围[1, 2^31)，保证回绕比较有效；不是输入Tick。</param>
        public LobbyStartGate(uint countdownTicks)
        {
            if (countdownTicks == 0 || countdownTicks >= 0x80000000U)
            {
                throw new ArgumentOutOfRangeException(nameof(countdownTicks));
            }
            this.countdownTicks = countdownTicks;
        }

        public bool Connect(uint entityId, bool recovering = false)
        {
            if (entityId == 0 || (members.TryGetValue(entityId, out var existing) && existing.Connected)
                || (!members.ContainsKey(entityId) && members.Count >= MaximumPlayers))
            {
                return false;
            }
            membershipSerial = NextNonzero(membershipSerial);
            members[entityId] = new Member
            {
                Epoch = membershipSerial,
                Connected = true,
                Recovering = recovering
            };
            CancelCountdown();
            return true;
        }

        public bool Disconnect(uint entityId)
        {
            if (!members.TryGetValue(entityId, out var member) || !member.Connected)
            {
                return false;
            }
            member.Connected = false;
            member.Recovering = false;
            member.Ready = false;
            CancelCountdown();
            return true;
        }

        public bool ConfirmRecovery(uint entityId, uint membershipEpoch)
        {
            if (!members.TryGetValue(entityId, out var member) || !member.Connected
                || !member.Recovering || member.Epoch != membershipEpoch)
            {
                return false;
            }
            member.Recovering = false;
            // 恢复成功不恢复旧Ready；等待界面必须重新确认，进行中的对局则继续保持Playing。
            return true;
        }

        public bool Remove(uint entityId)
        {
            if (!members.TryGetValue(entityId, out var member))
            {
                return false;
            }
            members.Remove(entityId);
            if (member.Connected)
            {
                CancelCountdown();
            }
            return true;
        }

        public LobbyCommandResult Apply(uint entityId, in LobbyReadyCommand command)
        {
            if ((Phase != LobbyPhase.Waiting && Phase != LobbyPhase.Countdown)
                || command.RoundEpoch != RoundEpoch || command.Sequence == 0
                || !members.TryGetValue(entityId, out var member) || !member.Connected || member.Recovering
                || command.MembershipEpoch != member.Epoch)
            {
                return LobbyCommandResult.Rejected;
            }
            if (member.HasSequence && command.Sequence == member.Sequence)
            {
                return command.Ready == member.Ready ? LobbyCommandResult.Duplicate : LobbyCommandResult.Rejected;
            }
            if (member.HasSequence && !SequenceMath.IsNewer(command.Sequence, member.Sequence))
            {
                return LobbyCommandResult.Rejected;
            }
            member.Sequence = command.Sequence;
            member.HasSequence = true;
            if (member.Ready != command.Ready)
            {
                member.Ready = command.Ready;
                if (!command.Ready)
                {
                    CancelCountdown();
                }
            }
            return LobbyCommandResult.Applied;
        }

        public bool Advance(uint serverTick)
        {
            if (hasTick && serverTick != lastTick && !SequenceMath.IsNewer(serverTick, lastTick))
            {
                return false;
            }
            hasTick = true;
            lastTick = serverTick;
            if (Phase == LobbyPhase.Playing || Phase == LobbyPhase.Finished)
            {
                return true;
            }
            if (!AllOnlinePlayersReady())
            {
                CancelCountdown();
                return true;
            }
            if (Phase == LobbyPhase.Waiting)
            {
                Phase = LobbyPhase.Countdown;
                countdownDeadline = unchecked(serverTick + countdownTicks);
            }
            else if (unchecked((int)(serverTick - countdownDeadline)) >= 0)
            {
                Phase = LobbyPhase.Playing;
                PlayingStartedTick = serverTick;
            }
            return true;
        }

        public bool Finish()
        {
            if (Phase != LobbyPhase.Playing)
            {
                return false;
            }
            Phase = LobbyPhase.Finished;
            foreach (var member in members.Values)
            {
                member.Ready = false;
            }
            return true;
        }

        public bool BeginNextRound()
        {
            if (Phase == LobbyPhase.Countdown || Phase == LobbyPhase.Playing)
            {
                return false;
            }
            RoundEpoch = NextNonzero(RoundEpoch);
            Phase = LobbyPhase.Waiting;
            PlayingStartedTick = 0;
            countdownDeadline = 0;
            foreach (var member in members.Values)
            {
                member.Ready = false;
                member.HasSequence = false;
                member.Sequence = 0;
            }
            return true;
        }

        public LobbySnapshot Snapshot()
        {
            var ids = new List<uint>(members.Keys);
            ids.Sort();
            var players = new LobbyPlayerState[ids.Count];
            for (int index = 0; index < ids.Count; index++)
            {
                uint id = ids[index];
                var member = members[id];
                players[index] = new LobbyPlayerState(id, member.Epoch,
                    member.Connected, member.Recovering, member.Ready,
                    member.HasSequence ? member.Sequence : 0);
            }
            uint remaining = Phase == LobbyPhase.Countdown ? unchecked(countdownDeadline - lastTick) : 0;
            return new LobbySnapshot(RoundEpoch, lastTick, remaining, Phase, players);
        }

        private bool AllOnlinePlayersReady()
        {
            int online = 0;
            foreach (var member in members.Values)
            {
                if (member.Connected)
                {
                    online++;
                    if (!member.Ready || member.Recovering)
                    {
                        return false;
                    }
                }
            }
            return online >= MinimumPlayers;
        }

        private void CancelCountdown()
        {
            if (Phase == LobbyPhase.Countdown)
            {
                Phase = LobbyPhase.Waiting;
                countdownDeadline = 0;
            }
        }

        private static uint NextNonzero(uint value)
        {
            uint next = unchecked(value + 1);
            return next == 0 ? 1U : next;
        }

        private sealed class Member
        {
            internal uint Epoch;
            internal uint Sequence;
            internal bool HasSequence;
            internal bool Connected;
            internal bool Recovering;
            internal bool Ready;
        }
    }
}

using System;
using AFPS.Math;
using AFPS.NetCode.Weapons;

namespace AFPS.NetCode.Match
{
    public enum DeathmatchPhase : byte
    {
        WaitingForPlayers = 0,
        Playing = 1,
        Finished = 2
    }

    public readonly struct DeathmatchPlayerState
    {
        public readonly uint EntityId;
        /// <summary>完整 Q16.16 生命值；整数 Health 仅供旧 HUD 显示。</summary>
        public readonly Fix HealthFixed;
        public ushort Health => CombatHealthMath.ToDisplayHealth(HealthFixed);
        public readonly ushort Kills;
        public readonly ushort Deaths;
        public readonly ushort RespawnTicksRemaining;
        public readonly bool IsAlive;
        /// <summary>权威连接是否在线；掉线保留角色仍在名单中，不能把名单总数当在线人数。</summary>
        public readonly bool IsConnected;

        public DeathmatchPlayerState(
            uint entityId, ushort health, ushort kills, ushort deaths, ushort respawnTicksRemaining,
            bool isAlive, bool isConnected = true)
            : this(entityId, Fix.FromInt(health), kills, deaths, respawnTicksRemaining, isAlive, isConnected)
        {
        }
        public DeathmatchPlayerState(
            uint entityId, Fix health, ushort kills, ushort deaths, ushort respawnTicksRemaining,
            bool isAlive, bool isConnected = true)
        {
            EntityId = entityId;
            HealthFixed = health;
            Kills = kills;
            Deaths = deaths;
            RespawnTicksRemaining = respawnTicksRemaining;
            IsAlive = isAlive;
            IsConnected = isConnected;
        }
    }

    public sealed class DeathmatchState
    {
        private readonly DeathmatchPlayerState[] players;

        public DeathmatchPhase Phase { get; }
        public uint ServerTick { get; }
        public uint RemainingTicks { get; }
        public uint WinnerEntityId { get; }
        public int PlayerCount => players.Length;
        public int OnlinePlayerCount
        {
            get
            {
                int count = 0;
                foreach (var player in players)
                {
                    if (player.IsConnected)
                    {
                        count++;
                    }
                }
                return count;
            }
        }

        public DeathmatchState(DeathmatchPhase phase, uint serverTick, uint remainingTicks, uint winnerEntityId, DeathmatchPlayerState[] players)
        {
            Phase = phase;
            ServerTick = serverTick;
            RemainingTicks = remainingTicks;
            WinnerEntityId = winnerEntityId;
            this.players = players ?? Array.Empty<DeathmatchPlayerState>();
        }

        public DeathmatchPlayerState GetPlayer(int index) => players[index];

        public bool TryGetPlayer(uint entityId, out DeathmatchPlayerState state)
        {
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i].EntityId == entityId)
                {
                    state = players[i];
                    return true;
                }
            }

            state = default;
            return false;
        }
    }
}

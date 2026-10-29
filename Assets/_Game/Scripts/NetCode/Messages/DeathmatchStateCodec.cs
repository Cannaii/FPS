using System;
using AFPS.Math;
using AFPS.NetCode.Match;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.Serialization;

namespace AFPS.NetCode.Messages
{
    public static class DeathmatchStateCodec
    {
        public const int FixedPayloadSize = 14;
        // 每玩家15B：实体4B、Q16.16血量4B、击杀/死亡/重生Tick各2B、标志1B（bit0存活，bit1在线）。
        public const int PlayerPayloadSize = 15;
        public const int MaximumPlayerCount = 64;
        public const int MaximumPacketSize = PacketHeader.Size + FixedPayloadSize + MaximumPlayerCount * PlayerPayloadSize;

        public static int GetPacketSize(int playerCount) => PacketHeader.Size + FixedPayloadSize + playerCount * PlayerPayloadSize;

        public static bool TrySerialize(DeathmatchState state, uint sequence, ArraySegment<byte> destination, out int bytesWritten)
        {
            bytesWritten = 0;
            if (state == null
                || state.PlayerCount > MaximumPlayerCount)
            {
                return false;
            }

            int payloadSize = FixedPayloadSize + state.PlayerCount * PlayerPayloadSize;
            int packetSize = PacketHeader.Size + payloadSize;
            if (destination.Count < packetSize
                || !PacketHeaderCodec.TryWrite(new PacketHeader(NetworkMessageType.DeathmatchState, (ushort)payloadSize, sequence), destination))
            {
                return false;
            }

            PacketBufferWriter writer = new PacketBufferWriter(new ArraySegment<byte>(
                destination.Array, destination.Offset + PacketHeader.Size, payloadSize));
            if (!writer.TryWriteByte((byte)state.Phase)
                || !writer.TryWriteUInt32(state.ServerTick)
                || !writer.TryWriteUInt32(state.RemainingTicks)
                || !writer.TryWriteUInt32(state.WinnerEntityId)
                || !writer.TryWriteByte((byte)state.PlayerCount))
            {
                return false;
            }

            for (int i = 0; i < state.PlayerCount; i++)
            {
                DeathmatchPlayerState player = state.GetPlayer(i);
                if (player.EntityId == 0
                    || player.HealthFixed.Raw < 0
                    || !writer.TryWriteUInt32(player.EntityId)
                    || !writer.TryWriteInt32(player.HealthFixed.Raw)
                    || !writer.TryWriteUInt16(player.Kills)
                    || !writer.TryWriteUInt16(player.Deaths)
                    || !writer.TryWriteUInt16(player.RespawnTicksRemaining)
                    || !writer.TryWriteByte((byte)((player.IsAlive ? 1 : 0) | (player.IsConnected ? 2 : 0))))
                {
                    return false;
                }
            }

            bytesWritten = packetSize;
            return true;
        }

        public static bool TryDeserialize(ArraySegment<byte> packet, out PacketHeader header, out DeathmatchState state)
        {
            header = default;
            state = null;
            if (!PacketHeaderCodec.TryRead(packet, out header)
                || header.MessageType != NetworkMessageType.DeathmatchState
                || header.PayloadLength < FixedPayloadSize)
            {
                return false;
            }

            PacketBufferReader reader = new PacketBufferReader(new ArraySegment<byte>(
                packet.Array, packet.Offset + PacketHeader.Size, header.PayloadLength));
            if (!reader.TryReadByte(out byte phaseRaw)
                || phaseRaw > (byte)DeathmatchPhase.Finished
                || !reader.TryReadUInt32(out uint serverTick)
                || !reader.TryReadUInt32(out uint remainingTicks)
                || !reader.TryReadUInt32(out uint winnerEntityId)
                || !reader.TryReadByte(out byte playerCount)
                || playerCount > MaximumPlayerCount
                || header.PayloadLength != FixedPayloadSize + playerCount * PlayerPayloadSize)
            {
                return false;
            }

            DeathmatchPlayerState[] players = new DeathmatchPlayerState[playerCount];
            for (int i = 0; i < playerCount; i++)
            {
                if (!reader.TryReadUInt32(out uint entityId)
                    || entityId == 0
                    || !reader.TryReadInt32(out int health)
                    || health < 0
                    || !reader.TryReadUInt16(out ushort kills)
                    || !reader.TryReadUInt16(out ushort deaths)
                    || !reader.TryReadUInt16(out ushort respawnTicks)
                    || !reader.TryReadByte(out byte flags)
                    || flags > 3)
                {
                    return false;
                }

                players[i] = new DeathmatchPlayerState(entityId, Fix.FromRaw(health), kills, deaths,
                    respawnTicks, (flags & 1) != 0, (flags & 2) != 0);
            }

            if (reader.BytesRemaining != 0)
            {
                return false;
            }

            state = new DeathmatchState((DeathmatchPhase)phaseRaw, serverTick, remainingTicks, winnerEntityId, players);
            return true;
        }
    }
}

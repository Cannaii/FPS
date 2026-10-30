using System;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.Serialization;
using AFPS.NetCode.Weapons;

namespace AFPS.NetCode.Messages
{
    /// <summary>独立远端武器消息：19字节小端负载，不改变移动快照和Delta基线。</summary>
    public static class RemoteWeaponStateCodec
    {
        public const int PayloadSize = 19;
        public const int PacketSize = PacketHeader.Size + PayloadSize;

        public static bool TrySerialize(
            in RemoteWeaponState state,
            uint sequence,
            ArraySegment<byte> destination,
            out int bytesWritten)
        {
            bytesWritten = 0;
            if (!state.IsValid || destination.Count < PacketSize
                || !PacketHeaderCodec.TryWrite(new PacketHeader(
                    NetworkMessageType.RemoteWeaponState, PayloadSize, sequence), destination))
            {
                return false;
            }
            var writer = new PacketBufferWriter(new ArraySegment<byte>(
                destination.Array, destination.Offset + PacketHeader.Size, PayloadSize));
            bool success = writer.TryWriteUInt32(state.EntityId)
                && writer.TryWriteUInt32(state.ServerTick)
                && writer.TryWriteUInt16(state.WeaponId)
                && writer.TryWriteUInt32(state.ReloadTicksRemaining)
                && writer.TryWriteUInt32(state.EquipTicksRemaining)
                && writer.TryWriteByte(state.IsEquipped ? (byte)1 : (byte)0);
            bytesWritten = success ? PacketSize : 0;
            return success;
        }

        public static bool TryDeserialize(
            ArraySegment<byte> packet,
            out PacketHeader header,
            out RemoteWeaponState state)
        {
            header = default;
            state = default;
            if (!PacketHeaderCodec.TryRead(packet, out header)
                || header.MessageType != NetworkMessageType.RemoteWeaponState || header.PayloadLength != PayloadSize)
            {
                return false;
            }
            var reader = new PacketBufferReader(new ArraySegment<byte>(
                packet.Array, packet.Offset + PacketHeader.Size, PayloadSize));
            if (!reader.TryReadUInt32(out uint entity) || !reader.TryReadUInt32(out uint tick)
                || !reader.TryReadUInt16(out ushort weapon) || !reader.TryReadUInt32(out uint reload)
                || !reader.TryReadUInt32(out uint equip) || !reader.TryReadByte(out byte flags)
                || flags > 1 || reader.BytesRemaining != 0)
            {
                return false;
            }
            var candidate = new RemoteWeaponState(entity, tick, weapon, reload, equip, flags == 1);
            if (!candidate.IsValid)
            {
                return false;
            }
            state = candidate;
            return true;
        }
    }
}

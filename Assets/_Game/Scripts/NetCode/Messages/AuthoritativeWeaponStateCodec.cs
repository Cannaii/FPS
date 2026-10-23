using System;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.Serialization;
using AFPS.NetCode.Weapons;

namespace AFPS.NetCode.Messages
{
    public static class AuthoritativeWeaponStateCodec
    {
        // 小端：Entity/ServerTick/AckInputTick/Ammo/Capacity/ReloadTicks 各 4B，再加可用标志 1B。
        public const int PayloadSize = 31;
        public const int PacketSize = PacketHeader.Size + PayloadSize;

        public static bool IsValid(in AuthoritativeWeaponState state) => state.EntityId != 0
            && state.Capacity > 0 && state.Ammo >= 0 && state.Ammo <= state.Capacity
            && state.ReloadTicksRemaining <= ServerWeaponFireController.ReloadDurationTicks
            && state.WeaponId != 0 && state.EquipTicksRemaining <= AFPS.Simulation.Weapons.WeaponCatalog.EquipDurationTicks
            && (state.IsEquipped || (!state.IsReloading && !state.IsEquipping));

        public static bool TrySerialize(in AuthoritativeWeaponState state, uint sequence,
            ArraySegment<byte> destination, out int written)
        {
            written = 0;
            if (!IsValid(state) || destination.Count < PacketSize
                || !PacketHeaderCodec.TryWrite(new PacketHeader(NetworkMessageType.AuthoritativeWeaponState,
                    PayloadSize, sequence), destination))
            {
                return false;
            }
            PacketBufferWriter writer = new PacketBufferWriter(new ArraySegment<byte>(
                destination.Array, destination.Offset + PacketHeader.Size, PayloadSize));
            if (!writer.TryWriteUInt32(state.EntityId)
                || !writer.TryWriteUInt32(state.ServerTick)
                || !writer.TryWriteUInt32(state.AckInputTick)
                || !writer.TryWriteInt32(state.Ammo)
                || !writer.TryWriteInt32(state.Capacity)
                || !writer.TryWriteUInt32(state.ReloadTicksRemaining)
                || !writer.TryWriteByte(state.IsEquipped ? (byte)1 : (byte)0)
                || !writer.TryWriteUInt16(state.WeaponId) || !writer.TryWriteUInt32(state.EquipTicksRemaining))
            {
                return false;
            }
            written = PacketSize;
            return true;
        }

        public static bool TryDeserialize(ArraySegment<byte> packet, out PacketHeader header,
            out AuthoritativeWeaponState state)
        {
            state = default;
            if (!PacketHeaderCodec.TryRead(packet, out header)
                || header.MessageType != NetworkMessageType.AuthoritativeWeaponState
                || header.PayloadLength != PayloadSize)
            {
                return false;
            }
            PacketBufferReader reader = new PacketBufferReader(new ArraySegment<byte>(
                packet.Array, packet.Offset + PacketHeader.Size, PayloadSize));
            if (!reader.TryReadUInt32(out uint entity)
                || !reader.TryReadUInt32(out uint tick)
                || !reader.TryReadUInt32(out uint ack)
                || !reader.TryReadInt32(out int ammo)
                || !reader.TryReadInt32(out int capacity)
                || !reader.TryReadUInt32(out uint reload)
                || !reader.TryReadByte(out byte flags) || flags > 1
                || !reader.TryReadUInt16(out ushort weaponId) || !reader.TryReadUInt32(out uint equip)
                || reader.BytesRemaining != 0)
            {
                return false;
            }
            AuthoritativeWeaponState decoded = new AuthoritativeWeaponState(entity, tick, ack,
                ammo, capacity, reload, flags == 1, weaponId, equip);
            if (!IsValid(decoded))
            {
                return false;
            }
            state = decoded;
            return true;
        }
    }
}

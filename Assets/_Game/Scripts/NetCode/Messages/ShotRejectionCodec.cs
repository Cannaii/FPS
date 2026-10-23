using System;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.Serialization;
using AFPS.NetCode.Weapons;

namespace AFPS.NetCode.Messages
{
    /// <summary>只给拥有者的射击拒绝；Tick 为原始裁决时刻，重试发送不能改变该时刻。</summary>
    public readonly struct ShotRejection
    {
        public readonly uint EntityId;
        public readonly uint ShotSequence;
        public readonly uint InputTick;
        public readonly uint ServerTick;
        public readonly WeaponFireRejectReason Reason;

        public ShotRejection(uint entityId, uint shotSequence, uint inputTick, uint serverTick, WeaponFireRejectReason reason)
        {
            EntityId = entityId;
            ShotSequence = shotSequence;
            InputTick = inputTick;
            ServerTick = serverTick;
            Reason = reason;
        }
    }

    public static class ShotRejectionCodec
    {
        // 小端 Entity/ShotSequence/InputTick/ServerTick 各 uint32，拒绝原因 byte；零射击序号不发布。
        public const int PayloadSize = 17;
        public const int PacketSize = PacketHeader.Size + PayloadSize;
        public static bool IsValid(in ShotRejection value) => value.EntityId != 0 && value.ShotSequence != 0
            && value.Reason >= WeaponFireRejectReason.InvalidSequence && value.Reason <= WeaponFireRejectReason.Reloading;

        public static bool TrySerialize(in ShotRejection value, uint sequence, ArraySegment<byte> buffer, out int written)
        {
            written = 0;
            if (!IsValid(value) || buffer.Count < PacketSize
                || !PacketHeaderCodec.TryWrite(new PacketHeader(NetworkMessageType.ShotRejection, PayloadSize, sequence), buffer))
            {
                return false;
            }
            var writer = new PacketBufferWriter(new ArraySegment<byte>(buffer.Array, buffer.Offset + PacketHeader.Size, PayloadSize));
            if (!writer.TryWriteUInt32(value.EntityId) || !writer.TryWriteUInt32(value.ShotSequence)
                || !writer.TryWriteUInt32(value.InputTick) || !writer.TryWriteUInt32(value.ServerTick)
                || !writer.TryWriteByte((byte)value.Reason))
            {
                return false;
            }
            written = PacketSize;
            return true;
        }

        public static bool TryDeserialize(ArraySegment<byte> packet, out PacketHeader header, out ShotRejection value)
        {
            value = default;
            if (!PacketHeaderCodec.TryRead(packet, out header) || header.MessageType != NetworkMessageType.ShotRejection
                || header.PayloadLength != PayloadSize)
            {
                return false;
            }
            var reader = new PacketBufferReader(new ArraySegment<byte>(packet.Array, packet.Offset + PacketHeader.Size, PayloadSize));
            if (!reader.TryReadUInt32(out uint entity) || !reader.TryReadUInt32(out uint shot)
                || !reader.TryReadUInt32(out uint input) || !reader.TryReadUInt32(out uint tick)
                || !reader.TryReadByte(out byte reason))
            {
                return false;
            }
            var decoded = new ShotRejection(entity, shot, input, tick, (WeaponFireRejectReason)reason);
            if (!IsValid(decoded))
            {
                return false;
            }
            value = decoded;
            return true;
        }
    }
}

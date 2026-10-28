using System;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.Serialization;
using AFPS.NetCode.Weapons;

namespace AFPS.NetCode.Messages
{
    /// <summary>一次完整恢复的原子种子；移动和武器必须来自同一世界Tick、同一已处理输入边界。</summary>
    public readonly struct SessionRecoverySeed
    {
        public readonly uint Attempt;
        public readonly uint EntityId;
        public readonly uint LastShotSequence;
        public readonly uint ReplayWindow;
        public readonly AuthoritativePlayerState Movement;
        public readonly AuthoritativeWeaponState Weapon;

        public SessionRecoverySeed(uint attempt, uint entityId, uint lastShotSequence,
            AuthoritativePlayerState movement, AuthoritativeWeaponState weapon, uint replayWindow = 32)
        {
            Attempt = attempt;
            EntityId = entityId;
            LastShotSequence = lastShotSequence;
            ReplayWindow = replayWindow;
            Movement = movement;
            Weapon = weapon;
        }
    }

    public static class SessionRecoveryCodec
    {
        // 复用已有Raw编解码的完整内包，避免新增另一份玩家/武器字段布局。
        private const int MetadataSize = 16;
        public const int SeedPayloadSize = MetadataSize + AuthoritativePlayerStateCodec.PacketSize + AuthoritativeWeaponStateCodec.PacketSize;
        public const int SeedPacketSize = PacketHeader.Size + SeedPayloadSize;
        public const int ReceiptPacketSize = PacketHeader.Size + 8;

        public static bool TryWriteSeed(in SessionRecoverySeed seed, uint epoch,
            ArraySegment<byte> destination, out int written)
        {
            written = 0;
            if (!IsValid(seed) || epoch == 0 || destination.Count < SeedPacketSize
                || !PacketHeaderCodec.TryWrite(new PacketHeader(NetworkMessageType.SessionRecoverySeed,
                    SeedPayloadSize, epoch), destination))
            {
                return false;
            }
            var writer = new PacketBufferWriter(Slice(destination, PacketHeader.Size, MetadataSize));
            if (!writer.TryWriteUInt32(seed.Attempt) || !writer.TryWriteUInt32(seed.EntityId)
                || !writer.TryWriteUInt32(seed.LastShotSequence)
                || !writer.TryWriteUInt32(seed.ReplayWindow)
                || !AuthoritativePlayerStateCodec.TrySerialize(seed.Movement, epoch,
                    Slice(destination, PacketHeader.Size + MetadataSize, AuthoritativePlayerStateCodec.PacketSize), out _)
                || !AuthoritativeWeaponStateCodec.TrySerialize(seed.Weapon, epoch,
                    Slice(destination, PacketHeader.Size + MetadataSize + AuthoritativePlayerStateCodec.PacketSize,
                        AuthoritativeWeaponStateCodec.PacketSize), out _))
            {
                return false;
            }
            written = SeedPacketSize;
            return true;
        }

        public static bool TryReadSeed(ArraySegment<byte> packet, out uint epoch, out SessionRecoverySeed seed)
        {
            epoch = 0;
            seed = default;
            if (!PacketHeaderCodec.TryRead(packet, out var header)
                || header.MessageType != NetworkMessageType.SessionRecoverySeed
                || header.Sequence == 0 || header.PayloadLength != SeedPayloadSize)
            {
                return false;
            }
            var reader = new PacketBufferReader(Slice(packet, PacketHeader.Size, MetadataSize));
            if (!reader.TryReadUInt32(out uint attempt) || !reader.TryReadUInt32(out uint entityId)
                || !reader.TryReadUInt32(out uint lastShot)
                || !reader.TryReadUInt32(out uint replayWindow)
                || !AuthoritativePlayerStateCodec.TryDeserialize(
                    Slice(packet, PacketHeader.Size + MetadataSize, AuthoritativePlayerStateCodec.PacketSize),
                    out var movementHeader, out var movement)
                || !AuthoritativeWeaponStateCodec.TryDeserialize(
                    Slice(packet, PacketHeader.Size + MetadataSize + AuthoritativePlayerStateCodec.PacketSize,
                        AuthoritativeWeaponStateCodec.PacketSize), out var weaponHeader, out var weapon)
                || movementHeader.Sequence != header.Sequence || weaponHeader.Sequence != header.Sequence)
            {
                return false;
            }
            var decoded = new SessionRecoverySeed(attempt, entityId, lastShot, movement, weapon, replayWindow);
            if (!IsValid(decoded))
            {
                return false;
            }
            epoch = header.Sequence;
            seed = decoded;
            return true;
        }

        public static bool TryWriteReceipt(NetworkMessageType type, uint attempt, uint epoch, uint entityId,
            ArraySegment<byte> destination, out int written)
        {
            written = 0;
            if (!IsReceipt(type) || attempt == 0 || epoch == 0 || entityId == 0
                || destination.Count < ReceiptPacketSize
                || !PacketHeaderCodec.TryWrite(new PacketHeader(type, 8, epoch), destination))
            {
                return false;
            }
            var writer = new PacketBufferWriter(Slice(destination, PacketHeader.Size, 8));
            if (!writer.TryWriteUInt32(attempt) || !writer.TryWriteUInt32(entityId))
            {
                return false;
            }
            written = ReceiptPacketSize;
            return true;
        }

        public static bool TryReadReceipt(ArraySegment<byte> packet, NetworkMessageType expectedType,
            out uint attempt, out uint epoch, out uint entityId)
        {
            attempt = 0;
            epoch = 0;
            entityId = 0;
            if (!IsReceipt(expectedType) || !PacketHeaderCodec.TryRead(packet, out var header)
                || header.MessageType != expectedType || header.Sequence == 0 || header.PayloadLength != 8)
            {
                return false;
            }
            var reader = new PacketBufferReader(Slice(packet, PacketHeader.Size, 8));
            if (!reader.TryReadUInt32(out uint decodedAttempt) || !reader.TryReadUInt32(out uint decodedEntity)
                || decodedAttempt == 0 || decodedEntity == 0)
            {
                return false;
            }
            attempt = decodedAttempt;
            epoch = header.Sequence;
            entityId = decodedEntity;
            return true;
        }

        private static bool IsValid(in SessionRecoverySeed seed)
        {
            return seed.Attempt != 0 && seed.EntityId != 0 && seed.ReplayWindow <= 32
                && seed.EntityId == seed.Weapon.EntityId
                && seed.Movement.ServerTick == seed.Weapon.ServerTick
                && seed.Movement.LastProcessedInputTick == seed.Weapon.AckInputTick;
        }

        private static bool IsReceipt(NetworkMessageType type)
        {
            return type == NetworkMessageType.SessionRecoveryAck || type == NetworkMessageType.SessionRecoveryConfirmed;
        }

        private static ArraySegment<byte> Slice(ArraySegment<byte> segment, int offset, int count)
        {
            return new ArraySegment<byte>(segment.Array, segment.Offset + offset, count);
        }
    }
}

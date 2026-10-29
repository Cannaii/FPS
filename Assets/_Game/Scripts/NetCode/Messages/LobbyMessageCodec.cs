using System;
using AFPS.NetCode.Match;
using AFPS.NetCode.Protocol;

namespace AFPS.NetCode.Messages
{
    public static class LobbyMessageCodec
    {
        public const int ReadyPacketSize = PacketHeader.Size + LobbyPayloadCodec.ReadyPayloadSize;
        public const int MaximumSnapshotPacketSize = PacketHeader.Size + LobbyPayloadCodec.MaximumSnapshotSize;

        public static bool TryWriteReady(in LobbyReadyCommand command, ArraySegment<byte> destination)
        {
            return destination.Array != null && destination.Count >= ReadyPacketSize
                && LobbyPayloadCodec.TryWriteReady(command, new ArraySegment<byte>(destination.Array,
                    destination.Offset + PacketHeader.Size, LobbyPayloadCodec.ReadyPayloadSize))
                && PacketHeaderCodec.TryWrite(new PacketHeader(NetworkMessageType.LobbyReady,
                    LobbyPayloadCodec.ReadyPayloadSize, command.Sequence), destination);
        }

        public static bool TryReadReady(ArraySegment<byte> packet, out LobbyReadyCommand command)
        {
            command = default;
            if (!PacketHeaderCodec.TryRead(packet, out var header) || header.MessageType != NetworkMessageType.LobbyReady
                || header.PayloadLength != LobbyPayloadCodec.ReadyPayloadSize
                || !LobbyPayloadCodec.TryReadReady(new ArraySegment<byte>(packet.Array,
                    packet.Offset + PacketHeader.Size, header.PayloadLength), out var decoded)
                || header.Sequence != decoded.Sequence)
            {
                return false;
            }
            command = decoded;
            return true;
        }

        public static bool TryWriteSnapshot(LobbySnapshot snapshot, uint sequence,
            ArraySegment<byte> destination, out int written)
        {
            written = 0;
            if (sequence == 0 || destination.Array == null || destination.Count < PacketHeader.Size
                || !LobbyPayloadCodec.TryWriteSnapshot(snapshot, new ArraySegment<byte>(destination.Array,
                    destination.Offset + PacketHeader.Size, destination.Count - PacketHeader.Size), out int payload)
                || !PacketHeaderCodec.TryWrite(new PacketHeader(NetworkMessageType.LobbySnapshot,
                    (ushort)payload, sequence), destination))
            {
                return false;
            }
            written = PacketHeader.Size + payload;
            return true;
        }

        public static bool TryReadSnapshot(ArraySegment<byte> packet, out uint sequence, out LobbySnapshot snapshot)
        {
            sequence = 0;
            snapshot = null;
            if (!PacketHeaderCodec.TryRead(packet, out var header) || header.MessageType != NetworkMessageType.LobbySnapshot
                || header.Sequence == 0 || !LobbyPayloadCodec.TryReadSnapshot(new ArraySegment<byte>(packet.Array,
                    packet.Offset + PacketHeader.Size, header.PayloadLength), out var decoded))
            {
                return false;
            }
            sequence = header.Sequence;
            snapshot = decoded;
            return true;
        }
    }
}

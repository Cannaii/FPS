using System;
using AFPS.NetCode.Match;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.Serialization;
using AFPS.Simulation.World;

namespace AFPS.NetCode.Runtime
{
    public readonly struct LanRoomAdvertisement
    {
        public readonly ulong InstanceId;
        public readonly byte GameProtocol;
        public readonly ushort Port;
        public readonly byte OnlinePlayers;
        public readonly byte Capacity;
        public readonly DeathmatchPhase Phase;
        public readonly SimulationWorldIdentity Identity;

        public LanRoomAdvertisement(ulong instanceId, byte gameProtocol, ushort port,
            byte onlinePlayers, byte capacity, DeathmatchPhase phase, SimulationWorldIdentity identity)
        {
            InstanceId = instanceId;
            GameProtocol = gameProtocol;
            Port = port;
            OnlinePlayers = onlinePlayers;
            Capacity = capacity;
            Phase = phase;
            Identity = identity;
        }

        public bool IsValid => InstanceId != 0 && GameProtocol != 0 && Port != 0
            && Capacity > 0 && Capacity <= 64 && OnlinePlayers <= Capacity
            && Phase <= DeathmatchPhase.Finished && Identity.AlgorithmVersion != 0;

        public bool IsCompatible(SimulationWorldIdentity identity)
        {
            return GameProtocol == PacketHeader.CurrentProtocolVersion && Identity.Equals(identity);
        }
    }

    /// <summary>独立发现协议，不复用游戏消息类型；没有恢复凭据、输入或玩家身份数据。</summary>
    public static class LanRoomDiscoveryProtocol
    {
        public const int QuerySize = 16;
        public const int AdvertisementSize = 50;
        private const uint Magic = 0x52444641; // 小端ASCII：AFDR。
        private const byte Version = 1;

        public static bool TryWriteQuery(ulong nonce, ArraySegment<byte> destination)
        {
            if (nonce == 0 || destination.Array == null || destination.Count < QuerySize)
            {
                return false;
            }
            var writer = new PacketBufferWriter(destination);
            return WriteHeader(ref writer, 1) && writer.TryWriteUInt64(nonce);
        }

        public static bool TryReadQuery(ArraySegment<byte> packet, out ulong nonce)
        {
            nonce = 0;
            if (packet.Array == null || packet.Count != QuerySize)
            {
                return false;
            }
            var reader = new PacketBufferReader(packet);
            if (!ReadHeader(ref reader, 1) || !reader.TryReadUInt64(out ulong value) || value == 0)
            {
                return false;
            }
            nonce = value;
            return true;
        }

        public static bool TryWriteAdvertisement(ulong nonce, in LanRoomAdvertisement room,
            ArraySegment<byte> destination)
        {
            if (nonce == 0 || !room.IsValid || destination.Array == null || destination.Count < AdvertisementSize)
            {
                return false;
            }
            var writer = new PacketBufferWriter(destination);
            return WriteHeader(ref writer, 2) && writer.TryWriteUInt64(nonce)
                && writer.TryWriteUInt64(room.InstanceId) && writer.TryWriteByte(room.GameProtocol)
                && writer.TryWriteByte(room.OnlinePlayers) && writer.TryWriteByte(room.Capacity)
                && writer.TryWriteByte((byte)room.Phase) && writer.TryWriteUInt16(room.Port)
                && writer.TryWriteUInt32(room.Identity.AlgorithmVersion)
                && writer.TryWriteUInt64(room.Identity.WorldHash)
                && writer.TryWriteUInt64(room.Identity.ConfigurationHash);
        }

        public static bool TryReadAdvertisement(ArraySegment<byte> packet, out ulong nonce,
            out LanRoomAdvertisement room)
        {
            nonce = 0;
            room = default;
            if (packet.Array == null || packet.Count != AdvertisementSize)
            {
                return false;
            }
            var reader = new PacketBufferReader(packet);
            if (!ReadHeader(ref reader, 2) || !reader.TryReadUInt64(out ulong request) || request == 0
                || !reader.TryReadUInt64(out ulong instance) || !reader.TryReadByte(out byte protocol)
                || !reader.TryReadByte(out byte online) || !reader.TryReadByte(out byte capacity)
                || !reader.TryReadByte(out byte phase) || !reader.TryReadUInt16(out ushort port)
                || !reader.TryReadUInt32(out uint algorithm) || algorithm == 0
                || !reader.TryReadUInt64(out ulong world) || !reader.TryReadUInt64(out ulong configuration))
            {
                return false;
            }
            var decoded = new LanRoomAdvertisement(instance, protocol, port, online, capacity,
                (DeathmatchPhase)phase, new SimulationWorldIdentity(algorithm, world, configuration));
            if (!decoded.IsValid)
            {
                return false;
            }
            nonce = request;
            room = decoded;
            return true;
        }

        private static bool WriteHeader(ref PacketBufferWriter writer, byte kind)
        {
            return writer.TryWriteUInt32(Magic) && writer.TryWriteByte(Version)
                && writer.TryWriteByte(kind) && writer.TryWriteUInt16(0);
        }

        private static bool ReadHeader(ref PacketBufferReader reader, byte expectedKind)
        {
            return reader.TryReadUInt32(out uint magic) && magic == Magic
                && reader.TryReadByte(out byte version) && version == Version
                && reader.TryReadByte(out byte kind) && kind == expectedKind
                && reader.TryReadUInt16(out ushort reserved) && reserved == 0;
        }
    }
}

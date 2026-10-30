using System;
using System.Collections.Generic;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.Transport;
using AFPS.NetCode.Weapons;
using AFPS.Simulation.Weapons;

namespace AFPS.NetCode.Sessions
{
    public sealed partial class NetworkSessionCoordinator
    {
        // 每连接/可见实体最多一条发送记录；移出兴趣、断线和销毁实体时清理。
        private readonly Dictionary<(TransportConnectionId Connection, uint Entity), RemoteWeaponState> sentRemoteWeapons =
            new Dictionary<(TransportConnectionId, uint), RemoteWeaponState>();
        private readonly List<(TransportConnectionId Connection, uint Entity)> expiredRemoteWeaponKeys =
            new List<(TransportConnectionId, uint)>();
        private readonly byte[] remoteWeaponBuffer = new byte[RemoteWeaponStateCodec.PacketSize];
        private uint remoteWeaponSequence;
        public RemoteWeaponReplicas RemoteWeapons { get; } = new RemoteWeaponReplicas();
        public ulong RemoteWeaponPacketsSent { get; private set; }
        public ulong RemoteWeaponBytesSent { get; private set; }

        private void SendRemoteWeapon(TransportConnectionId connection, uint entityId, uint tick)
        {
            if (!allServerSessions.TryGetValue(entityId, out var session))
            {
                return;
            }
            bool active = !session.IsRetainedOffline && !IsRecoveringEntity(entityId)
                && deathmatchPhase == AFPS.NetCode.Match.DeathmatchPhase.Playing;
            RemoteWeaponState state = session.CreateRemoteWeaponState(tick, active);
            var key = (connection, entityId);
            // 跟随成功的移动快照发送：状态变化立即补充，空闲每半秒刷新，丢包可自行收敛。
            if (sentRemoteWeapons.TryGetValue(key, out var previous)
                && state.SamePresentation(previous) && unchecked(tick - previous.ServerTick) < 25)
            {
                return;
            }
            remoteWeaponSequence = unchecked(remoteWeaponSequence + 1);
            if (RemoteWeaponStateCodec.TrySerialize(state, remoteWeaponSequence,
                    new ArraySegment<byte>(remoteWeaponBuffer), out int bytes)
                && serverTransport.Send(connection, TransportDelivery.Unreliable,
                    new ArraySegment<byte>(remoteWeaponBuffer, 0, bytes)) == TransportSendResult.Success)
            {
                sentRemoteWeapons[key] = state;
                RemoteWeaponPacketsSent++;
                RemoteWeaponBytesSent += (ulong)bytes;
            }
        }

        private bool ReceiveRemoteWeapon(ArraySegment<byte> packet)
        {
            if (!RemoteWeaponStateCodec.TryDeserialize(packet, out var header, out var state)
                || RemotePlayers == null || state.EntityId == RemotePlayers.LocalEntityId
                || !RemotePlayers.ContainsEntity(state.EntityId)
                || (state.WeaponId != weaponConfig.WeaponId
                    && state.WeaponId != WeaponCatalog.SecondaryFor(weaponConfig).WeaponId))
            {
                return false;
            }
            // 未有移动实体的包直接拒绝：迟到的旧武器包不能在Despawn之后重建表现。
            return RemoteWeapons.TryInsert(state, header.Sequence);
        }

        private void RemoveRemoteWeaponSends(TransportConnectionId? connection = null, uint entityId = 0)
        {
            expiredRemoteWeaponKeys.Clear();
            foreach (var key in sentRemoteWeapons.Keys)
            {
                if ((connection.HasValue && key.Connection.Equals(connection.Value))
                    || (entityId != 0 && key.Entity == entityId))
                {
                    expiredRemoteWeaponKeys.Add(key);
                }
            }
            foreach (var key in expiredRemoteWeaponKeys)
            {
                sentRemoteWeapons.Remove(key);
            }
        }
    }
}

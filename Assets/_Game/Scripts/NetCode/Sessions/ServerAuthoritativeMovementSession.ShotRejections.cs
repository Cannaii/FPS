using System;
using System.Collections.Generic;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Transport;
using AFPS.NetCode.Weapons;
using AFPS.Simulation.Characters;

namespace AFPS.NetCode.Sessions
{
    public sealed partial class ServerAuthoritativeMovementSession
    {
        private readonly Queue<ShotRejection> pendingRejections = new Queue<ShotRejection>();
        private readonly byte[] rejectionBuffer = new byte[ShotRejectionCodec.PacketSize];
        private uint rejectionSequence;
        public ulong DroppedShotRejectionCount { get; private set; }

        private void SendShotRejections(uint serverTick, in PlayerInputCommand original)
        {
            if (entityId == 0 || weaponFireController == null)
            {
                return;
            }
            if (original.FirePressed && original.ShotSequence != 0 && !LastWeaponFireResult.Accepted)
            {
                // 死亡/终局过滤后的 appliedCommand 无开火事件，仍要向原始请求返回明确的不可用裁决。
                var reason = LastWeaponFireResult.RejectReason == WeaponFireRejectReason.NotRequested
                    ? WeaponFireRejectReason.WeaponUnavailable : LastWeaponFireResult.RejectReason;
                if (pendingRejections.Count == 256)
                {
                    pendingRejections.Dequeue();
                    DroppedShotRejectionCount++;
                }
                pendingRejections.Enqueue(new ShotRejection(entityId, original.ShotSequence, original.Tick, serverTick, reason));
            }
            while (pendingRejections.Count > 0)
            {
                if (!ShotRejectionCodec.TrySerialize(pendingRejections.Peek(), rejectionSequence,
                    new ArraySegment<byte>(rejectionBuffer), out int count)
                    || weaponTransport.Send(weaponConnection, TransportDelivery.ReliableSequenced,
                        new ArraySegment<byte>(rejectionBuffer, 0, count)) != TransportSendResult.Success)
                {
                    // 失败不消费队列或包序号，后续模拟 Tick 重试；容量上限避免断流时无限增长。
                    break;
                }
                pendingRejections.Dequeue();
                rejectionSequence = unchecked(rejectionSequence + 1);
            }
        }
    }
}

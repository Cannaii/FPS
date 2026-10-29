using System.Collections.Generic;
using AFPS.NetCode.Weapons;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Protocol;

namespace AFPS.NetCode.Sessions
{
    public sealed partial class NetworkSessionCoordinator
    {
        // 仅为表现缓冲：最多保留 256 条尚未消费的结果，超限丢弃最旧项，不回写权威数据。
        public const int ShotPresentationCapacity = 256;
        private readonly Queue<AuthoritativeShotResult> shotPresentationResults =
            new Queue<AuthoritativeShotResult>(ShotPresentationCapacity);

        public int PendingShotPresentationCount => shotPresentationResults.Count;
        public ulong DroppedShotPresentationCount { get; private set; }
        private readonly Queue<ShotRejection> shotRejections = new Queue<ShotRejection>();
        private bool hasRejectionSequence;
        private uint lastRejectionSequence;

        private bool IsCompatibleWeaponState(in AuthoritativeWeaponState state)
        {
            var secondary = AFPS.Simulation.Weapons.WeaponCatalog.SecondaryFor(weaponConfig);
            return (state.WeaponId == weaponConfig.WeaponId && state.Capacity == weaponConfig.MagazineCapacity)
                || (state.WeaponId == secondary.WeaponId && state.Capacity == secondary.MagazineCapacity);
        }

        public bool TryDequeueShotRejection(out ShotRejection rejection)
        {
            if (shotRejections.Count == 0)
            {
                rejection = default;
                return false;
            }
            rejection = shotRejections.Dequeue();
            return true;
        }

        private bool ReceiveShotRejection(System.ArraySegment<byte> packet)
        {
            if (!ShotRejectionCodec.TryDeserialize(packet, out var header, out var value)
                || RemotePlayers == null || value.EntityId != RemotePlayers.LocalEntityId
                || (hasRejectionSequence && !SequenceMath.IsNewer(header.Sequence, lastRejectionSequence)))
            {
                return false;
            }
            hasRejectionSequence = true;
            lastRejectionSequence = header.Sequence;
            if (shotRejections.Count == ShotPresentationCapacity)
            {
                shotRejections.Dequeue();
                DroppedShotPresentationCount++;
            }
            shotRejections.Enqueue(value);
            return true;
        }

        /// <summary>单个表现消费者按接收顺序取出结果；同一渲染帧可消费多条。</summary>
        public bool TryDequeueShotPresentation(out AuthoritativeShotResult result)
        {
            if (shotPresentationResults.Count == 0)
            {
                result = default;
                return false;
            }

            result = shotPresentationResults.Dequeue();
            return true;
        }

        private void EnqueueShotPresentation(in AuthoritativeShotResult result)
        {
            if (shotPresentationResults.Count == ShotPresentationCapacity)
            {
                shotPresentationResults.Dequeue();
                DroppedShotPresentationCount++;
            }
            shotPresentationResults.Enqueue(result);
        }

        private void ResetClientShotResults()
        {
            shotPresentationResults.Clear();
            shotRejections.Clear();
            hasRejectionSequence = false;
            lastRejectionSequence = 0;
            DroppedShotPresentationCount = 0;
            hasReceivedShotResultSequence = false;
            lastReceivedShotResultSequence = 0;
            LastReceivedShotResult = default;
            ShotResultVersion = 0;
        }
    }
}

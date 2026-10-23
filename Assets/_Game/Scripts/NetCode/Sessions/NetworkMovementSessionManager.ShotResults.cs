using System.Collections.Generic;
using AFPS.NetCode.Weapons;

namespace AFPS.NetCode.Sessions
{
    public sealed partial class NetworkMovementSessionManager
    {
        // 仅为表现缓冲：最多保留 256 条尚未消费的结果，超限丢弃最旧项，不回写权威数据。
        public const int ShotPresentationCapacity = 256;
        private readonly Queue<AuthoritativeShotResult> shotPresentationResults =
            new Queue<AuthoritativeShotResult>(ShotPresentationCapacity);

        public int PendingShotPresentationCount => shotPresentationResults.Count;
        public ulong DroppedShotPresentationCount { get; private set; }

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
            DroppedShotPresentationCount = 0;
            hasReceivedShotResultSequence = false;
            lastReceivedShotResultSequence = 0;
            LastReceivedShotResult = default;
            ShotResultVersion = 0;
        }
    }
}

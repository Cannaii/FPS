using System;
using AFPS.NetCode.Protocol;

namespace AFPS.NetCode.SnapshotInterpolation
{
    /// <summary>
    /// 为单个远端玩家保存按 Server Tick 从旧到新排列的固定容量快照。
    /// 乱序到达的包会插入正确位置，重复 Tick 会用新内容替换旧内容。
    /// </summary>
    public sealed class RemotePlayerSnapshotBuffer
    {
        private readonly RemotePlayerSnapshot[] snapshots;

        public uint EntityId { get; }
        public int Capacity => snapshots.Length;
        public int Count { get; private set; }

        public RemotePlayerSnapshotBuffer(uint entityId, int capacity)
        {
            if (entityId == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(entityId), "远端玩家实体 ID 不能为零。");
            }

            if (capacity < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "快照缓冲区至少需要两个槽位才能执行插值。");
            }

            EntityId = entityId;
            snapshots = new RemotePlayerSnapshot[capacity];
        }

        public SnapshotBufferInsertResult Insert(in RemotePlayerSnapshot snapshot)
        {
            if (snapshot.EntityId != EntityId)
            {
                return SnapshotBufferInsertResult.EntityMismatch;
            }

            for (int i = 0; i < Count; i++)
            {
                if (snapshots[i].ServerTick == snapshot.ServerTick)
                {
                    snapshots[i] = snapshot;
                    return SnapshotBufferInsertResult.Replaced;
                }
            }

            if (Count == snapshots.Length && SequenceMath.IsOlder(snapshot.ServerTick, snapshots[0].ServerTick))
            {
                return SnapshotBufferInsertResult.TooOld;
            }

            int insertionIndex = Count;
            for (int i = 0; i < Count; i++)
            {
                if (SequenceMath.IsNewer(snapshots[i].ServerTick, snapshot.ServerTick))
                {
                    insertionIndex = i;
                    break;
                }
            }

            if (Count == snapshots.Length)
            {
                Array.Copy(snapshots, 1, snapshots, 0, Count - 1);
                Count--;
                insertionIndex--;
            }

            if (insertionIndex < Count)
            {
                Array.Copy(snapshots, insertionIndex, snapshots, insertionIndex + 1, Count - insertionIndex);
            }

            snapshots[insertionIndex] = snapshot;
            Count++;
            return SnapshotBufferInsertResult.Added;
        }

        public RemotePlayerSnapshot GetAt(int index)
        {
            if (index < 0 || index >= Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return snapshots[index];
        }

        public void Clear()
        {
            Array.Clear(snapshots, 0, snapshots.Length);
            Count = 0;
        }
    }
}

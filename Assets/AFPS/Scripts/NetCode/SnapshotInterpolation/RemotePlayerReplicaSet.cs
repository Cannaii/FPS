using System;
using System.Collections.Generic;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Protocol;

namespace AFPS.NetCode.SnapshotInterpolation
{
    /// <summary>
    /// 保存当前客户端观察到的所有远端玩家时间线，并过滤自己的实体和过时快照。
    /// 该类不创建 Unity 对象，表现层可按实体 ID 独立生成或销毁视图。
    /// </summary>
    public sealed class RemotePlayerReplicaSet
    {
        private readonly Dictionary<uint, RemotePlayerSnapshotTimeline> timelines = new Dictionary<uint, RemotePlayerSnapshotTimeline>();
        private readonly Dictionary<uint, uint> lastSequences = new Dictionary<uint, uint>();
        private readonly int snapshotCapacity;
        private readonly float tickDeltaTime;
        private readonly double interpolationDelayTicks;
        private readonly double maxExtrapolationTicks;
        private readonly float teleportDistance;
        private bool hasLatestServerTick;

        /// <summary>服务器分配给当前客户端控制角色的实体 ID，未收到分配消息时为 0。</summary>
        public uint LocalEntityId { get; private set; }

        /// <summary>最近接受的远端快照所属服务器世界 Tick。</summary>
        public uint LatestServerTick { get; private set; }

        /// <summary>每接受一个未倒退的最新服务器 Tick 时递增，供表现层刷新服务器 Tick 时钟。</summary>
        public uint SnapshotVersion { get; private set; }

        public int Count => timelines.Count;

        public RemotePlayerReplicaSet(int snapshotCapacity, float tickDeltaTime, double interpolationDelayTicks, double maxExtrapolationTicks, float teleportDistance)
        {
            if (snapshotCapacity < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(snapshotCapacity));
            }

            if (tickDeltaTime <= 0f || float.IsNaN(tickDeltaTime) || float.IsInfinity(tickDeltaTime))
            {
                throw new ArgumentOutOfRangeException(nameof(tickDeltaTime));
            }

            if (interpolationDelayTicks < 0d || double.IsNaN(interpolationDelayTicks) || double.IsInfinity(interpolationDelayTicks))
            {
                throw new ArgumentOutOfRangeException(nameof(interpolationDelayTicks));
            }

            if (maxExtrapolationTicks < 0d || double.IsNaN(maxExtrapolationTicks) || double.IsInfinity(maxExtrapolationTicks))
            {
                throw new ArgumentOutOfRangeException(nameof(maxExtrapolationTicks));
            }

            if (teleportDistance <= 0f || float.IsNaN(teleportDistance) || float.IsInfinity(teleportDistance))
            {
                throw new ArgumentOutOfRangeException(nameof(teleportDistance));
            }

            this.snapshotCapacity = snapshotCapacity;
            this.tickDeltaTime = tickDeltaTime;
            this.interpolationDelayTicks = interpolationDelayTicks;
            this.maxExtrapolationTicks = maxExtrapolationTicks;
            this.teleportDistance = teleportDistance;
        }

        /// <summary>应用可靠身份分配，并移除可能因跨通道先到达而误建的本地副本。</summary>
        public bool TryApplyAssignment(ArraySegment<byte> packet)
        {
            if (!PlayerSessionAssignmentCodec.TryDeserialize(packet, out PlayerSessionAssignment assignment))
            {
                return false;
            }

            LocalEntityId = assignment.EntityId;
            timelines.Remove(LocalEntityId);
            lastSequences.Remove(LocalEntityId);
            return true;
        }

        /// <summary>解码并插入一个远端玩家快照；同实体重复或倒退的包序号会被拒绝。</summary>
        public bool TryInsertSnapshot(ArraySegment<byte> packet, out RemotePlayerSnapshot snapshot)
        {
            snapshot = default;
            if (!RemotePlayerSnapshotCodec.TryDeserialize(packet, out PacketHeader header, out RemotePlayerSnapshot decoded) || decoded.EntityId == LocalEntityId)
            {
                return false;
            }

            if (lastSequences.TryGetValue(decoded.EntityId, out uint lastSequence) && !SequenceMath.IsNewer(header.Sequence, lastSequence))
            {
                return false;
            }

            if (!timelines.TryGetValue(decoded.EntityId, out RemotePlayerSnapshotTimeline timeline))
            {
                timeline = new RemotePlayerSnapshotTimeline(decoded.EntityId, snapshotCapacity, tickDeltaTime, interpolationDelayTicks, maxExtrapolationTicks, teleportDistance);
                timelines.Add(decoded.EntityId, timeline);
            }

            SnapshotBufferInsertResult insertResult = timeline.Insert(decoded);
            if (insertResult == SnapshotBufferInsertResult.EntityMismatch || insertResult == SnapshotBufferInsertResult.TooOld)
            {
                return false;
            }

            lastSequences[decoded.EntityId] = header.Sequence;
            if (!hasLatestServerTick || decoded.ServerTick == LatestServerTick || SequenceMath.IsNewer(decoded.ServerTick, LatestServerTick))
            {
                LatestServerTick = decoded.ServerTick;
                SnapshotVersion = unchecked(SnapshotVersion + 1);
                hasLatestServerTick = true;
            }
            snapshot = decoded;
            return true;
        }

        /// <summary>应用服务器可靠销毁消息并清除该实体的插值历史。</summary>
        public bool TryApplyDespawn(ArraySegment<byte> packet, out uint entityId)
        {
            if (!PlayerDespawnCodec.TryDeserialize(packet, out entityId))
            {
                return false;
            }

            timelines.Remove(entityId);
            lastSequences.Remove(entityId);
            return true;
        }

        public bool TrySample(uint entityId, double estimatedServerTick, out RemotePlayerRenderState state)
        {
            state = default;
            return timelines.TryGetValue(entityId, out RemotePlayerSnapshotTimeline timeline) && timeline.TrySample(estimatedServerTick, out state);
        }

        /// <summary>把当前远端实体 ID 复制到调用方复用列表，避免每帧产生迭代器分配。</summary>
        public void CopyEntityIds(List<uint> destination)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            destination.Clear();
            foreach (uint entityId in timelines.Keys)
            {
                destination.Add(entityId);
            }
        }

        public void Clear()
        {
            timelines.Clear();
            lastSequences.Clear();
            LocalEntityId = 0;
            LatestServerTick = 0;
            SnapshotVersion = 0;
            hasLatestServerTick = false;
        }
    }
}

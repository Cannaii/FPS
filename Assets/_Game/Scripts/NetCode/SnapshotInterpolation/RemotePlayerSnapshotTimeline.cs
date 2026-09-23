using System;
using UnityEngine;

namespace AFPS.NetCode.SnapshotInterpolation
{
    /// <summary>
    /// 使用连续服务器 Tick 时间为一个远端玩家生成延迟插值状态。
    /// 缓冲区耗尽时只允许有限时间的速度外推，之后冻结，避免持续漂移。
    /// </summary>
    public sealed class RemotePlayerSnapshotTimeline
    {
        private readonly RemotePlayerSnapshotBuffer buffer;
        private readonly float tickDeltaTime;
        private readonly double interpolationDelayTicks;
        private readonly double maxExtrapolationTicks;
        private readonly float teleportDistance;

        public uint EntityId => buffer.EntityId;
        public int SnapshotCount => buffer.Count;
        public double InterpolationDelayTicks => interpolationDelayTicks;

        public RemotePlayerSnapshotTimeline(uint entityId, int capacity, float tickDeltaTime, double interpolationDelayTicks, double maxExtrapolationTicks, float teleportDistance)
        {
            if (tickDeltaTime <= 0f || float.IsNaN(tickDeltaTime) || float.IsInfinity(tickDeltaTime))
            {
                throw new ArgumentOutOfRangeException(nameof(tickDeltaTime));
            }

            if (interpolationDelayTicks < 0.0 || double.IsNaN(interpolationDelayTicks) || double.IsInfinity(interpolationDelayTicks))
            {
                throw new ArgumentOutOfRangeException(nameof(interpolationDelayTicks));
            }

            if (maxExtrapolationTicks < 0.0 || double.IsNaN(maxExtrapolationTicks) || double.IsInfinity(maxExtrapolationTicks))
            {
                throw new ArgumentOutOfRangeException(nameof(maxExtrapolationTicks));
            }

            if (teleportDistance <= 0f || float.IsNaN(teleportDistance) || float.IsInfinity(teleportDistance))
            {
                throw new ArgumentOutOfRangeException(nameof(teleportDistance));
            }

            buffer = new RemotePlayerSnapshotBuffer(entityId, capacity);
            this.tickDeltaTime = tickDeltaTime;
            this.interpolationDelayTicks = interpolationDelayTicks;
            this.maxExtrapolationTicks = maxExtrapolationTicks;
            this.teleportDistance = teleportDistance;
        }

        public SnapshotBufferInsertResult Insert(in RemotePlayerSnapshot snapshot) => buffer.Insert(snapshot);

        public bool TrySample(double estimatedServerTick, out RemotePlayerRenderState state)
        {
            state = default;
            if (buffer.Count == 0 || estimatedServerTick < 0.0 || double.IsNaN(estimatedServerTick) || double.IsInfinity(estimatedServerTick))
            {
                return false;
            }

            double targetTick = estimatedServerTick - interpolationDelayTicks;
            RemotePlayerSnapshot newest = buffer.GetAt(buffer.Count - 1);
            double estimatedWholeTick = Math.Floor(estimatedServerTick);
            uint estimatedRawTick = unchecked((uint)(long)estimatedWholeTick);
            double newestTickTime = estimatedWholeTick + unchecked((int)(newest.ServerTick - estimatedRawTick));
            RemotePlayerSnapshot oldest = buffer.GetAt(0);
            double oldestTickTime = newestTickTime + unchecked((int)(oldest.ServerTick - newest.ServerTick));

            if (targetTick <= oldestTickTime)
            {
                state = FromSnapshot(oldest, targetTick, RemotePlayerSamplingStatus.Held);
                return true;
            }

            for (int i = 1; i < buffer.Count; i++)
            {
                RemotePlayerSnapshot before = buffer.GetAt(i - 1);
                RemotePlayerSnapshot after = buffer.GetAt(i);
                double beforeTickTime = newestTickTime + unchecked((int)(before.ServerTick - newest.ServerTick));
                double afterTickTime = newestTickTime + unchecked((int)(after.ServerTick - newest.ServerTick));
                if (targetTick > afterTickTime)
                {
                    continue;
                }

                bool teleport = after.IsTeleport || Vector3.Distance(before.Position, after.Position) >= teleportDistance;
                if (targetTick >= afterTickTime)
                {
                    state = FromSnapshot(after, targetTick, teleport ? RemotePlayerSamplingStatus.Teleported : RemotePlayerSamplingStatus.Interpolated);
                    return true;
                }

                if (teleport)
                {
                    state = FromSnapshot(before, targetTick, RemotePlayerSamplingStatus.HeldBeforeTeleport);
                    return true;
                }

                float interpolation = (float)((targetTick - beforeTickTime) / (afterTickTime - beforeTickTime));
                state = new RemotePlayerRenderState(targetTick, Vector3.LerpUnclamped(before.Position, after.Position, interpolation), Quaternion.SlerpUnclamped(before.Rotation, after.Rotation, interpolation), Vector3.LerpUnclamped(before.Velocity, after.Velocity, interpolation), RemotePlayerSamplingStatus.Interpolated);
                return true;
            }

            double requestedExtrapolationTicks = Math.Max(0.0, targetTick - newestTickTime);
            double appliedExtrapolationTicks = Math.Min(requestedExtrapolationTicks, maxExtrapolationTicks);
            Vector3 extrapolatedPosition = newest.Position + newest.Velocity * (float)(appliedExtrapolationTicks * tickDeltaTime);
            RemotePlayerSamplingStatus extrapolationStatus = requestedExtrapolationTicks <= 0.0 ? RemotePlayerSamplingStatus.Held : requestedExtrapolationTicks <= maxExtrapolationTicks ? RemotePlayerSamplingStatus.Extrapolated : RemotePlayerSamplingStatus.ExtrapolationLimitReached;
            state = new RemotePlayerRenderState(targetTick, extrapolatedPosition, newest.Rotation, newest.Velocity, extrapolationStatus);
            return true;
        }

        private static RemotePlayerRenderState FromSnapshot(in RemotePlayerSnapshot snapshot, double targetTick, RemotePlayerSamplingStatus status)
        {
            return new RemotePlayerRenderState(targetTick, snapshot.Position, snapshot.Rotation, snapshot.Velocity, status);
        }
    }
}

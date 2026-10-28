using System;
using AFPS.Math;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.SnapshotInterpolation;

namespace AFPS.NetCode.StateReplication
{
    /// <summary>单个接收连接/实体的发送状态；只有传输成功才更新节拍与全量基线。</summary>
    public sealed class RemoteSnapshotSendState
    {
        private readonly int tickRate;
        private readonly int snapshotRate;
        private readonly long teleportDistanceRaw;
        private int remainder;
        private uint nextSendTick;
        private RemotePlayerSnapshot latest;

        public bool HasSent { get; private set; }
        public RemotePlayerSnapshot FullBaseline { get; private set; }

        /// <param name="tickRate">服务器每秒Tick数，必须为正。</param>
        /// <param name="snapshotRate">目标每秒快照数，必须为正，最高限制为每Tick一次。</param>
        /// <param name="teleportDistance">相对上次成功发送位置的大位移阈值，单位米，含边界。</param>
        public RemoteSnapshotSendState(int tickRate, int snapshotRate, Fix teleportDistance)
        {
            if (tickRate <= 0 || snapshotRate <= 0 || teleportDistance <= Fix.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(snapshotRate));
            }
            this.tickRate = tickRate;
            this.snapshotRate = System.Math.Min(snapshotRate, tickRate);
            teleportDistanceRaw = teleportDistance.Raw;
        }

        public bool IsDue(uint worldTick, FixVec3 position)
        {
            if (!HasSent)
            {
                return true;
            }
            // 一次世界Tick最多一包；过期调用不能倒退发送时钟，比较允许uint回绕。
            return SequenceMath.IsNewer(worldTick, latest.ServerTick)
                && (IsUrgent(position) || worldTick == nextSendTick
                    || SequenceMath.IsNewer(worldTick, nextSendTick));
        }

        public bool IsUrgent(FixVec3 position)
        {
            if (!HasSent)
            {
                return true;
            }
            // 先宽位相减并按轴筛选，再求平方，避免定点减法及极端坐标平方溢出。
            long x = (long)position.X.Raw - latest.PositionFixed.X.Raw;
            long y = (long)position.Y.Raw - latest.PositionFixed.Y.Raw;
            long z = (long)position.Z.Raw - latest.PositionFixed.Z.Raw;
            long limit = teleportDistanceRaw;
            if (x >= limit || x <= -limit || y >= limit || y <= -limit || z >= limit || z <= -limit)
            {
                return true;
            }
            return (ulong)(x * x) + (ulong)(y * y) + (ulong)(z * z) >= (ulong)(limit * limit);
        }

        public void RecordSuccessfulSend(in RemotePlayerSnapshot snapshot, bool isFull)
        {
            if ((!HasSent && !isFull) || (HasSent && (snapshot.EntityId != latest.EntityId
                || !SequenceMath.IsNewer(snapshot.ServerTick, latest.ServerTick))))
            {
                throw new InvalidOperationException("快照首包必须全量，后续成功发送必须保持实体和世界Tick顺序。");
            }
            // 50/20使用2、3 Tick交替，不向上取整成16.7Hz。失败不进入这里；迟发后从当前Tick
            // 重新排期，不补发积压包。紧急全量同样重排，避免下一Tick无意义重复发送。
            long numerator = (long)tickRate + remainder;
            uint interval = (uint)(numerator / snapshotRate);
            remainder = (int)(numerator % snapshotRate);
            nextSendTick = unchecked(snapshot.ServerTick + interval);
            latest = snapshot;
            HasSent = true;
            if (isFull)
            {
                FullBaseline = snapshot;
            }
        }
    }
}

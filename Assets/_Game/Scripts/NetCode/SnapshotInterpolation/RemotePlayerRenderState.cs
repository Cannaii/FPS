using UnityEngine;

namespace AFPS.NetCode.SnapshotInterpolation
{
    public enum RemotePlayerSamplingStatus
    {
        /// <summary>目标时间早于已有快照，或恰好停留在最新快照。</summary>
        Held,

        /// <summary>目标时间位于两个连续快照之间。</summary>
        Interpolated,

        /// <summary>尚无更新快照，正在允许窗口内按最新速度短时外推。</summary>
        Extrapolated,

        /// <summary>缺失快照时间超过外推上限，显示位置已冻结。</summary>
        ExtrapolationLimitReached,

        /// <summary>目标时间尚未到达传送快照，因此保持传送前的位置。</summary>
        HeldBeforeTeleport,

        /// <summary>目标时间已经到达传送快照，表现层应立即跳转。</summary>
        Teleported
    }

    /// <summary>
    /// 远端玩家表现层在一个渲染帧应使用的状态，不会参与本地玩家预测模拟。
    /// </summary>
    public readonly struct RemotePlayerRenderState
    {
        /// <summary>本状态采样的连续服务器 Tick 时间，可以包含小数相位和回绕展开值。</summary>
        public readonly double ServerTickTime;

        /// <summary>远端玩家在该采样时间的世界位置，单位为米。</summary>
        public readonly Vector3 Position;

        /// <summary>远端玩家在该采样时间的世界旋转。</summary>
        public readonly Quaternion Rotation;

        /// <summary>远端玩家在该采样时间的速度，单位为米每秒。</summary>
        public readonly Vector3 Velocity;

        /// <summary>本次结果采用插值、外推、冻结还是传送处理。</summary>
        public readonly RemotePlayerSamplingStatus Status;

        public RemotePlayerRenderState(double serverTickTime, Vector3 position, Quaternion rotation, Vector3 velocity, RemotePlayerSamplingStatus status)
        {
            ServerTickTime = serverTickTime;
            Position = position;
            Rotation = rotation;
            Velocity = velocity;
            Status = status;
        }
    }
}

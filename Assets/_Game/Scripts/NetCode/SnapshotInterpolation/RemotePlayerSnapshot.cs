using UnityEngine;

namespace AFPS.NetCode.SnapshotInterpolation
{
    /// <summary>
    /// 服务器为一个远端玩家生成的世界状态快照。
    /// ServerTick 属于服务器世界时间；位置单位为米，速度单位为米每秒，旋转为世界空间朝向。
    /// </summary>
    public readonly struct RemotePlayerSnapshot
    {
        /// <summary>
        /// 服务器分配的实体 ID；0 表示无效实体。
        /// </summary>
        public readonly uint EntityId;

        /// <summary>
        /// 服务器生成该快照时已经完成的世界 Tick，允许 uint 自然回绕。
        /// </summary>
        public readonly uint ServerTick;

        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly Vector3 Velocity;

        /// <summary>
        /// 服务器明确要求客户端在该快照处中断插值，例如重生或传送。
        /// </summary>
        public readonly bool IsTeleport;

        public RemotePlayerSnapshot(uint entityId, uint serverTick, Vector3 position, Quaternion rotation, Vector3 velocity, bool isTeleport = false)
        {
            EntityId = entityId;
            ServerTick = serverTick;
            Position = position;
            Rotation = rotation;
            Velocity = velocity;
            IsTeleport = isTeleport;
        }
    }
}

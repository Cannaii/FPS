using AFPS.Math;

namespace AFPS.NetCode.SnapshotInterpolation
{
    /// <summary>远端玩家的规范网络状态；位置米、速度米/秒、观察角度数，均为 Q16.16。</summary>
    public readonly partial struct RemotePlayerSnapshot
    {
        public readonly uint EntityId;
        /// <summary>已完成的服务器世界 Tick，允许 uint 自然回绕。</summary>
        public readonly uint ServerTick;
        public readonly FixVec3 PositionFixed;
        public readonly FixVec3 VelocityFixed;
        public readonly Fix YawFixedDegrees;
        public readonly Fix PitchFixedDegrees;
        public readonly bool IsTeleport;

        // 与玩家输入的角域一致：Yaw [0,360)，Pitch [-89.9,89.9]；边界采用有理数向零截断。
        internal const int FullTurnRaw = 360 * 65536;
        internal const int MaximumPitchRaw = 899 * 65536 / 10;
        internal bool IsValid => EntityId != 0
            && YawFixedDegrees.Raw >= 0 && YawFixedDegrees.Raw < FullTurnRaw
            && PitchFixedDegrees.Raw >= -MaximumPitchRaw
            && PitchFixedDegrees.Raw <= MaximumPitchRaw;

        public RemotePlayerSnapshot(
            uint entityId,
            uint serverTick,
            FixVec3 position,
            Fix yawDegrees,
            Fix pitchDegrees,
            FixVec3 velocity,
            bool isTeleport = false)
        {
            EntityId = entityId;
            ServerTick = serverTick;
            PositionFixed = position;
            VelocityFixed = velocity;
            YawFixedDegrees = yawDegrees;
            PitchFixedDegrees = pitchDegrees;
            IsTeleport = isTeleport;
        }

        public bool RawEquals(in RemotePlayerSnapshot other) =>
            EntityId == other.EntityId && ServerTick == other.ServerTick
            && PositionFixed == other.PositionFixed && VelocityFixed == other.VelocityFixed
            && YawFixedDegrees == other.YawFixedDegrees
            && PitchFixedDegrees == other.PitchFixedDegrees
            && IsTeleport == other.IsTeleport;
    }
}
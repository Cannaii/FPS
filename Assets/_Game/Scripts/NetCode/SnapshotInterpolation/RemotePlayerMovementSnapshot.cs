using AFPS.Math;

namespace AFPS.NetCode.SnapshotInterpolation
{
    /// <summary>远端玩家的规范网络状态；位置米、速度米/秒、观察角度数，均为 Q16.16。</summary>
    public readonly partial struct RemotePlayerMovementSnapshot
    {
        public readonly uint EntityId;
        /// <summary>已完成的服务器世界 Tick，允许 uint 自然回绕。</summary>
        public readonly uint ServerTick;
        public readonly FixVec3 PositionFixed;
        public readonly FixVec3 VelocityFixed;
        public readonly Fix YawFixedDegrees;
        public readonly Fix PitchFixedDegrees;
        public readonly bool IsTeleport;
        public readonly bool IsGrounded;
        public readonly bool IsCrouched;
        public readonly bool IsGrappling;
        public readonly FixVec3 GrapplePointFixed; // 服务器锚点，世界米Raw；未挂钩时为零。
        // bit0传送，bit1落地，bit2下蹲，bit3挂钩；离散状态不能从速度猜测。
        internal byte StateFlags => (byte)((IsTeleport ? 1 : 0) | (IsGrounded ? 2 : 0)
            | (IsCrouched ? 4 : 0) | (IsGrappling ? 8 : 0));

        // 与玩家输入的角域一致：Yaw [0,360)，Pitch [-89.9,89.9]；边界采用有理数向零截断。
        internal const int FullTurnRaw = 360 * 65536;
        internal const int MaximumPitchRaw = 899 * 65536 / 10;
        internal bool IsValid => EntityId != 0
            && YawFixedDegrees.Raw >= 0 && YawFixedDegrees.Raw < FullTurnRaw
            && PitchFixedDegrees.Raw >= -MaximumPitchRaw
            && PitchFixedDegrees.Raw <= MaximumPitchRaw
            && (IsGrappling
                ? !IsCrouched && IsGrappleSpanValid()
                : GrapplePointFixed == FixVec3.Zero);

        private bool IsGrappleSpanValid()
        {
            // 仅约束表现协议接收域，覆盖当前256米最大绳长；先用long减法筛选，避免坏包溢出。
            const long limit = 256L * 65536;
            long x = (long)GrapplePointFixed.X.Raw - PositionFixed.X.Raw;
            long y = (long)GrapplePointFixed.Y.Raw - PositionFixed.Y.Raw;
            long z = (long)GrapplePointFixed.Z.Raw - PositionFixed.Z.Raw;
            return x >= -limit && x <= limit && y >= -limit && y <= limit && z >= -limit && z <= limit
                && (ulong)(x * x) + (ulong)(y * y) + (ulong)(z * z) <= (ulong)(limit * limit);
        }

        public RemotePlayerMovementSnapshot(
            uint entityId,
            uint serverTick,
            FixVec3 position,
            Fix yawDegrees,
            Fix pitchDegrees,
            FixVec3 velocity,
            bool isTeleport = false,
            bool isGrounded = false,
            bool isCrouched = false,
            bool isGrappling = false,
            FixVec3 grapplePoint = default)
        {
            EntityId = entityId;
            ServerTick = serverTick;
            PositionFixed = position;
            VelocityFixed = velocity;
            YawFixedDegrees = yawDegrees;
            PitchFixedDegrees = pitchDegrees;
            IsTeleport = isTeleport;
            IsGrounded = isGrounded;
            IsCrouched = isCrouched;
            IsGrappling = isGrappling;
            GrapplePointFixed = grapplePoint;
        }

        public bool RawEquals(in RemotePlayerMovementSnapshot other) =>
            EntityId == other.EntityId && ServerTick == other.ServerTick
            && PositionFixed == other.PositionFixed && VelocityFixed == other.VelocityFixed
            && YawFixedDegrees == other.YawFixedDegrees
            && PitchFixedDegrees == other.PitchFixedDegrees
            && IsTeleport == other.IsTeleport && IsGrounded == other.IsGrounded && IsCrouched == other.IsCrouched
            && IsGrappling == other.IsGrappling && GrapplePointFixed == other.GrapplePointFixed;
    }
}

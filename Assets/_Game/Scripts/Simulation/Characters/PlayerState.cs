using AFPS.Math;

namespace AFPS.Simulation.Characters
{
    /// <summary>输入 Tick 完成后的定点状态：位置米、速度米/秒、观察角度数。</summary>
    public partial struct PlayerState
    {
        public uint Tick;
        public FixVec3 PositionFixed;
        public FixVec3 VelocityFixed;
        public Fix YawFixedDegrees;
        public Fix PitchFixedDegrees;
        public bool IsGrounded;
        /// <summary>已消耗体力 [0,1]；零初始化表示满体力，出生/重生不需要额外初始化哨兵。</summary>
        public Fix StaminaSpentFixed;
        public bool IsSprinting;
        /// <summary>耗尽锁存，必须松开快跑并恢复到配置阈值才清除，避免低体力逐Tick启停。</summary>
        public bool SprintExhausted;
        public Fix DashRemainingFixed; // 当前Tick之后的冲刺剩余秒数，零表示结束。
        public FixVec3 DashVelocityFixed; // 触发时锁定的世界水平速度，Y恒为零；结束时清零。
        public int DashSpentCharges; // 已用充能；零初始化即满充能，不使用特殊出生哨兵。
        public Fix DashRechargeRemainingFixed; // 恢复下一次充能的剩余秒数；满充能时为零。
        public Fix CoyoteRemainingFixed; // 当前Tick后的离地宽限剩余秒数；零不允许宽限跳。
        public bool GroundJumpConsumed; // 主动起跳已消耗地面机会；防止再用宽限伪造地面跳。
        public int AirJumpsUsed; // 最近一次真实落地后已用的额外空中跳次数；零即全部可用。
        public FixVec3 WallNormalFixed; // 当前Tick结束的水平单位向外墙法线；零表示无墙。
        public FixVec3 WallIncomingVelocityFixed; // 当前Tick碰撞前速度，米/秒；无墙时清零。
        public Fix WallCooldownRemainingFixed; // 下一次墙能力可用前的剩余秒数；触发Tick结束保留完整冷却。
        public bool IsCrouched; // 当前碰撞姿态；可能因顶棚无法站起而保持，不等于按键电平。
        public Fix SlideRemainingFixed; // 本Tick后的滑铲剩余秒数，零表示结束。
        public FixVec3 SlideVelocityFixed; // 滑铲实际水平速度，米/秒，Y恒零；结束时清零。
        public bool IsWallRunning;
        public Fix WallRunUsedFixed; // 最近真实落地后的累计墙跑秒数；松键/换墙/传送不刷新。
        public bool IsClimbing;
        public uint ClimbShapeId; // 当前竖直梯盒的规范ShapeId；未攀爬时为零。
        public FixVec3 ClimbNormalFixed; // 当前梯面的水平向外单位法线；未攀爬时清零。
        public Fix ClimbCooldownRemainingFixed; // 脱梯后重挂禁止时间，秒。
        public bool IsGrappling;
        public bool GrappleConsumed; // 本次按住已经尝试发射；成功/失败/强制脱绳均需松开后重试。
        public FixVec3 GrapplePointFixed; // 固定世界锚点，米；未挂钩时清零。
        public Fix GrappleLengthFixed; // 脚底积分点到锚点的目标绳长，米。
        public uint GrappleShapeId; // 锚点的规范几何ShapeId；未挂钩时为零。
        public bool RawEquals(in PlayerState other) =>
            Tick == other.Tick
            && PositionFixed == other.PositionFixed
            && VelocityFixed == other.VelocityFixed
            && YawFixedDegrees == other.YawFixedDegrees
            && PitchFixedDegrees == other.PitchFixedDegrees
            && IsGrounded == other.IsGrounded
            && StaminaSpentFixed == other.StaminaSpentFixed
            && IsSprinting == other.IsSprinting && SprintExhausted == other.SprintExhausted
            && DashRemainingFixed == other.DashRemainingFixed
            && DashVelocityFixed == other.DashVelocityFixed
            && DashSpentCharges == other.DashSpentCharges
            && DashRechargeRemainingFixed == other.DashRechargeRemainingFixed
            && CoyoteRemainingFixed == other.CoyoteRemainingFixed
            && GroundJumpConsumed == other.GroundJumpConsumed
            && AirJumpsUsed == other.AirJumpsUsed
            && WallNormalFixed == other.WallNormalFixed
            && WallIncomingVelocityFixed == other.WallIncomingVelocityFixed
            && WallCooldownRemainingFixed == other.WallCooldownRemainingFixed
            && IsCrouched == other.IsCrouched
            && SlideRemainingFixed == other.SlideRemainingFixed
            && SlideVelocityFixed == other.SlideVelocityFixed
            && IsWallRunning == other.IsWallRunning
            && WallRunUsedFixed == other.WallRunUsedFixed
            && IsClimbing == other.IsClimbing && ClimbShapeId == other.ClimbShapeId
            && ClimbNormalFixed == other.ClimbNormalFixed
            && ClimbCooldownRemainingFixed == other.ClimbCooldownRemainingFixed
            && IsGrappling == other.IsGrappling && GrappleConsumed == other.GrappleConsumed
            && GrapplePointFixed == other.GrapplePointFixed && GrappleLengthFixed == other.GrappleLengthFixed
            && GrappleShapeId == other.GrappleShapeId;
    }
}

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
            && DashRechargeRemainingFixed == other.DashRechargeRemainingFixed;
    }
}

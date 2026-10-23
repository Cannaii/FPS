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
        /// <summary>耗尽锁存，必须松开冲刺并恢复到配置阈值才清除，避免低体力逐Tick启停。</summary>
        public bool SprintExhausted;
        public bool RawEquals(in PlayerState other) =>
            Tick == other.Tick
            && PositionFixed == other.PositionFixed
            && VelocityFixed == other.VelocityFixed
            && YawFixedDegrees == other.YawFixedDegrees
            && PitchFixedDegrees == other.PitchFixedDegrees
            && IsGrounded == other.IsGrounded
            && StaminaSpentFixed == other.StaminaSpentFixed
            && IsSprinting == other.IsSprinting && SprintExhausted == other.SprintExhausted;
    }
}

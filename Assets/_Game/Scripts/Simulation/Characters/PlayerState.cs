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
        public bool RawEquals(in PlayerState other) =>
            Tick == other.Tick
            && PositionFixed == other.PositionFixed
            && VelocityFixed == other.VelocityFixed
            && YawFixedDegrees == other.YawFixedDegrees
            && PitchFixedDegrees == other.PitchFixedDegrees
            && IsGrounded == other.IsGrounded;
    }
}

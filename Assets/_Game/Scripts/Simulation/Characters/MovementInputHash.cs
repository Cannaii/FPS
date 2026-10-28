namespace AFPS.Simulation.Characters
{
    /// <summary>仅诊断PlayerSimulation实际消费的规范输入；开火/换弹不改变移动状态，不纳入此指纹。</summary>
    public static class MovementInputHash
    {
        public static ulong Compute(in PlayerInputCommand command)
        {
            ulong hash = 14695981039346656037UL;
            Add(ref hash, command.Tick);
            Add(ref hash, unchecked((uint)command.MoveXGrid));
            Add(ref hash, unchecked((uint)command.MoveYGrid));
            Add(ref hash, command.YawCentidegrees);
            Add(ref hash, unchecked((uint)command.PitchCentidegrees));
            // 新增参与移动模拟的输入字段时必须扩展该域；不要用GetHashCode或浮点视图。
            uint flags = (command.JumpPressed ? 1u : 0) | (command.SprintHeld ? 2u : 0)
                | (command.DashPressed ? 4u : 0) | (command.WallBouncePressed ? 8u : 0)
                | (command.CrouchHeld ? 16u : 0) | (command.GrappleHeld ? 32u : 0);
            Add(ref hash, flags);
            return hash;
        }

        private static void Add(ref ulong hash, uint value)
        {
            for (int shift = 0; shift < 32; shift += 8)
            {
                hash = unchecked((hash ^ (byte)(value >> shift)) * 1099511628211UL);
            }
        }
    }
}

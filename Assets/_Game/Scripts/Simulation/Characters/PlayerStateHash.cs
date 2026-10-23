namespace AFPS.Simulation.Characters
{
    /// <summary>覆盖当前全部模拟状态 Raw 的 FNV-1a 64；不使用 Unity 或运行时对象哈希。</summary>
    public static class PlayerStateHash
    {
        public static ulong Compute(in PlayerState state)
        {
            ulong hash = 14695981039346656037UL;
            Add(ref hash, unchecked((int)state.Tick));
            Add(ref hash, state.PositionFixed.X.Raw);
            Add(ref hash, state.PositionFixed.Y.Raw);
            Add(ref hash, state.PositionFixed.Z.Raw);
            Add(ref hash, state.VelocityFixed.X.Raw);
            Add(ref hash, state.VelocityFixed.Y.Raw);
            Add(ref hash, state.VelocityFixed.Z.Raw);
            Add(ref hash, state.YawFixedDegrees.Raw);
            Add(ref hash, state.PitchFixedDegrees.Raw);
            Add(ref hash, state.IsGrounded ? 1 : 0);
            Add(ref hash, state.StaminaSpentFixed.Raw);
            Add(ref hash, state.IsSprinting ? 1 : 0);
            Add(ref hash, state.SprintExhausted ? 1 : 0);
            Add(ref hash, state.DashRemainingFixed.Raw);
            Add(ref hash, state.DashVelocityFixed.X.Raw);
            Add(ref hash, state.DashVelocityFixed.Y.Raw);
            Add(ref hash, state.DashVelocityFixed.Z.Raw);
            Add(ref hash, state.DashSpentCharges);
            Add(ref hash, state.DashRechargeRemainingFixed.Raw);
            return hash;
        }

        private static void Add(ref ulong hash, int raw)
        {
            uint bits = unchecked((uint)raw);
            for (int shift = 0; shift < 32; shift += 8)
            {
                hash = unchecked((hash ^ ((bits >> shift) & 255u)) * 1099511628211UL);
            }
        }
    }
}

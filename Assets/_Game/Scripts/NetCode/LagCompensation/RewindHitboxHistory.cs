using System;
using AFPS.NetCode.Weapons;

namespace AFPS.NetCode.LagCompensation
{
    /// <summary>按服务器 Tick 保存单个实体的独立胶囊历史，并拒绝超出回溯窗口的查询。</summary>
    public sealed class RewindHitboxHistory
    {
        private readonly uint[] ticks;
        private readonly ServerCombatTarget[] targets;
        private int nextIndex;
        private int count;

        public int Count => count;

        public RewindHitboxHistory(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }
            ticks = new uint[capacity];
            targets = new ServerCombatTarget[capacity];
        }

        /// <summary>隔离不同生命的历史；清空后旧 Tick 查询必须失败，直到记录新生命。</summary>
        public void Clear()
        {
            nextIndex = 0;
            count = 0;
        }

        public void Record(uint serverTick, in ServerCombatTarget target)
        {
            ticks[nextIndex] = serverTick;
            // 值类型保存完整 Raw 快照，不经 Unity/float，也不引用可变玩家状态。
            targets[nextIndex] = target;
            nextIndex = (nextIndex + 1) % ticks.Length;
            if (count < ticks.Length)
            {
                count++;
            }
        }

        public bool TryGetAtOrBefore(uint requestedTick, uint currentTick, uint maximumRewindTicks, out ServerCombatTarget target, out uint sampledTick)
        {
            target = default;
            sampledTick = default;
            uint requestedAge = unchecked(currentTick - requestedTick);
            if (requestedAge > maximumRewindTicks)
            {
                return false;
            }

            bool found = false;
            uint bestExtraAge = uint.MaxValue;
            for (int i = 0; i < count; i++)
            {
                uint age = unchecked(currentTick - ticks[i]);
                if (age > maximumRewindTicks || age < requestedAge)
                {
                    continue;
                }

                uint extraAge = age - requestedAge;
                if (!found || extraAge < bestExtraAge)
                {
                    found = true;
                    bestExtraAge = extraAge;
                    target = targets[i];
                    sampledTick = ticks[i];
                }
            }

            return found;
        }
    }
}

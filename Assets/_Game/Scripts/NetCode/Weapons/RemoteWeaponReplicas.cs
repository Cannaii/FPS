using System;
using System.Collections.Generic;
using AFPS.NetCode.Protocol;

namespace AFPS.NetCode.Weapons
{
    /// <summary>远端装备的有界离散时间线；与移动共享显示时刻，不插值武器编号或提前播放换弹。</summary>
    public sealed class RemoteWeaponReplicas
    {
        private const int MaximumEntities = 64;
        private const int HistoryCapacity = 32;
        private sealed class History
        {
            public readonly List<RemoteWeaponState> States = new List<RemoteWeaponState>(HistoryCapacity);
            public uint Sequence;
        }
        private readonly Dictionary<uint, History> histories = new Dictionary<uint, History>();
        public int Count => histories.Count;

        public bool TryInsert(in RemoteWeaponState state, uint sequence)
        {
            if (!state.IsValid)
            {
                return false;
            }
            if (!histories.TryGetValue(state.EntityId, out History history))
            {
                if (histories.Count == MaximumEntities)
                {
                    return false;
                }
                history = new History();
                histories.Add(state.EntityId, history);
            }
            else
            {
                RemoteWeaponState previous = history.States[history.States.Count - 1];
                if (!SequenceMath.IsNewer(sequence, history.Sequence)
                    || SequenceMath.IsOlder(state.ServerTick, previous.ServerTick))
                {
                    return false;
                }
                if (state.ServerTick == previous.ServerTick)
                {
                    history.States.RemoveAt(history.States.Count - 1);
                }
            }
            if (history.States.Count == HistoryCapacity)
            {
                history.States.RemoveAt(0);
            }
            history.States.Add(state);
            history.Sequence = sequence;
            return true;
        }

        public bool TrySample(uint entityId, double renderTick, out RemoteWeaponState state)
        {
            state = default;
            if (double.IsNaN(renderTick) || double.IsInfinity(renderTick)
                || renderTick < 0d || renderTick >= long.MaxValue
                || !histories.TryGetValue(entityId, out History history))
            {
                return false;
            }
            double wholeTick = System.Math.Floor(renderTick);
            uint rawTick = unchecked((uint)(long)wholeTick);
            for (int i = history.States.Count - 1; i >= 0; i--)
            {
                RemoteWeaponState candidate = history.States[i];
                double candidateTick = wholeTick + unchecked((int)(candidate.ServerTick - rawTick));
                if (candidateTick <= renderTick)
                {
                    // 武器流单独断更超过1秒（50Hz）时不继续保持旧换弹/装备姿态。
                    if (renderTick - candidateTick > 50d)
                    {
                        return false;
                    }
                    state = candidate;
                    return true;
                }
            }
            return false;
        }

        public void Remove(uint entityId) => histories.Remove(entityId);
        public void Clear() => histories.Clear();
    }
}

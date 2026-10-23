using System.Collections.Generic;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Protocol;
using AFPS.Simulation.Characters;

namespace AFPS.NetCode.Weapons
{
    /// <summary>只预测显示与动作许可，不重放命中；权威快照确认输入后重算未确认动作，避免延迟包覆盖本地扣弹。</summary>
    public sealed class ClientWeaponPrediction
    {
        private const int MaximumPendingActions = 256;
        private readonly List<PlayerInputCommand> pending = new List<PlayerInputCommand>();
        private bool overflowed;
        private uint latestObservedTick;
        public bool HasState { get; private set; }
        public bool IsSwitchPending
        {
            get
            {
                foreach (var input in pending)
                {
                    if (input.SwitchWeaponPressed)
                    {
                        return true;
                    }
                }
                return false;
            }
        }
        public AuthoritativeWeaponState State { get; private set; }
        public int Ammo
        {
            get
            {
                int result = State.Ammo;
                foreach (PlayerInputCommand input in pending)
                {
                    if (input.FirePressed && result > 0)
                    {
                        result--;
                    }
                }
                return result;
            }
        }
        public bool IsReloading
        {
            get
            {
                if (State.IsReloading)
                {
                    return true;
                }
                foreach (PlayerInputCommand input in pending)
                {
                    if (input.ReloadPressed)
                    {
                        return true;
                    }
                }
                return false;
            }
        }
        public bool CanFire => HasState && State.IsEquipped && !State.IsEquipping && !IsSwitchPending && !IsReloading && Ammo > 0 && !overflowed;
        public bool CanReload => HasState && State.IsEquipped && !State.IsEquipping && !IsSwitchPending && !IsReloading && Ammo < State.Capacity && !overflowed;

        public bool Apply(in AuthoritativeWeaponState state)
        {
            if (!AuthoritativeWeaponStateCodec.IsValid(state)
                || (HasState && SequenceMath.IsNewer(State.AckInputTick, state.AckInputTick)))
            {
                return false;
            }
            State = state;
            HasState = true;
            uint acknowledged = state.AckInputTick;
            pending.RemoveAll(input => !SequenceMath.IsNewer(input.Tick, acknowledged));
            overflowed &= SequenceMath.IsNewer(latestObservedTick, state.AckInputTick);
            return true;
        }

        public void Observe(in PlayerInputCommand input)
        {
            if (!input.FirePressed && !input.ReloadPressed && !input.SwitchWeaponPressed)
            {
                return;
            }
            latestObservedTick = input.Tick;
            if (pending.Count >= MaximumPendingActions)
            {
                // 极端断流时保守禁止继续预测，而不是无限积累内存或遗忘未确认扣弹。
                overflowed = true;
                return;
            }
            pending.Add(input);
        }

        public void Clear()
        {
            pending.Clear();
            HasState = false;
            State = default;
            overflowed = false;
        }
    }
}

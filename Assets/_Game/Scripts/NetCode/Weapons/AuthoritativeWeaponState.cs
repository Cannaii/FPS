namespace AFPS.NetCode.Weapons
{
    /// <summary>拥有者武器权威状态；AckInputTick 用于消除已结算的本地动作，剩余换弹时间单位为服务器 Tick。</summary>
    public readonly struct AuthoritativeWeaponState
    {
        public readonly uint EntityId;
        public readonly uint ServerTick;
        public readonly uint AckInputTick;
        public readonly int Ammo;
        public readonly int Capacity;
        public readonly uint ReloadTicksRemaining;
        public readonly bool IsEquipped;
        public bool IsReloading => ReloadTicksRemaining != 0;

        public AuthoritativeWeaponState(uint entityId, uint serverTick, uint ackInputTick,
            int ammo, int capacity, uint reloadTicksRemaining, bool isEquipped)
        {
            EntityId = entityId;
            ServerTick = serverTick;
            AckInputTick = ackInputTick;
            Ammo = ammo;
            Capacity = capacity;
            ReloadTicksRemaining = reloadTicksRemaining;
            IsEquipped = isEquipped;
        }
    }
}

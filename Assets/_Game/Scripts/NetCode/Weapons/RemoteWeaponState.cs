using AFPS.Simulation.Weapons;

namespace AFPS.NetCode.Weapons
{
    /// <summary>远端只读装备状态，不公开弹量或输入确认；所有计时均为服务器世界Tick。</summary>
    public readonly struct RemoteWeaponState
    {
        public readonly uint EntityId;
        public readonly uint ServerTick;
        public readonly ushort WeaponId;
        public readonly uint ReloadTicksRemaining;
        public readonly uint EquipTicksRemaining;
        public readonly bool IsEquipped;

        public bool IsValid => EntityId != 0 && WeaponId != 0
            && ReloadTicksRemaining <= ServerWeaponFireController.ReloadDurationTicks
            && EquipTicksRemaining <= WeaponCatalog.EquipDurationTicks
            && (IsEquipped || (ReloadTicksRemaining == 0 && EquipTicksRemaining == 0))
            && (ReloadTicksRemaining == 0 || EquipTicksRemaining == 0);

        public RemoteWeaponState(
            uint entityId,
            uint serverTick,
            ushort weaponId,
            uint reloadTicksRemaining,
            uint equipTicksRemaining,
            bool isEquipped)
        {
            EntityId = entityId;
            ServerTick = serverTick;
            WeaponId = weaponId;
            ReloadTicksRemaining = reloadTicksRemaining;
            EquipTicksRemaining = equipTicksRemaining;
            IsEquipped = isEquipped;
        }

        public bool SamePresentation(in RemoteWeaponState other) => WeaponId == other.WeaponId
            && ReloadTicksRemaining == other.ReloadTicksRemaining
            && EquipTicksRemaining == other.EquipTicksRemaining && IsEquipped == other.IsEquipped;
    }
}

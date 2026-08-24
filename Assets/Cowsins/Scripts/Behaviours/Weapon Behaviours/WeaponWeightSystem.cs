using UnityEngine;

namespace cowsins
{
    public class WeaponWeightSystem
    {
        private WeaponContext context;
        private IWeaponReferenceProvider weaponReference;
        private IWeaponEventsProvider weaponEvents;
        private IPlayerMultipliers playerMultipliers;

        private bool isAddonAvailable;

        private Weapon_SO weapon => weaponReference.Weapon;

        public WeaponWeightSystem(WeaponContext context)
        {
            this.context = context;
            this.weaponReference = context.Dependencies.WeaponReference;
            this.weaponEvents = context.Dependencies.WeaponEvents;
            this.playerMultipliers = context.Dependencies.PlayerMultipliers;

            weaponEvents.Events.OnUnholster.AddListener(GetWeaponWeightModifierOnUnholster);
            weaponEvents.Events.OnReleaseWeapon.AddListener(GetWeaponWeightModifier);
            weaponEvents.Events.OnUnselectingWeapon.AddListener(GetWeaponWeightModifier);

            GetWeaponWeightModifier();
        }

        private void GetWeaponWeightModifierOnUnholster(bool prop, bool prop2) => GetWeaponWeightModifier();
        public void GetWeaponWeightModifier()
        {
            // Only apply the weight modification if the Inventory Pro Manager add-on is not available. If it is available, the weight of the player is calculated by the inventory
            if (isAddonAvailable) return;

            // Read from weaponReference.Id to ensure attachment modifiers are included
            if (weaponReference.Id != null)
                playerMultipliers.WeightMultiplier.BaseValue = weaponReference.Id.weightMultiplier;
            else
                playerMultipliers.WeightMultiplier.BaseValue = weapon != null ? weapon.weightMultiplier : 1;

        }
    }

}

using UnityEngine;

namespace cowsins
{
    public class SpreadSystem
    {
        private float spread; // Current spread in use. Changes to baseSpread or baseAimSpread depending on whether the player is aiming or not.

        public float Spread => spread;

        private WeaponContext context;
        private IWeaponReferenceProvider weaponReference;
        private IWeaponBehaviourProvider weaponBehaviour;

        public SpreadSystem(WeaponContext context)
        {
            this.context = context;
            this.weaponReference = context.Dependencies.WeaponReference;
            this.weaponBehaviour = context.Dependencies.WeaponBehaviour;

            var weaponEvents = context.Dependencies.WeaponEvents;
            weaponEvents.Events.OnUnholster.AddListener(UpdateSpreadOnUnholster);
            weaponEvents.Events.OnAiming.AddListener(UpdateSpreadOnAiming);
            weaponEvents.Events.OnAimStop.AddListener(UpdateSpreadOnAimStop);

            weaponEvents.Events.OnGetSpread += GetCurrentSpread;
        }

        public void UpdateSpreadOnUnholster(bool autoReloadProp, bool prop2) => UpdateSpread(weaponBehaviour.IsAiming);
        public void UpdateSpreadOnAiming() => UpdateSpread(true);
        public void UpdateSpreadOnAimStop() => UpdateSpread(false);
        public void UpdateSpread(bool isAiming)
        {
            float newSpread = isAiming ? weaponReference.Id.baseAimSpread : weaponReference.Id.baseSpread;
            UpdateSpread(newSpread);
        }
        public void UpdateSpread(float spread)
        {
            if (weaponReference.Weapon != null && weaponReference.Weapon.applyBulletSpread)
                this.spread = spread;
        }

        public float GetCurrentSpread() => spread;
    }

}
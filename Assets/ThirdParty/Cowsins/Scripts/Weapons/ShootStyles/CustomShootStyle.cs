using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System;

namespace cowsins
{
    /// <summary>
    /// When a weapon is set to Custom Shoot Style, its shootBehaviour in WeaponIdentification is assigned to this CustomShootStyle.
    /// This is done when the weapon is unholstered.
    /// </summary>
    public class CustomShootStyle : IShootStyle
    {
        private PlayerDependencies playerDependencies;
        private Weapon_SO weapon;
        private WeaponIdentification id;
        private WeaponControllerSettings settings;

        private UnityEvent customMethod;

        private bool canShoot = true;

        // Calls events in Weapon Controller when shooting or hitting an enemy ( CustomShootStyle does not handle hits so onHit is not used here,
        // but it is still required by IShootStyle.
        public event Action onShoot;
#pragma warning disable CS0067
        public event Action<int, float, RaycastHit, bool> onHit;
#pragma warning restore CS0067

        public CustomShootStyle(PlayerDependencies playerDependencies, WeaponControllerSettings settings)
        {
            this.playerDependencies = playerDependencies;
            this.weapon = playerDependencies.WeaponReference.Weapon;
            this.id = playerDependencies.WeaponReference.Id;
            this.settings = settings;

            SelectCustomShotMethod();
        }

        public void Shoot(float spread, float damageMultiplier, float shakeMultiplier)
        {
            if (!canShoot) return;

            customMethod?.Invoke();
            onShoot?.Invoke();

            if (!weapon.continuousFire)
            {
                canShoot = false;
                playerDependencies.StartCoroutine(AllowShootAfterDelay(id.fireRate));
            }
        }

        private void SelectCustomShotMethod()
        {
            // Iterate through each item in the array
            for (int i = 0; i < settings.customPrimaryShot.Length; i++)
            {
                // Assign the on shoot event to the unity event to call it each time we fire
                if (settings.customPrimaryShot[i].weapon == weapon)
                {
                    customMethod = settings.customPrimaryShot[i].OnShoot;
                    return;
                }
            }

            CowsinsUtilities.LogError("<b><color=yellow>Appropriate weapon ScriptableObject not found!</color></b> " +
                "Please configure the <b><color=cyan>Weapon ScriptableObject</color></b> and assign a suitable method " +
                "in the <b><color=green>Custom Shot Array (Events tab)</color></b> to fix this error.");
        }


        private IEnumerator AllowShootAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            canShoot = true;
        }

        public void SetOnShootEvent(Action @event) => onShoot = @event;
    }
}
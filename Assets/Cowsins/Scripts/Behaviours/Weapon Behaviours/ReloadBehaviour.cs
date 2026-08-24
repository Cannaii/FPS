using UnityEngine;
using System.Collections;

namespace cowsins
{
    public class ReloadBehaviour
    {
        private WeaponContext context;
        private InputManager inputManager;
        private IWeaponBehaviourProvider weaponBehaviour;
        private IWeaponReferenceProvider weaponReference;
        private IWeaponEventsProvider weaponEvents;
        private IPlayerMovementStateProvider playerMovement;

        private float coolSpeed;
        private WeaponControllerSettings settings;

        private Weapon_SO weapon => weaponReference.Weapon;
        private WeaponIdentification id => weaponReference.Id;
        private Camera mainCamera => weaponReference.MainCamera;
        private Transform weaponHolder => context.WeaponHolder;
        private AudioSource audioSource => ((WeaponController)weaponReference).AudioSource;

        private delegate IEnumerator Reload();

        private Reload reload;

        private Coroutine activeReloadCoroutine;
        private bool reloadAnimationStarted = false;

        public ReloadBehaviour(WeaponContext context)
        {
            this.context = context;
            this.inputManager = context.InputManager;
            this.weaponBehaviour = context.Dependencies.WeaponBehaviour;
            this.weaponReference = context.Dependencies.WeaponReference;
            this.weaponEvents = context.Dependencies.WeaponEvents;
            this.playerMovement = context.Dependencies.PlayerMovementState;
            this.settings = context.Settings;

            weaponEvents.Events.OnEquipWeapon.AddListener(OnWeaponEquipped);
            weaponEvents.Events.OnSwitchingWeapon.AddListener(StopReload); // Stop reload when switching weapons
            weaponEvents.Events.OnReleaseWeapon.AddListener(StopReload);
            inputManager.OnShoot += CheckReloadCancellation;
            inputManager.OnDrop += CheckDropCancellation;
        }

        private void CheckDropCancellation()
        {
            if (weapon == null) return;
            if (weaponBehaviour.IsReloading && weapon.allowCancelReload)
            {
                StopReload();
            }
        }

        private void CheckReloadCancellation()
        {
            if (weapon == null || id == null) return;
            bool forcedAutoReload = settings.autoReload && id.bulletsLeftInMagazine <= 0;
            if (weaponBehaviour.IsReloading && weapon.allowCancelReload && !forcedAutoReload)
            {
                StopReload();
            }
        }

        public void StartReload() => activeReloadCoroutine = context.CoroutineRunner.StartCoroutine(reload());
        public void StopReload()
        {
            if(activeReloadCoroutine != null) 
            {
                if(context.CoroutineRunner != null) context.CoroutineRunner.StopCoroutine(activeReloadCoroutine);
                activeReloadCoroutine = null; 
            }
            if(audioSource != null && audioSource.isPlaying) 
            {
                audioSource.Stop();
            }
            if (reloadAnimationStarted)
            {
                weaponEvents.Events.OnCancelReload.Invoke();
                reloadAnimationStarted = false;
            }
            weaponBehaviour.IsReloading = false;
            context.CanShoot = true;
        }
        

        /// <summary>
        /// Handle Reloading
        /// </summary>
        public IEnumerator DefaultReload()
        {
            weaponBehaviour.IsReloading = true;
            // Run custom event
            settings.userEvents.onStartReload.Invoke();

            yield return new WaitForSeconds(settings.autoReload && id.bulletsLeftInMagazine <= 0 ? settings.autoReloadDelay : .1f);

            // Play reload sound
            if (audioSource != null)
            {
                audioSource.clip = id.bulletsLeftInMagazine == 0 ? weapon.audioSFX.emptyMagReload : weapon.audioSFX.reload;
                audioSource.pitch = 1f; 
                audioSource.PlayDelayed(.1f);
            }

            reloadAnimationStarted = true;
            weaponEvents.Events.OnStartReload.Invoke(); // ( Plays Animation in WeaponAnimator.cs )

            // Wait reloadTime seconds (use empty reload time if magazine is empty)
            float waitTime = id.bulletsLeftInMagazine == 0 ? id.emptyReloadTime : id.reloadTime;
            yield return new WaitForSeconds(waitTime);

            context.CanShoot = true;
            reloadAnimationStarted = false;
            if (!weaponBehaviour.IsReloading) yield break;

            weaponBehaviour.IsReloading = false;

            // Set the proper amount of bullets, depending on magazine type.
            if (weapon != null && !weapon.limitedMagazines) id.bulletsLeftInMagazine = id.magazineSize;
            else if (weapon != null)
            {
                if (id.totalBullets > id.magazineSize) // You can still reload a full magazine
                {
                    id.totalBullets = id.totalBullets - (id.magazineSize - id.bulletsLeftInMagazine);
                    id.bulletsLeftInMagazine = id.magazineSize;
                }
                else if (id.totalBullets == id.magazineSize) // You can only reload a single full magazine more
                {
                    id.totalBullets = id.totalBullets - (id.magazineSize - id.bulletsLeftInMagazine);
                    id.bulletsLeftInMagazine = id.magazineSize;
                }
                else if (id.totalBullets < id.magazineSize) // You cant reload a whole magazine
                {
                    int bulletsLeft = id.bulletsLeftInMagazine;
                    if (id.bulletsLeftInMagazine + id.totalBullets <= id.magazineSize)
                    {
                        id.bulletsLeftInMagazine = id.bulletsLeftInMagazine + id.totalBullets;
                        if (id.totalBullets - (id.magazineSize - bulletsLeft) >= 0) id.totalBullets = id.totalBullets - (id.magazineSize - bulletsLeft);
                        else id.totalBullets = 0;
                    }
                    else
                    {
                        int ToAdd = id.magazineSize - id.bulletsLeftInMagazine;
                        id.bulletsLeftInMagazine = id.bulletsLeftInMagazine + ToAdd;
                        if (id.totalBullets - ToAdd >= 0) id.totalBullets = id.totalBullets - ToAdd;
                        else id.totalBullets = 0;
                    }
                }
            }
            // Reload has finished
            weaponEvents.Events.OnAmmoChanged?.Invoke(settings.autoReload);
            settings.userEvents.OnFinishReload.Invoke();
            weaponEvents.Events.OnFinishReload.Invoke();
        }

        public IEnumerator OverheatReload()
        {
            // Currently reloading
            context.CanShoot = false;

            float waitTime = weapon.cooledPercentageAfterOverheat;

            // Stop being able to shoot, prevents from glitches
            weaponEvents.Events.OnWeaponCooling?.Invoke();

            // Wait until the heat ratio is appropriate to keep shooting
            yield return new WaitUntil(() => id.heatRatio <= waitTime);

            // Reload has finished
            weaponEvents.Events.OnAmmoChanged?.Invoke(settings.autoReload);
            settings.userEvents.OnFinishReload.Invoke();

            weaponBehaviour.IsReloading = false;
            context.CanShoot = true;
        }

        // Handles overheat weapons reloading.
        public void HandleHeatRatio()
        {
            if (weapon == null || id == null || id.magazineSize == 0 || weapon.reloadStyle == ReloadingStyle.defaultReload) return;

            // Dont keep cooling if it is completely cooled
            if (id.heatRatio <= 0) return;
            id.heatRatio -= Time.deltaTime * coolSpeed;
            if (id.heatRatio > 1) id.heatRatio = 1;
            if (id.heatRatio < 0) id.heatRatio = 0;
            weaponEvents.Events.OnAmmoChanged?.Invoke(settings.autoReload);
        }

        public void UpdateReloadUI()
        {
            if (weapon == null) return;

            bool isInfinite = weapon.infiniteBullets;
            bool isOverheat = weapon.reloadStyle == ReloadingStyle.Overheat;

            weaponEvents.Events.OnReloadUIChanged?.Invoke(!isInfinite && !isOverheat, isOverheat && !isInfinite);
        }

        public void SetupReloadMethods()
        {
            if (weapon == null) return;

            if (weapon.reloadStyle == ReloadingStyle.defaultReload)
            {
                reload = DefaultReload;
            }
            else
            {
                reload = OverheatReload;
                coolSpeed = weapon.coolSpeed;
            }
        }

        private void OnWeaponEquipped(WeaponIdentification id)
        {
            SetupReloadMethods();
            UpdateReloadUI();
        }
    }

}
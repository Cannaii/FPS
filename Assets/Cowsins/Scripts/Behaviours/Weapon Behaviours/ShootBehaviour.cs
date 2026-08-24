using UnityEngine;

namespace cowsins
{
    public class ShootBehaviour
    {
        private WeaponContext context;
        private InputManager inputManager;
        private IWeaponBehaviourProvider weaponBehaviour;
        private IWeaponReferenceProvider weaponReference;
        private IWeaponEventsProvider weaponEvents;
        private IPlayerMovementStateProvider playerMovement;
        private IPlayerMultipliers playerMultipliers;
        private PlayerDependencies playerDependencies;

        private WeaponControllerSettings settings;

        private Weapon_SO weapon => weaponReference.Weapon;
        private WeaponIdentification id => weaponReference.Id;
        private Camera mainCamera => weaponReference.MainCamera;
        private Transform weaponHolder => context.WeaponHolder;


        private Coroutine _allowShootCoroutine;

        public ShootBehaviour(WeaponContext context)
        {
            this.context = context;
            this.playerDependencies = context.Dependencies;
            this.inputManager = context.InputManager;
            this.weaponBehaviour = context.Dependencies.WeaponBehaviour;
            this.weaponReference = context.Dependencies.WeaponReference;
            this.weaponEvents = context.Dependencies.WeaponEvents;
            this.playerMovement = context.Dependencies.PlayerMovementState;
            this.playerMultipliers = context.Dependencies.PlayerMultipliers;

            this.settings = context.Settings;

            context.CanShoot = true;

            weaponEvents.Events.OnEquipWeapon.AddListener(SetShootingStyle);
            weaponEvents.Events.OnReduceAmmo.AddListener(ReduceAmmo);
            weaponEvents.Events.OnSelectWeapon.AddListener(CancelAllowShoot);
            weaponEvents.Events.OnWeaponCooling.AddListener(CancelAllowShoot);
        }

        public void Shoot() => id?.Shoot(weaponEvents.Events.RequestSpread(), playerMultipliers.DamageMultiplier.Value, weaponBehaviour.AimingCamShakeMultiplier * weaponBehaviour.CrouchingCamShakeMultiplier);

        public void ReduceAmmo()
        {
            id?.ReduceAmmo();
            weaponEvents.Events.OnAmmoChanged?.Invoke(settings.autoReload);
        }

        /// <summary>
        /// Get Shooting Style 
        /// Set the proper IShootStyle based on the Weapon Selected Shoot Style.
        /// Also, properly assign callbacks for shooting and hitting.
        /// The Shoot logic for each IShootStyle will run when calling Shoot() from the shoot Behaviour
        /// </summary>
        public void SetShootingStyle(WeaponIdentification id)
        {
            if (weapon == null) return;

            switch ((int)weapon.shootStyle)
            {
                case 0:
                    var hitscanShootWrapper = new HitscanShootStyle(playerDependencies, settings.hitLayer);
                    hitscanShootWrapper.SetOnShootEvent(OnShootHitscanProjectile);
                    id.SetShootStyle(hitscanShootWrapper);
                    break;
                case 1:
                    var projectileShootStyle = new ProjectileShootStyle(playerDependencies);
                    projectileShootStyle.SetOnShootEvent(OnShootHitscanProjectile);
                    id.SetShootStyle(projectileShootStyle);
                    break;
                case 2:
                    var meleeShootStyle = new MeleeShootStyle(playerDependencies, settings.hitLayer);
                    meleeShootStyle.SetOnShootEvent(OnShoot);
                    id.SetShootStyle(meleeShootStyle);
                    break;
                case 3:
                    var customShootStyle = new CustomShootStyle(playerDependencies, settings);
                    customShootStyle.SetOnShootEvent(OnShoot);
                    id.SetShootStyle(customShootStyle);
                    break;
            }
        }

        private void OnShoot()
        {
            settings.userEvents.OnShoot?.Invoke();

            // Determine if we want to add an effect for FOV
            if (weapon.applyFOVEffectOnShooting) weaponEvents.Events.OnShootApplyFOV?.Invoke(-weapon.FOVValueToSubtract);

            weaponEvents.Events.OnShootShake?.Invoke(weapon.camShakeAmount * weaponBehaviour.AimingCamShakeMultiplier * weaponBehaviour.CrouchingCamShakeMultiplier);
        }

        private void OnShootHitscanProjectile()
        {
            if (weapon == null) return;

            if (weapon.timeBetweenShots > 0)
            {
                // Rest the bullets that have just been shot
                ReduceAmmo();
            }

            weaponEvents.Events.OnShootShake?.Invoke(weapon.camShakeAmount * weaponBehaviour.AimingCamShakeMultiplier * weaponBehaviour.CrouchingCamShakeMultiplier);
            if (weapon.useProceduralShot) ProceduralShot.Instance.Shoot(weapon.proceduralShotPattern);

            // Determine if we want to add an effect for FOV
            if (weapon.applyFOVEffectOnShooting)
            {
                float fovAdjustment = weaponBehaviour.IsAiming ? weapon.AimingFOVValueToSubtract : weapon.FOVValueToSubtract;
                weaponEvents.Events.OnShootApplyFOV?.Invoke(-fovAdjustment);
            }
            foreach (var p in context.firePoint)
            {
                if (id.muzzleVFX != null)
                {
                    var vfx = PoolManager.Instance.GetFromPool(id.muzzleVFX, p.position, mainCamera.transform.rotation);
                    vfx.transform.SetParent(mainCamera.transform);
                }
            }
            CowsinsUtilities.ForcePlayAnim(id.GetCurrentShotAnimation(), id.Animator);
            if (weapon.timeBetweenShots > float.Epsilon) SoundManager.Instance.PlaySound(id.GetFireSFX(), 0, weapon.pitchVariationFiringSFX, true);

            settings.userEvents.OnShoot.Invoke();
            weaponEvents.Events.OnShootHitscanProjectile?.Invoke();
        }

        public void CancelAllowShoot()
        {
            if (_allowShootCoroutine != null)
            {
                context.CoroutineRunner.StopCoroutine(_allowShootCoroutine);
                _allowShootCoroutine = null;
            }
        }
    }
}

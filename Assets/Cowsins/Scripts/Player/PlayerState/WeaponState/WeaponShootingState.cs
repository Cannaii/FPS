using UnityEngine;
using UnityEngine.Events;

namespace cowsins
{
    public class WeaponShootingState : WeaponBaseState
    {
        // Weapon FSM ( Finite State Machine ) is dependant on WeaponController.
        private WeaponController controller;
        private IPlayerMovementStateProvider movement; // Reference to PlayerMovement.cs ( IPlayerMovementStateProvider is implemented in PlayerMovement.cs )
        private IPlayerControlProvider playerControl; // Reference to PlayerControl.cs ( IPlayerControlProvider is implemented in PlayerControl.cs )
        private InputManager inputManager;

        private Weapon_SO currentWeapon;
        private UnityEvent shootAction;

        // Control properties
        private bool isPressToShoot;
        private bool isHoldAndRelease;
        private bool shootsOnStopFire;

        private float holdProgress;

        public WeaponShootingState(WeaponStates currentContext, WeaponStateFactory playerStateFactory)
            : base(currentContext, playerStateFactory)
        {
            controller = _ctx.WeaponController;
            movement = _ctx.PlayerMovement;
            playerControl = _ctx.PlayerControlProvider;
            inputManager = _ctx.Dependencies.InputManager;

            shootAction = new UnityEvent();
        }

        public override void EnterState() 
        {
            // Reset hold progress when we start to shoot
            holdProgress = 0f;
            currentWeapon = controller.Weapon;

            isPressToShoot = currentWeapon.shootMethod == ShootingMethod.Press;
            isHoldAndRelease = currentWeapon.shootMethod == ShootingMethod.HoldAndRelease;
            shootsOnStopFire = isPressToShoot || isHoldAndRelease;

            UpdateShootEventListeners();

            controller.settings.userEvents.OnUnholster.AddListener(UpdateShootEventListeners);
            if (!isPressToShoot) inputManager.OnStopShoot += SwitchToDefault;

            controller.IsShooting = true;
        }

        public override void UpdateState()
        {
            if (CanShoot()) shootAction?.Invoke();
            HandleAiming();
            CheckSwitchState();
        }

        public override void FixedUpdateState(){}

        public override void ExitState()
        {
            controller.settings.userEvents.OnSelectWeapon.RemoveListener(UpdateShootEventListeners);

            if (isHoldAndRelease && holdProgress > 100f)
                controller.shootBehaviour.Shoot();

            if (!isPressToShoot)
                inputManager.OnStopShoot -= SwitchToDefault;

            controller.IsShooting = false;
        }

        public override void CheckSwitchState()
        {
            if (shootsOnStopFire && !isHoldAndRelease || !playerControl.IsControllable)
                SwitchToDefault();

            CheckReload();
        }

        private void SwitchToDefault() => SwitchState(_factory.Default());

        private bool CanShoot()
        {
            return controller.weaponContext.CanShoot &&
                    (controller.Id.bulletsLeftInMagazine > 0 || controller.Weapon.shootStyle == ShootStyle.Melee) // Melee weapons dont use bullets 
                    && (movement.CanShootWhileDashing && movement.IsDashing || !movement.IsDashing);
        }

        private void CheckReload()
        {
            if (controller.Weapon != null && controller.Weapon.reloadStyle == ReloadingStyle.defaultReload)
            {
                if (CanReload())
                    SwitchState(_factory.Reload());
            }
            else if (controller.Id != null && controller.Id.heatRatio >= 1)
            {
                SwitchState(_factory.Reload());
            }
        }
        private bool CanReload()
        {
            bool outOfAmmo = controller.Id.bulletsLeftInMagazine <= 0;
            bool needsReload = controller.Id.bulletsLeftInMagazine < controller.Id.magazineSize;
            bool hasAmmo = controller.Id.totalBullets > 0;
            bool isNotMelee = (int)controller.Weapon.shootStyle != 2;

            return (inputManager.Reloading && isNotMelee && needsReload && hasAmmo) ||
                   (outOfAmmo && controller.settings.autoReload && isNotMelee && hasAmmo);
        }

        private void HandleAiming()
        {
            if (inputManager.Aiming && controller.Weapon.allowAim) controller.aimBehaviour?.Tick();
            CheckStopAim();
        }

        private void CheckStopAim() { if (!inputManager.Aiming) controller.aimBehaviour?.Exit(); }

        // Subscribe to different ways of handling shpooting based on the shooting method
        private void UpdateShootEventListeners()
        {
            // Ensure shootAction is clean
            shootAction.RemoveAllListeners();

            switch (controller.Weapon?.shootMethod)
            {
                case ShootingMethod.Press: shootAction.AddListener(PressShoot); break;
                case ShootingMethod.PressAndHold: shootAction.AddListener(PressHoldShoot); break;
                case ShootingMethod.HoldAndRelease: shootAction.AddListener(HoldAndReleaseShoot); break;
                case ShootingMethod.HoldUntilReady: shootAction.AddListener(HoldUntilReadyShoot); break;
            }
        }

        private void PressShoot()
        {
            if (!_ctx.holding)
            {
                _ctx.holding = true;
                controller.shootBehaviour.Shoot();
            }
        }
        private void PressHoldShoot()
        {
            controller.shootBehaviour.Shoot();
        }
        private void HoldAndReleaseShoot()
        {
            if(holdProgress < 100 ) holdProgress += Time.deltaTime * controller.Weapon.holdProgressSpeed;
        }
        private void HoldUntilReadyShoot()
        {
            holdProgress += Time.deltaTime * controller.Weapon.holdProgressSpeed;

            if (holdProgress > 100)
            {
                controller.shootBehaviour.Shoot();
                holdProgress = 0;
            }
        }
    }
}
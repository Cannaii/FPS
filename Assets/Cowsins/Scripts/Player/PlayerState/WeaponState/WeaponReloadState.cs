namespace cowsins
{
    public class WeaponReloadState : WeaponBaseState
    {
        private WeaponController controller;
        private IPlayerControlProvider playerControl; // IPlayerControlProvider is implemented in PlayerControl.cs
        private InputManager inputManager;

        public WeaponReloadState(WeaponStates currentContext, WeaponStateFactory playerStateFactory)
            : base(currentContext, playerStateFactory)
        {
            controller = _ctx.WeaponController;
            playerControl = _ctx.PlayerControlProvider;
            inputManager = _ctx.Dependencies.InputManager;
        }

        public sealed override void EnterState()
        {
            controller.reloadBehaviour.StartReload();
        }

        public sealed override void UpdateState()
        {
            // Allow inventory switching if configured
            if (controller.Weapon != null && controller.Weapon.allowCancelReload)
                controller.weaponInventoryBehaviour.HandleInventory();

            CheckSwitchState();
            if (!playerControl.IsControllable) return;
            CheckStopAim();
        }

        public sealed override void FixedUpdateState()
        {
        }

        public sealed override void ExitState() { }

        public sealed override void CheckSwitchState()
        {
            if (!controller.IsReloading) SwitchState(_factory.Default());
        }
        private void CheckStopAim()
        {
            if (!inputManager.Aiming || !controller.Weapon.allowAimingIfReloading) controller.aimBehaviour?.Exit();
        }
    }
}
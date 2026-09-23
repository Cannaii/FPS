using System.Collections.Generic;

namespace cowsins
{
    public abstract class PlayerBaseState
    {
        protected PlayerStates _ctx;
        protected PlayerStateFactory _factory;

        protected InputManager inputManager => _ctx.InputManager;
        protected PlayerMovement playerMovement => _ctx.PlayerMovement;

        public PlayerBaseState(PlayerStates currentContext, PlayerStateFactory playerStateFactory)
        {
            _ctx = currentContext;
            _factory = playerStateFactory;
        }

        public abstract void EnterState();

        public abstract void UpdateState();

        public abstract void FixedUpdateState();

        public abstract void ExitState();

        public abstract void CheckSwitchState();

        protected void SwitchState(PlayerBaseState newState)
        {
            ExitState();

            newState.EnterState();

            _ctx.CurrentState = newState;
        }

        public void StartGrapple()
        {
            if (!playerMovement.playerSettings.allowGrapple) return;

            playerMovement.grapplingHookBehaviour?.Enter();
        }
        public void StopGrapple()
        {
            if (!playerMovement.playerSettings.allowGrapple) return;
            playerMovement.grapplingHookBehaviour?.Exit();
        }

        public void SwitchToDie() => SwitchState(_factory.Die());
    }
}
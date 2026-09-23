using UnityEngine;
namespace cowsins
{
    public class PlayerCrouchState : PlayerBaseState
    {
        private IPlayerControlProvider playerControlProvider; // IPlayerControlProvider is implemented in PlayerControl.cs
        private IPlayerStatsProvider statsProvider; // IPlayerStatsProvider is implemented in PlayerStats.cs

        private bool enteredWhileAirborne;

        public PlayerCrouchState(PlayerStates currentContext, PlayerStateFactory playerStateFactory)
            : base(currentContext, playerStateFactory)
        {
            statsProvider = _ctx.PlayerStatsProvider;
            playerControlProvider = _ctx.PlayerControlProvider;
        }

        public sealed override void EnterState()
        {
            enteredWhileAirborne = !playerMovement.Grounded;
            playerMovement.crouchSlideBehaviour?.Enter();
            statsProvider.AddOnDieListener(SwitchToDie);
        }

        public sealed override void UpdateState()
        {
            if (!playerControlProvider.IsControllable) return;

            playerMovement.crouchSlideBehaviour?.HandleCrouch();
            playerMovement.cameraLookBehaviour?.Tick();

            CheckSwitchState();
        }
        public sealed override void FixedUpdateState()
        {
            if (!playerControlProvider.IsControllable) return;

            playerMovement.basicMovementBehaviour?.Movement();
            playerMovement.crouchSlideBehaviour?.FixedTick();
            playerMovement.footstepsBehaviour?.FootSteps();
            playerMovement.speedLinesBehaviour?.Tick();
        }

        public sealed override void ExitState() 
        { 
            playerMovement.crouchSlideBehaviour?.Exit(); // Invoke your own method on the moment you are standing up NOT WHILE YOU ARE NOT CROUCHING
            statsProvider.RemoveOnDieListener(SwitchToDie);
        }

        public sealed override void CheckSwitchState()
        {
            if (playerMovement.jumpBehaviour.CanExecute() && inputManager.Jumping && playerMovement.playerSettings.canJumpWhileCrouching)
                SwitchState(_factory.Jump());

            if (inputManager.Dashing && playerMovement.dashBehaviour.CanExecute()) SwitchState(_factory.Dash());

            // Clear the airborne-entry flag once we touch the ground
            if (enteredWhileAirborne && playerMovement.Grounded)
                enteredWhileAirborne = false;


            if (!enteredWhileAirborne && !playerMovement.Grounded && !playerMovement.IsSliding)
            {
                SwitchState(_factory.Default());
                return;
            }

            if(playerMovement.crouchSlideBehaviour.CheckUnCrouch()) SwitchState(_factory.Default());
        }
    }
}
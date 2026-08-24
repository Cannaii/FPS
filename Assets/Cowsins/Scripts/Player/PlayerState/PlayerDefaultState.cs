using UnityEngine;

namespace cowsins
{
    public class PlayerDefaultState : PlayerBaseState
    {
        private IPlayerStatsProvider statsProvider;
        private IPlayerControlProvider playerControlProvider;
        private float ladderExitTime = -1f;
        private const float ladderExitCooldown = 0.3f;

        public PlayerDefaultState(PlayerStates currentContext, PlayerStateFactory playerStateFactory)
            : base(currentContext, playerStateFactory)
        {
            statsProvider = _ctx.PlayerStatsProvider;
            playerControlProvider = _ctx.PlayerControlProvider;
        }

        public sealed override void EnterState()
        {
            inputManager.OnJump += SwitchToJumpState;
            inputManager.OnDash += SwitchToDashState;
            inputManager.OnStartGrapple += StartGrapple;
            inputManager.OnStopGrapple += StopGrapple;
            statsProvider.AddOnDieListener(SwitchToDie);

            if (playerMovement.IsClimbing)
            {
                ladderExitTime = Time.time;
            }
        }

        public sealed override void UpdateState()
        {
            if (!playerControlProvider.IsControllable) return;

            playerMovement.cameraLookBehaviour.Tick();
            playerMovement.footstepsBehaviour?.FootSteps();
            if (playerMovement.playerSettings.allowGrapple)
                playerMovement.grapplingHookBehaviour?.Tick();
            playerMovement.crouchSlideBehaviour?.CheckUnCrouch();

            CheckSwitchState();
        }

        public sealed override void FixedUpdateState()
        {
            playerMovement.basicMovementBehaviour?.Movement();
            playerMovement.wallRunBehaviour?.FixedTick();
        }

        public sealed override void ExitState()
        {
            inputManager.OnJump -= SwitchToJumpState;
            inputManager.OnDash -= SwitchToDashState;
            inputManager.OnStartGrapple -= StartGrapple;
            inputManager.OnStopGrapple -= StopGrapple;
            statsProvider.RemoveOnDieListener(SwitchToDie);
        }

        public sealed override void CheckSwitchState()
        {
            bool canCheckLadder = Time.time - ladderExitTime > ladderExitCooldown;

            // Check climbing only if cooldown has passed
            if (canCheckLadder && playerMovement.climbLadderBehaviour.CanExecute())
                SwitchState(_factory.Climb());

            // Check Crouch
            if (playerMovement.crouchSlideBehaviour.CanExecute())
                SwitchState(_factory.Crouch());
        }

        public void SwitchToJumpState()
        {
            if (playerMovement.jumpBehaviour.CanExecute())
                SwitchState(_factory.Jump());
        }

        public void SwitchToDashState()
        {
            if (playerMovement.dashBehaviour.CanExecute())
                SwitchState(_factory.Dash());
        }
    }
}
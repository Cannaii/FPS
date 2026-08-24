using UnityEditor;
using UnityEngine;
namespace cowsins
{
    public class PlayerJumpState : PlayerBaseState
    {
        private IPlayerStatsProvider statsProvider; // IPlayerStatsProvider is implemented in PlayerStats.cs
        private IPlayerControlProvider playerControl; // IPlayerControlProvider is implemented in PlayerControl.cs

        public PlayerJumpState(PlayerStates currentContext, PlayerStateFactory playerStateFactory)
            : base(currentContext, playerStateFactory)
        {
            statsProvider = _ctx.PlayerStatsProvider;
            playerControl = _ctx.PlayerControlProvider;
        }

        public sealed override void EnterState()
        {
            playerMovement.jumpBehaviour.Enter();

            inputManager.OnStartGrapple += StartGrapple;
            inputManager.OnStopGrapple += StopGrapple;
            statsProvider.AddOnDieListener(SwitchToDie);
        }

        public sealed override void UpdateState()
        {
            CheckSwitchState();
            if (!playerControl.IsControllable) return;
            playerMovement.cameraLookBehaviour?.Tick();
            playerMovement.wallBounceBehaviour?.Tick();
            playerMovement.crouchSlideBehaviour?.CheckUnCrouch();
        }

        public sealed override void FixedUpdateState()
        {
            if (!playerControl.IsControllable) return;
            playerMovement.basicMovementBehaviour?.Movement();
            playerMovement.wallRunBehaviour?.FixedTick();
        }

        public sealed override void ExitState() 
        {
            inputManager.OnStartGrapple -= StartGrapple;
            inputManager.OnStopGrapple -= StopGrapple;
            statsProvider.RemoveOnDieListener(SwitchToDie);
        }

        public sealed override void CheckSwitchState()
        {
            if (playerMovement.wallBounceBehaviour != null)
            {
                playerMovement.wallBounceBehaviour?.Tick();

                if (playerMovement.wallBounceBehaviour.CanExecute())
                {
                    playerMovement.wallBounceBehaviour?.Enter();
                    playerMovement.playerSettings.events.OnWallBounce.Invoke();
                }
            }

            if (playerMovement.climbLadderBehaviour.CanExecute())
            {
                SwitchState(_factory.Climb());
                return;
            }

            if (inputManager.Jumping && playerMovement.jumpBehaviour.CanExecuteDoubleJump())
            {
                SwitchState(_factory.Jump());
                return;
            }

            if (playerMovement.Grounded && !playerMovement.movementContext.HasJumped || playerMovement.IsWallRunning)
            {
                SwitchState(_factory.Default());
                return;
            }

            if (inputManager.Dashing && playerMovement.dashBehaviour.CanExecute())
            {
                SwitchState(_factory.Dash());
                return;
            }

            if (playerMovement.playerSettings.allowCrouchWhileJumping && playerMovement.crouchSlideBehaviour.CanExecute())
            {
                SwitchState(_factory.Crouch());
                return;
            }

            if (playerMovement.playerSettings.allowGrapple)
            {
                playerMovement.grapplingHookBehaviour?.Tick();
            }
        }
    }
}
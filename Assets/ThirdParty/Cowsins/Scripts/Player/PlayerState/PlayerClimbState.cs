using UnityEngine;

namespace cowsins
{
    public class PlayerClimbState : PlayerBaseState
    {
        private Rigidbody rb;
        private IPlayerControlProvider playerControlProvider;
        private IPlayerStatsProvider statsProvider;

        public PlayerClimbState(PlayerStates currentContext, PlayerStateFactory playerStateFactory)
            : base(currentContext, playerStateFactory)
        {
            rb = _ctx.Rigidbody;
            playerControlProvider = _ctx.PlayerControlProvider;
            statsProvider = _ctx.PlayerStatsProvider;
        }

        public sealed override void EnterState()
        {
            playerMovement.climbLadderBehaviour?.Enter();
            playerMovement.playerSettings.events.OnStartClimb.Invoke();
            playerMovement.speedLinesBehaviour.StopSpeedlines();
            statsProvider.AddOnDieListener(SwitchToDie);
        }

        public sealed override void UpdateState()
        {
            if (!playerControlProvider.IsControllable) return;

            playerMovement.climbLadderBehaviour?.Tick();
            playerMovement.cameraLookBehaviour?.VerticalLook();
            CheckSwitchState();
        }

        public sealed override void FixedUpdateState() { }

        public sealed override void ExitState()
        {
            playerMovement.playerSettings.events.OnEndClimb.Invoke();
            playerMovement.climbLadderBehaviour?.Exit();
            statsProvider.RemoveOnDieListener(SwitchToDie);
        }

        public sealed override void CheckSwitchState()
        {
            bool isGrounded = playerMovement.Grounded;

            // Jump always exits
            if (inputManager.Jumping)
            {
                SwitchState(_factory.Default());
                return;
            }

            // Grounded + backward movement exits based on cooldown to prevent states flickering issues
            if (isGrounded && inputManager.Y < -0.01f)
            {
                SwitchState(_factory.Default());
                return;
            }

            // Reached top of ladder
            if (playerMovement.climbLadderBehaviour.DetectTopLadder() && inputManager.Y > 0.01f)
            {
                playerMovement.climbLadderBehaviour?.ApplyForcesOnTopReached();
                SwitchState(_factory.Default());
                return;
            }

            // Reached bottom of ladder
            if (playerMovement.climbLadderBehaviour.DetectBottomLadder() && inputManager.Y < -0.01f)
            {
                SwitchState(_factory.Default());
            }
        }
    }
}
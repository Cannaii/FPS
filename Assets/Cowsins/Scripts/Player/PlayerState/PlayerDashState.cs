using UnityEngine;
using System;

namespace cowsins
{
    public class PlayerDashState : PlayerBaseState
    {
        private Rigidbody rb;

        private float dashTimer;
        private Vector2 input;

        public PlayerDashState(PlayerStates currentContext, PlayerStateFactory playerStateFactory, Vector2 inp)
            : base(currentContext, playerStateFactory)
        {
            rb = _ctx.Rigidbody;
            input = inp;
        }

        public sealed override void EnterState()
        {
            dashTimer = playerMovement.playerSettings.dashDuration;

            playerMovement.dashBehaviour?.Enter(input);
            playerMovement.playerSettings.cameraFOVManager.ForceAddFOV(-playerMovement.playerSettings.fovToAddOnDash);
            playerMovement.playerSettings.events.OnStartDash.Invoke();
        }

        public sealed override void UpdateState()
        {
            playerMovement.playerSettings.events.OnDashing?.Invoke();
            playerMovement.dashBehaviour?.Tick();
            CheckSwitchState();
        }
        public sealed override void FixedUpdateState() {}

        public sealed override void ExitState()
        {
            playerMovement.dashBehaviour?.Exit();
        }

        public sealed override void CheckSwitchState()
        {
            dashTimer -= Time.deltaTime;

            if (dashTimer <= 0 || !playerMovement.IsDashing) SwitchState(_factory.Default());
        }
    }
}
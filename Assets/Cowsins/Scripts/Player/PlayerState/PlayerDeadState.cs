using UnityEngine;
namespace cowsins
{
    public class PlayerDeadState : PlayerBaseState
    {
        private Rigidbody rb;
        private IPlayerControlProvider playerControlProvider; // IPlayerControlProvider is implemented in PlayerControl.cs
        private IPlayerStatsProvider statsProvider; // IPlayerStatsProvider is implemented in PlayerStats.cs
        public PlayerDeadState(PlayerStates currentContext, PlayerStateFactory playerStateFactory)
            : base(currentContext, playerStateFactory) 
        {
            rb = _ctx.Rigidbody;
            playerControlProvider = _ctx.PlayerControlProvider;
            statsProvider = _ctx.PlayerStatsProvider;
        }

        public sealed override void EnterState()
        {
            playerControlProvider.LoseControl();
            
            if (statsProvider.FreezePlayerOnDeath) rb.isKinematic = true;
        }

        public sealed override void UpdateState() {}

        public sealed override void FixedUpdateState() { }

        public sealed override void ExitState() { rb.isKinematic = false; }

        public sealed override void CheckSwitchState() { }


    }
}
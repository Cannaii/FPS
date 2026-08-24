using UnityEngine;

namespace cowsins
{
    public class WeaponHiddenState : WeaponBaseState
    {
        private WeaponController controller;
        private IPlayerControlProvider playerControl; // IPlayerControlProvider is implemented in PlayerControl.cs

        public WeaponHiddenState(WeaponStates currentContext, WeaponStateFactory playerStateFactory)
            : base(currentContext, playerStateFactory)
        {
            controller = _ctx.WeaponController;
            playerControl = _ctx.PlayerControlProvider;
        }

        public sealed override void EnterState()
        {
            CowsinsUtilities.PlayAnim("hit", _ctx.WeaponAnimator.HolsterMotionObject);
        }

        public sealed override void UpdateState()
        {
        }

        public sealed override void FixedUpdateState()
        {
        }

        public sealed override void ExitState() 
        {
            _ctx.WeaponAnimator.HolsterMotionObject.Play("MeleeShowWeapon", 0, 0);
        }

        public sealed override void CheckSwitchState() {}
    }

}
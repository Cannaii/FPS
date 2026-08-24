using UnityEngine;
using System.Collections; 

namespace cowsins
{
    public class MeleeState : WeaponBaseState
    {
        private WeaponController controller;
        private WeaponAnimator weaponAnimator;

        private float timer;
        private bool isSwitchQueued = false;

        private Animator meleeObject;

        private float animationDuration;

        public MeleeState(WeaponStates currentContext, WeaponStateFactory playerStateFactory)
            : base(currentContext, playerStateFactory)
        {
            controller = _ctx.WeaponController;
            weaponAnimator = _ctx.WeaponAnimator;
            meleeObject = controller.settings.meleeObject.GetComponent<Animator>(); 
        }

        public sealed override void EnterState()
        {
            timer = 0;
            isSwitchQueued = false;
            controller.quickActionBehaviour.SecondaryMeleeAttack();
            if(controller.Id != null) controller.Id.gameObject.SetActive(false);
        }

        public sealed override void UpdateState()
        {
            controller.aimBehaviour?.Exit();
            CheckSwitchState();
        }

        public sealed override void FixedUpdateState()
        {
        }

        public sealed override void ExitState()
        {
            if (controller.Id != null) controller.Id.gameObject.SetActive(true);
            _ctx.WeaponAnimator.HolsterMotionObject.Play("MeleeShowWeapon", 0, 0);
        }

        public sealed override void CheckSwitchState()
        {
            timer += Time.deltaTime;
            AnimatorStateInfo stateInfo = meleeObject.GetCurrentAnimatorStateInfo(0);
            animationDuration = stateInfo.length;
 
            if (!isSwitchQueued && timer >= animationDuration + controller.settings.meleeDelay)
            {
                controller.quickActionBehaviour.FinishMelee();

                AnimatorStateInfo holsterStateInfo = weaponAnimator.HolsterMotionObject.GetCurrentAnimatorStateInfo(0);

                SwitchState(_factory.Default());
                isSwitchQueued = true;
            }
        }
    }
}

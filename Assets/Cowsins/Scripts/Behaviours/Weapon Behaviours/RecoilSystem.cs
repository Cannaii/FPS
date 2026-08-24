using cowsins;
using UnityEngine;

namespace cowsins
{
    public class RecoilSystem
    {
        private InputManager inputManager;
        private IWeaponBehaviourProvider weaponBehaviour;
        private IWeaponReferenceProvider weaponReference;
        private IWeaponEventsProvider weaponEvents;
        private IPlayerControlProvider playerControl;

        private WeaponContext weaponContext;

        private Weapon_SO weapon => weaponReference.Weapon;
        private WeaponIdentification id => weaponReference.Id;

        private float evaluationProgress;
        public float recoilPitchOffset { get; private set; }
        public float recoilYawOffset { get; private set; }
        private bool shotTriggered = false;

        public RecoilSystem(WeaponContext context)
        {
            this.weaponContext = context;
            PlayerDependencies playerDependencies = weaponContext.Dependencies;

            this.inputManager = playerDependencies.InputManager;
            this.weaponBehaviour = playerDependencies.WeaponBehaviour;
            this.weaponReference = playerDependencies.WeaponReference;
            this.weaponEvents = playerDependencies.WeaponEvents;
            this.playerControl = playerDependencies.PlayerControl;

            weaponEvents.Events.OnShootHitscanProjectile.AddListener(ProgressRecoil);
        }

        public void Tick()
        {
            // Relax back to 0 if weapon is null or the current weapon does not apply recoil
            if (weapon == null || !weapon.applyRecoil || id.bulletsLeftInMagazine <= 0)
            {
                recoilPitchOffset = Mathf.Lerp(recoilPitchOffset, 0, 3 * Time.deltaTime);
                recoilYawOffset = Mathf.Lerp(recoilYawOffset, 0, 3 * Time.deltaTime);
                evaluationProgress = 0f;
                return;
            }

            // Determine if not shooting so we will relax back to 0
            // For Press weapons we only consider the moment the shot was triggered
            bool isShootingNow = (weapon.shootMethod == ShootingMethod.Press) ? shotTriggered : inputManager.Shooting;

            // If reloading or player not controllable, relax and reset progress
            if (weaponBehaviour.IsReloading || !playerControl.IsControllable)
            {
                recoilPitchOffset = Mathf.Lerp(recoilPitchOffset, 0, weapon.recoilRelaxSpeed * Time.deltaTime);
                recoilYawOffset = Mathf.Lerp(recoilYawOffset, 0, weapon.recoilRelaxSpeed * Time.deltaTime);
                evaluationProgress = 0f;
                shotTriggered = false;
                return;
            }

            // If not shooting and we have no recoil progress, relax to 0 and stop running
            if (!isShootingNow && evaluationProgress <= 0f)
            {
                recoilPitchOffset = Mathf.Lerp(recoilPitchOffset, 0, weapon.recoilRelaxSpeed * Time.deltaTime);
                recoilYawOffset = Mathf.Lerp(recoilYawOffset, 0, weapon.recoilRelaxSpeed * Time.deltaTime);
                evaluationProgress = 0f;
                return;
            }

            // Calculate amounts based on aiming
            float xamount = (weapon.applyDifferentRecoilOnAiming && weaponBehaviour.IsAiming) ? weapon.xRecoilAmountOnAiming : weapon.xRecoilAmount;
            float yamount = (weapon.applyDifferentRecoilOnAiming && weaponBehaviour.IsAiming) ? weapon.yRecoilAmountOnAiming : weapon.yRecoilAmount;

            // If not currently shooting (according to `isShootingNow`) but we have progress (single-shot), decay the evaluationProgress
            if (!isShootingNow && evaluationProgress > 0f)
            {
                float decayRate = weapon.recoilRelaxSpeed / Mathf.Max(1f, weapon.magazineSize);
                evaluationProgress = Mathf.MoveTowards(evaluationProgress, 0f, decayRate * Time.deltaTime);

                float targetPitchRecoil = -weapon.recoilY.Evaluate(evaluationProgress) * yamount;
                float targetYawRecoil = -weapon.recoilX.Evaluate(evaluationProgress) * xamount;

                recoilPitchOffset = Mathf.Lerp(recoilPitchOffset, targetPitchRecoil, weapon.recoilRelaxSpeed * Time.deltaTime);
                recoilYawOffset = Mathf.Lerp(recoilYawOffset, targetYawRecoil, weapon.recoilRelaxSpeed * Time.deltaTime);
                return;
            }

            // While shooting evaluate recoil normally
            float shootTargetPitchRecoil = -weapon.recoilY.Evaluate(evaluationProgress) * yamount;
            float shootTargetYawRecoil = -weapon.recoilX.Evaluate(evaluationProgress) * xamount;

            recoilPitchOffset = Mathf.Lerp(recoilPitchOffset, shootTargetPitchRecoil, weapon.recoilRelaxSpeed * Time.deltaTime);
            recoilYawOffset = Mathf.Lerp(recoilYawOffset, shootTargetYawRecoil, weapon.recoilRelaxSpeed * Time.deltaTime);

            // If this was a single press shot trigger, clear the control bool so holding doesnt retrigger
            if (weapon.shootMethod == ShootingMethod.Press)
            {
                shotTriggered = false;
            }
        }
        public void ProgressRecoil()
        {
            if (weapon.applyRecoil)
            {
                evaluationProgress += 1f / weapon.magazineSize;
                shotTriggered = true;
            }
        }
    }

}
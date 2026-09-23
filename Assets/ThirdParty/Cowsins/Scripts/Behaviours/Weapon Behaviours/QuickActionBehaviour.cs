using UnityEngine;
using System.Collections;

namespace cowsins
{
    public class QuickActionBehaviour
    {
        private WeaponContext context;
        private InputManager inputManager;
        private IWeaponBehaviourProvider weaponBehaviour;
        private IWeaponReferenceProvider weaponReference;
        private IPlayerMovementStateProvider playerMovement;
        private IPlayerControlProvider playerControl;
        private IWeaponEventsProvider weaponEvents;
        private IPlayerMultipliers playerMultipliers;

        private Weapon_SO weapon => weaponReference.Weapon;
        private WeaponIdentification id => weaponReference.Id;
        private Camera mainCamera => weaponReference.MainCamera;
        private Transform weaponHolder => context.WeaponHolder;

        private WeaponControllerSettings settings;

        private Coroutine meleeCoroutine;
        private Coroutine reEnableMeleeCoroutine;

        private MonoBehaviour CoroutineRunner => context.CoroutineRunner;

        public QuickActionBehaviour(WeaponContext context)
        {
            this.context = context;
            this.inputManager = context.InputManager;
            this.weaponBehaviour = context.Dependencies.WeaponBehaviour;
            this.weaponReference = context.Dependencies.WeaponReference;
            this.playerMovement = context.Dependencies.PlayerMovementState;
            this.playerControl = context.Dependencies.PlayerControl;
            this.weaponEvents = context.Dependencies.WeaponEvents;
            this.playerMultipliers = context.Dependencies.PlayerMultipliers;

            this.settings = context.Settings;
            weaponBehaviour.IsMeleeAvailable = true;
        }

        public bool CanExecute()
        {
            return playerControl.IsControllable && settings.canMelee && weaponBehaviour.IsMeleeAvailable && !playerMovement.IsClimbing;
        }

        public void SecondaryMeleeAttack()
        {
            weaponBehaviour.IsMeleeAvailable = false;
            settings.meleeObject.SetActive(true);

            if (meleeCoroutine != null) CoroutineRunner.StopCoroutine(meleeCoroutine);
            meleeCoroutine = CoroutineRunner.StartCoroutine(MeleeRoutine());

            weaponEvents.Events.OnSecondaryAttack?.Invoke(settings.meleeHeadBone ?? null);

            // Play melee audio
            if (settings.meleeAudioClip != null && context.AudioSource != null)
            {
                context.AudioSource.PlayOneShot(settings.meleeAudioClip);
            }
        }

        private IEnumerator MeleeRoutine()
        {
            yield return new WaitForSeconds(settings.meleeDelay);
            Melee();
        }

        private void Melee()
        {
            settings.userEvents.OnSecondaryMelee?.Invoke();
            MeleeAttack(settings.meleeRange, settings.meleeAttackDamage);
            weaponEvents.Events.OnShootShake?.Invoke(settings.meleeCamShakeAmount * weaponBehaviour.AimingCamShakeMultiplier * weaponBehaviour.CrouchingCamShakeMultiplier);
        }

        public void FinishMelee()
        {
            settings.meleeObject.SetActive(false);

            if (reEnableMeleeCoroutine != null) CoroutineRunner.StopCoroutine(reEnableMeleeCoroutine);
            reEnableMeleeCoroutine = CoroutineRunner.StartCoroutine(ReEnableMeleeRoutine());
        }

        private IEnumerator ReEnableMeleeRoutine()
        {
            yield return new WaitForSeconds(settings.reEnableMeleeAfterAction);
            weaponBehaviour.IsMeleeAvailable = true;
        }

        /// <summary>
        /// Moreover, cowsins� FPS ENGINE also supports melee attacking
        /// Use this for Swords, knives etc
        /// </summary>
        private void MeleeAttack(float attackRange, float damage)
        {
            RaycastHit hit;
            Vector3 basePosition = id != null ? id.transform.position : context.Transform.position;
            Collider[] col = Physics.OverlapSphere(basePosition + mainCamera.transform.parent.forward * attackRange / 2, attackRange, settings.hitLayer);

            float dmg = damage * playerMultipliers.DamageMultiplier.Value;

            foreach (var c in col)
            {
                if (c.CompareTag("Critical") || c.CompareTag("BodyShot"))
                {
                    CowsinsUtilities.GatherDamageableParent(c.transform)?.Damage(dmg, false);
                    break;
                }

                if (c.TryGetComponent<IDamageable>(out var damageable))
                {
                    damageable.Damage(dmg, false);
                    break;
                }
            }

            //VISUALS
            Ray ray = new Ray(mainCamera.transform.position, mainCamera.transform.forward);
            if (Physics.Raycast(ray, out hit, attackRange, settings.hitLayer))
            {
                weaponEvents.Events.OnHit?.Invoke(hit.collider.gameObject.layer, 0f, hit, false);
            }
        }
    }

}

using UnityEngine;

namespace cowsins
{
    public class HitDetectionSystem
    {
        private WeaponContext context;
        private InputManager inputManager;
        private IWeaponReferenceProvider weaponReference;
        private IWeaponEventsProvider weaponEvents;
        private IPlayerMovementStateProvider playerMovement;

        private Weapon_SO weapon => weaponReference.Weapon;
        private WeaponIdentification id => weaponReference.Id;
        private Camera mainCamera => weaponReference.MainCamera;
        private Transform weaponHolder => context.WeaponHolder;

        private WeaponControllerSettings settings;

        public HitDetectionSystem(WeaponContext context, WeaponControllerSettings settings)
        {
            this.settings = settings;

            // Register Pool
            foreach (var entry in this.settings.impactEffects.impacts)
            {
                if (entry.impact != null)
                    PoolManager.Instance.RegisterPool(entry.impact, PoolManager.Instance.WeaponEffectsSize);
            }
            if (this.settings.impactEffects.defaultImpact != null)
                PoolManager.Instance.RegisterPool(this.settings.impactEffects.defaultImpact, PoolManager.Instance.WeaponEffectsSize);

            this.context = context;
            this.inputManager = context.InputManager;
            this.weaponReference = context.Dependencies.WeaponReference;
            this.weaponEvents = context.Dependencies.WeaponEvents;
            this.playerMovement = context.Dependencies.PlayerMovementState;

            context.Dependencies.WeaponEvents.Events.OnHit.AddListener(Hit);
        }

        /// <summary>
        /// Handles hit detection, applies damage, and triggers effects.
        /// </summary>
        public void Hit(int layer, float damage, RaycastHit h, bool damageTarget)
        {
            if (weapon == null || h.collider == null) return;

            settings.userEvents.OnHit?.Invoke();
            weaponEvents.Events.OnInstantiateBulletHoleImpact?.Invoke(layer, h);

            if (!damageTarget) return;

            var hitTransform = h.collider.transform;
            float finalDamage = damage * GetDistanceDamageReduction(hitTransform);

            // Determine hit type and apply damage accordingly
            if (hitTransform.CompareTag("Critical"))
            {
                settings.userEvents.OnCriticalHit?.Invoke();
                var damageable = CowsinsUtilities.GatherDamageableParent(hitTransform);
                damageable?.Damage(finalDamage * weapon.criticalDamageMultiplier, true);
            }
            else if (hitTransform.CompareTag("BodyShot"))
            {
                var damageable = CowsinsUtilities.GatherDamageableParent(hitTransform);
                damageable?.Damage(finalDamage, false);
            }
            else
            {
                var damageable = h.collider.GetComponent<IDamageable>();
                damageable?.Damage(finalDamage, false);
            }
        }

        private float GetDistanceDamageReduction(Transform target)
        {
            if (!weapon.applyDamageReductionBasedOnDistance) return 1;
            if (Vector3.Distance(target.position, context.Transform.position) > weapon.minimumDistanceToApplyDamageReduction)
                return (weapon.minimumDistanceToApplyDamageReduction / Vector3.Distance(target.position, context.Transform.position)) * weapon.damageReductionMultiplier;
            else return 1;
        }
    }

}
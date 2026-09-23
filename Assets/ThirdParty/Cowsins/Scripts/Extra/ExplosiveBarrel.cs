/// <summary>
/// This script belongs to cowsins™ as a part of the cowsins´ FPS Engine. All rights reserved. 
/// </summary>
using UnityEngine;

namespace cowsins
{
    /// <summary>
    /// Inheriting from destructible, lets you explode barrels
    /// </summary>
    public class ExplosiveBarrel : Destructible
    {
        [SerializeField] private float explosionRadius;

        [SerializeField] private float explosionForce;

        [SerializeField] private bool hurtPlayer = true;

        [Tooltip("Damage dealt on explosion to any Damageable object within the radius." +
            "NOTE:Damage will be scaled depending on how far the object is from the center of the explosion "), SerializeField]
        private float damage;

        [Header("Effects")]
        [Tooltip("Instantiate this when the barrel explodes"), SerializeField]
        private GameObject destroyedObject, explosionVFX;

        /// <summary>
        /// Override the method from Destructible.cs
        /// Here we are damaging IDamageables within a certain radius & also instantiating some effect on destructed.
        /// </summary>
        public override void Die()
        {
            SoundManager.Instance.PlaySoundAtPosition(destroyedSFX,transform.position, 0, .1f, true);
            Collider[] cols = Physics.OverlapSphere(transform.position, explosionRadius);

            Instantiate(destroyedObject, transform.position, Quaternion.identity);
            Instantiate(explosionVFX, transform.position, Quaternion.identity);

            foreach (Collider c in cols)
            {
                if (c.CompareTag("Player") && !hurtPlayer) return;

                if (c.TryGetComponent<Rigidbody>(out Rigidbody rb))
                {
                    rb.AddExplosionForce(
                        explosionForce / (Vector3.Distance(c.transform.position, transform.position) + 0.1f),
                        transform.position,
                        explosionRadius,
                        5f,
                        ForceMode.Impulse
                    );
                }

                float dmg = damage / Vector3.Distance(c.transform.position, transform.position);
                if (c.CompareTag("BodyShot"))
                {
                    CowsinsUtilities.GatherDamageableParent(c.transform).Damage(dmg, false);
                    continue;
                }
                else if (c.GetComponent<IDamageable>() != null)
                {
                    c.GetComponent<IDamageable>().Damage(dmg, false);
                    if (c.GetComponent<IPlayerMovementStateProvider>() != null)
                    {
                        CameraEffects cameraEffects = c.GetComponent<CameraEffects>();
                        cameraEffects.ExplosionShake(Vector3.Distance(cameraEffects.transform.position, transform.position));
                    }
                    continue;
                }
            }
            base.Die();
        }
    }
}
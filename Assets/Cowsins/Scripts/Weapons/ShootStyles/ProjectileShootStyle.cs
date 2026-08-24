using System;
using System.Collections;
using UnityEngine;

namespace cowsins
{
    /// <summary>
    /// When a weapon is set to Projectile Shoot Style, its shootBehaviour in WeaponIdentification is assigned to this ProjectileShootStyle.
    /// This is done when the weapon is unholstered.
    /// </summary>
    public class ProjectileShootStyle : IShootStyle
    {
        private PlayerDependencies playerDependencies;
        private IWeaponReferenceProvider weaponReference;
        private IPlayerMultipliers multipliers;
        private IWeaponEventsProvider weaponEvents; 
        private Transform[] firePoint;
        private Weapon_SO weapon;
        private WeaponIdentification id;
        private Camera mainCamera;

        private bool canShoot = true;
        public Coroutine shootingCoroutine, allowShootCoroutine;

        // Calls events in Weapon Controller when shooting or hitting an enemy ( ProjectileShootStyle does not handle hits so onHit is not used here,
        // but it is still required by IShootStyle.
        public event Action onShoot;
#pragma warning disable CS0067
        public event Action<int, float, RaycastHit, bool> onHit;
#pragma warning restore CS0067

        public ProjectileShootStyle(PlayerDependencies playerDependencies)
        {
            this.playerDependencies = playerDependencies;
            this.weaponReference = playerDependencies.WeaponReference;
            this.weapon = weaponReference.Weapon;
            this.id = weaponReference.Id;
            this.multipliers = playerDependencies.PlayerMultipliers;
            this.weaponEvents = playerDependencies.WeaponEvents;
            this.mainCamera = weaponReference.MainCamera;
            this.firePoint = id.FirePoint;
        }

        public void Shoot(float spread, float damageMultiplier, float shakeMultiplier)
        {
            if (canShoot)
                HandleProjectileShot(spread);
        }
        private void HandleProjectileShot(float spread)
        {
            if (weapon == null) return;

            canShoot = false; // since you have already shot, you will have to wait in order to being able to shoot again

            shootingCoroutine = playerDependencies.StartCoroutine(HandleShooting(spread));
            if (allowShootCoroutine != null)
            {
                playerDependencies.StopCoroutine(allowShootCoroutine);
                allowShootCoroutine = null;
            }
            playerDependencies.StartCoroutine(AllowShootAfterDelay(id.fireRate));

            weaponEvents.Events.OnShootSpawnEffects?.Invoke();
        }

        private IEnumerator HandleShooting(float spread)
        {
            if ((int)weapon.shootStyle == 1)
            {
                yield return new WaitForSeconds(weapon.shootDelay);
            }

            if (weapon.timeBetweenShots == 0)
            {
                // Rest the bullets that have just been shot
                weaponEvents.Events.OnReduceAmmo?.Invoke();
            }

            // Avoid calling the while loop if we only want to shoot one bullet
            if (weapon.bulletsPerFire == 1)
            {
                if (weapon == null) yield break;
                ProjectileShoot(spread);
            }
            else
            {
                int i = 0;
                while (i < weapon.bulletsPerFire)
                {
                    if (weapon == null) yield break;

                    ProjectileShoot(spread);
                    yield return new WaitForSeconds(weapon.timeBetweenShots);
                    i++;
                }
            }

            yield break;
        }

        private void ProjectileShoot(float spread)
        {
            onShoot?.Invoke();

            RaycastHit hit;
            Vector3 dir = CowsinsUtilities.GetSpreadDirection(spread, mainCamera);
            Ray ray = new Ray(mainCamera.transform.position, dir);
            Vector3 destination = (Physics.Raycast(ray, out hit) && !hit.transform.CompareTag("Player")) ? hit.point : ray.GetPoint(50f);

            if (weapon.projectile == null || weapon.projectile?.GetComponent<IBullet>() == null)
            {
                Debug.LogWarning($"Projectile is not set for {weapon._name}");
                return;
            }

            foreach (var p in firePoint)
            {
                GameObject bulletGO = GameObject.Instantiate(weapon.projectile.gameObject, p.position, p.transform.rotation);
                IBullet bullet = bulletGO.GetComponent<IBullet>();
                if (weapon.explosionOnHit) bullet.ExplosionVFX = weapon.explosionVFX;

                bullet.HurtsPlayer = weapon.hurtsPlayer;
                bullet.ExplosionOnHit = weapon.explosionOnHit;
                bullet.ExplosionRadius = weapon.explosionRadius;
                bullet.ExplosionForce = weapon.explosionForce;

                bullet.CriticalMultiplier = weapon.criticalDamageMultiplier;
                bullet.Destination = destination;
                bullet.Player = playerDependencies.transform;
                bullet.Speed = weapon.speed;
                if (bulletGO.TryGetComponent<Rigidbody>(out Rigidbody rb)) rb.isKinematic = !weapon.projectileUsesGravity;
                bullet.Damage = id.damage * multipliers.DamageMultiplier.Value;
                bullet.Duration = weapon.bulletDuration;
            }
        }

        private IEnumerator AllowShootAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            canShoot = true;
        }

        public void SetOnShootEvent(Action @event) => onShoot = @event;
    }
}

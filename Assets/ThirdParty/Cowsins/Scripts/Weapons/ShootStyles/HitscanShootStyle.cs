using System;
using UnityEngine;
using System.Collections;

namespace cowsins
{
    /// <summary>
    /// When a weapon is set to Hitscan Shoot Style, its shootBehaviour in WeaponIdentification is assigned to this HitscanShootStyle.
    /// This is done when the weapon is unholstered.
    /// </summary>
    public class HitscanShootStyle : IShootStyle
    {
        private Weapon_SO weapon;
        private WeaponIdentification id;
        private PlayerDependencies playerDependencies;
        private IWeaponReferenceProvider weaponReference;
        private IWeaponEventsProvider weaponEvents;
        private IPlayerMultipliers multipliers;
        private Transform[] firePoint;
        private Camera mainCamera;
        private LayerMask hitLayer;

        private bool canShoot = true;
        public Coroutine shootingCoroutine, allowShootCoroutine;

        // Calls events in Weapon Controller when shooting or hitting an enemy
        public event Action onShoot;

        public HitscanShootStyle(PlayerDependencies playerDependencies, LayerMask hitLayer)
        {
            this.playerDependencies = playerDependencies;
            this.weaponReference = playerDependencies.WeaponReference;
            this.weaponEvents = playerDependencies.WeaponEvents;
            this.weapon = weaponReference.Weapon;
            this.id = weaponReference.Id;  
            this.multipliers = playerDependencies.PlayerMultipliers;
            this.firePoint = id.FirePoint;
            this.mainCamera = weaponReference.MainCamera;
            this.hitLayer = hitLayer;
        }

        public void Shoot(float spread, float damageMultiplier, float shakeMultiplier)
        {
            if (canShoot) 
                HandleHitscanProjectileShot(spread); 
        }

        private void HandleHitscanProjectileShot(float spread)
        {
            if (weapon == null) return;

            canShoot = false; // since you have already shot, you will have to wait in order to being able to shoot again

            shootingCoroutine = playerDependencies.StartCoroutine(HandleShooting(spread));
            if(allowShootCoroutine != null)
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
                HitscanShot(spread);
            }
            else
            {
                int i = 0;
                while (i < weapon.bulletsPerFire)
                {
                    if (weapon == null) yield break;

                    HitscanShot(spread);
                    yield return new WaitForSeconds(weapon.timeBetweenShots);
                    i++;
                }
            }

            yield break;
        }

        private void HitscanShot(float spread)
        {
            onShoot?.Invoke();
        
            RaycastHit hit;
            Transform hitObj;

            //This defines the first hit on the object
            Vector3 dir = CowsinsUtilities.GetSpreadDirection(spread, mainCamera);
            Ray ray = new Ray(mainCamera.transform.position, dir);

            if (Physics.Raycast(ray, out hit, weapon.bulletRange, hitLayer))
            {
                float dmg = id.damage * multipliers.DamageMultiplier.Value;
                weaponEvents.Events.OnHit?.Invoke(hit.collider.gameObject.layer, dmg, hit, true);
                hitObj = hit.collider.transform;

                //Handle Penetration
                Ray newRay = new Ray(hit.point, ray.direction);
                RaycastHit newHit;

                if (Physics.Raycast(newRay, out newHit, id.penetrationAmount, hitLayer))
                {
                    if (hitObj != newHit.collider.transform)
                    {
                        float dmg_ = id.damage * multipliers.DamageMultiplier.Value * weapon.penetrationDamageReduction;
                        weaponEvents.Events.OnHit?.Invoke(newHit.collider.gameObject.layer, dmg_, newHit, true);
                    }
                }

                // Handle Bullet Trails
                if (weapon.bulletTrail == null) return;

                foreach (var p in firePoint)
                {
                    if(p == null) continue;

                    TrailRenderer trail = PoolManager.Instance.GetFromPool(weapon.bulletTrail.gameObject, p.position, Quaternion.identity).GetComponent<TrailRenderer>();

                    playerDependencies.StartCoroutine(SpawnTrail(trail, hit));
                }
            }
        }
        private IEnumerator SpawnTrail(TrailRenderer trail, RaycastHit hit)
        {
            float time = 0;
            Vector3 startPos = trail.transform.position;
            GameObject prefab = weapon.bulletTrail != null ? weapon.bulletTrail.gameObject : null;
            while (time < 1)
            {
                trail.transform.position = Vector3.Lerp(startPos, hit.point, time);
                time += Time.deltaTime / trail.time;

                yield return null;
            }

            trail.transform.position = hit.point;
            PoolManager.Instance.ReturnToPool(trail.gameObject, prefab);
        }

        private IEnumerator AllowShootAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            canShoot = true;
        }

        public void SetOnShootEvent(Action @event) => onShoot = @event;
    }
}

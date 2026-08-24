using UnityEngine;

namespace cowsins
{
    public class WeaponEffectsSystem
    {
        private WeaponContext context;
        private IWeaponReferenceProvider weaponReference;
        private IWeaponEventsProvider weaponEvents;

        private readonly GameObject[] sharedImpactPrefabs = new GameObject[5];

        private WeaponControllerSettings settings;

        public WeaponEffectsSystem(WeaponContext context, WeaponControllerSettings settings)
        {
            this.context = context;
            this.weaponReference = context.Dependencies.WeaponReference;
            this.weaponEvents = context.Dependencies.WeaponEvents;

            weaponEvents.Events.OnShootSpawnEffects.AddListener(SpawnBulletShells);
            weaponEvents.Events.OnUnholster.AddListener(RegisterWeaponPools);
            weaponEvents.Events.OnInstantiateBulletHoleImpact.AddListener(HandleBulletHoleImpacts);
            this.settings = settings;
        }

        private void RegisterWeaponPools(bool prop, bool prop2) => RegisterWeaponPools();
        public void RegisterWeaponPools()
        {
            if (weaponReference.Weapon == null) return;
            var bulletHoles = weaponReference.Weapon.bulletHoleImpact;

            foreach (var entry in bulletHoles.bulletHoleImpact)
            {
                if (entry.bulletHoleImpact != null)
                    PoolManager.Instance.RegisterPool(entry.bulletHoleImpact, PoolManager.Instance.WeaponEffectsSize);
            }

            if (bulletHoles.defaultImpact != null)
                PoolManager.Instance.RegisterPool(bulletHoles.defaultImpact, PoolManager.Instance.WeaponEffectsSize);

            if (weaponReference.Weapon.showBulletShells && (int)weaponReference.Weapon.shootStyle != 2 && weaponReference.Weapon.bulletGraphics != null)
                PoolManager.Instance.RegisterPool(weaponReference.Weapon.bulletGraphics.gameObject, PoolManager.Instance.BulletGraphicsSize);
        }


        public void SpawnBulletShells()
        {
            if (weaponReference.Weapon == null) return;

            foreach (var p in context.firePoint)
            {
                // Adding a layer of realism, bullet shells get instantiated and interact with the world
                // We should  first check if we really wanna do this
                if (weaponReference.Weapon.showBulletShells && (int)weaponReference.Weapon.shootStyle != 2 && weaponReference.Weapon.bulletGraphics != null)
                {
                    Transform spawnPoint = (weaponReference.Id.shellEjectPoint != null) ? weaponReference.Id.shellEjectPoint : p;
                    var bulletShell = PoolManager.Instance.GetFromPool(weaponReference.Weapon.bulletGraphics.gameObject, spawnPoint.position, Quaternion.identity);
                    Rigidbody shellRigidbody = bulletShell.GetComponent<Rigidbody>();
                    float torque = UnityEngine.Random.Range(-15f, 15f);
                    Vector3 shellForce = weaponReference.MainCamera.transform.right * 5 + weaponReference.MainCamera.transform.up * 5;
                    shellRigidbody.AddTorque(weaponReference.MainCamera.transform.right * torque, ForceMode.Impulse);
                    shellRigidbody.AddForce(shellForce, ForceMode.Impulse);
                }
            }
            if (weaponReference.Weapon.timeBetweenShots == 0) SoundManager.Instance.PlaySound(weaponReference.Id.GetFireSFX(), 0, weaponReference.Weapon.pitchVariationFiringSFX, true);
        }
        private void HandleBulletHoleImpacts(int layer, RaycastHit h)
        {
            Quaternion normalRot = Quaternion.LookRotation(h.normal);
            Vector3 hitPoint = h.point;

            // Grab VFX impact by layer
            GameObject impactEffect = PoolManager.Instance.GetFromPool(
                settings.impactEffects.GetImpactForLayer(layer),
                hitPoint,
                Quaternion.identity
            );

            GameObject bulletHoleImpact = PoolManager.Instance.GetFromPool(
                weaponReference.Weapon.bulletHoleImpact.GetBulletHoleForLayer(layer),
                hitPoint,
                Quaternion.identity
            );

            if (h.collider == null) return;

            if (impactEffect != null)
            {
                impactEffect.transform.rotation = normalRot;
                impactEffect.transform.SetParent(h.collider.transform, worldPositionStays: true);
            }
            if (bulletHoleImpact != null)
            {
                bulletHoleImpact.transform.rotation = normalRot;
                bulletHoleImpact.transform.SetParent(h.collider.transform, worldPositionStays: true);
            }
        }
    }

}
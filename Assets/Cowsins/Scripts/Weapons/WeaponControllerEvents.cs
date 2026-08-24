using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using System;

namespace cowsins
{
    public class WeaponControllerEvents
    {
        // INITIALIZATION
        public UnityEvent<int> OnInitializeWeaponSystem = new UnityEvent<int>();

        // SHOOTING
        public UnityEvent OnShoot = new UnityEvent();
        public UnityEvent<float> OnShootShake = new UnityEvent<float>();
        public UnityEvent<float> OnShootApplyFOV = new UnityEvent<float>();
        public UnityEvent OnShootSpawnEffects = new UnityEvent();
        public UnityEvent OnShootHitscanProjectile = new UnityEvent();

        // Damage / Hit Detection
        public UnityEvent<int, float, RaycastHit, bool> OnHit = new UnityEvent<int, float, RaycastHit, bool>();
        public UnityEvent<int, RaycastHit> OnInstantiateBulletHoleImpact = new UnityEvent<int, RaycastHit>();

        // AMMO / RELOAD
        public UnityEvent OnReduceAmmo = new UnityEvent();
        public UnityEvent<bool> OnAmmoChanged = new UnityEvent<bool>();
        public UnityEvent OnStartReload = new UnityEvent();
        public UnityEvent OnCancelReload = new UnityEvent();
        public UnityEvent OnFinishReload = new UnityEvent();
        public UnityEvent<bool, bool> OnReloadUIChanged = new UnityEvent<bool, bool>();
        public UnityEvent OnWeaponCooling = new UnityEvent();

        // AIMING
        public UnityEvent<float> OnAimStart = new UnityEvent<float>();
        public UnityEvent OnAiming = new UnityEvent();
        public UnityEvent OnAimStop = new UnityEvent();

        // WEAPON SELECTION
        public UnityEvent OnSelectWeapon = new UnityEvent();
        public UnityEvent<WeaponIdentification> OnEquipWeapon = new UnityEvent<WeaponIdentification>();
        public UnityEvent OnSwitchingWeapon = new UnityEvent();
        public UnityEvent<bool, bool> OnUnholster = new UnityEvent<bool, bool>();
        public UnityEvent OnUnselectingWeapon = new UnityEvent();
        public UnityEvent OnReleaseWeapon = new UnityEvent();

        // INVENTORY / ATTACHMENTS
        public UnityEvent<int, Weapon_SO> OnWeaponInventoryChanged = new UnityEvent<int, Weapon_SO>();
        public UnityEvent<WeaponIdentification, int, List<AttachmentIdentifier_SO>> OnAssignAttachmentsToWeapon =
            new UnityEvent<WeaponIdentification, int, List<AttachmentIdentifier_SO>>();

        // ENEMY DETECTION
        public UnityEvent<bool> OnEnemySpotted = new UnityEvent<bool>();

        // SECONDARY ATTACK
        public UnityEvent<Transform> OnSecondaryAttack = new UnityEvent<Transform>();

        // DATA REQUESTS
        public event Func<float> OnGetSpread;
        public float RequestSpread()
        {
            // If no listeners are subscribed, return default value
            return OnGetSpread?.Invoke() ?? 0f;
        }
    }

}
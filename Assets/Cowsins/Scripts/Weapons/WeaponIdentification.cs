/// <summary>
/// This script belongs to cowsins� as a part of the cowsins� FPS Engine. All rights reserved. 
/// </summary>
using UnityEngine;  
using System.Collections.Generic;
using UnityEditor;

namespace cowsins
{
    /// <summary>
    /// Attach this to your weapon object ( the one that goes in the weapon array of WeaponController )
    /// </summary>
    public class WeaponIdentification : MonoBehaviour
    {
        public Weapon_SO weapon;

        [Tooltip("Every weapon, excluding melee, must have a firePoint, which is the point where the bullet comes from." +
            "Just make an empty object, call it firePoint for organization purposes and attach it here. ")]
        public Transform[] FirePoint;

        [Tooltip("Optional. Reference to the point where bullet shells will be ejected. If left empty, the FirePoint will be used instead.")]
        public Transform shellEjectPoint;

        public Transform aimPoint;

        [HideInInspector] public int totalMagazines, magazineSize, bulletsLeftInMagazine, totalBullets;

        [SerializeField] private Transform headBone; 

        [Tooltip("Defines the default attachments for your weapon. The first time you pick it up, these attachments will be equipped."), SerializeField] private DefaultAttachment defaultAttachments;

        [Tooltip("Defines all the attachments that can be equipped on your weapon.")] public CompatibleAttachments compatibleAttachments;

        private AttachmentStateManager attachmentStateManager;

        [HideInInspector] public Vector3 originalAimPointPos, originalAimPointRot;

        [HideInInspector] public float heatRatio;

        private delegate void ReduceAmmoStyle();

        private ReduceAmmoStyle reduceAmmo;

        private IShootStyle shootBehaviour;
        public Transform HeadBone => headBone;

        private Animator animator;
        public Animator Animator => animator;

        public float damage;
        public float fireRate;
        public float baseSpread;
        public float baseAimSpread;
        public float aimSpeed;
        public float reloadTime;
        public float emptyReloadTime;
        public float weightMultiplier;
        public float camShakeAmount;
        public float penetrationAmount;

        public Vector3 aimingRotation;
        public Vector3 aimingOffset;

        public GameObject muzzleVFX;
        public AudioClip[] fireSFXs;

        private void OnEnable()
        {
            originalAimPointPos = aimPoint.localPosition;
            originalAimPointRot = aimPoint.localRotation.eulerAngles;
        }
        private void Awake()
        {
            animator = GetComponentInChildren<Animator>(true); 
            if(animator) animator.keepAnimatorStateOnDisable = true;

            totalMagazines = weapon.totalMagazines;

            // Initialize attachment state manager for this weapon
            attachmentStateManager = new AttachmentStateManager(this, defaultAttachments, compatibleAttachments);

            if(weapon.reloadStyle == ReloadingStyle.defaultReload)
                reduceAmmo = ReduceDefaultAmmo;
            else 
                reduceAmmo = ReduceOverheatAmmo;

            damage = weapon.damagePerBullet;
            fireRate = weapon.fireRate;
            baseSpread = weapon.applyBulletSpread ? weapon.spreadAmount : 0;
            baseAimSpread = weapon.applyBulletSpread ? weapon.aimSpreadAmount : 0;
            aimSpeed = weapon.aimingSpeed;
            reloadTime = weapon.reloadTime;
            emptyReloadTime = weapon.emptyReloadTime;
            weightMultiplier = weapon.weightMultiplier;
            camShakeAmount = weapon.camShakeAmount;
            penetrationAmount = weapon.penetrationAmount;

            aimingOffset = Vector3.zero;
            aimingRotation = weapon.aimingRotation;

            muzzleVFX = weapon.muzzleVFX;
            fireSFXs = weapon.audioSFX.shooting;
            magazineSize = weapon.magazineSize;
        }

        public void SetShootStyle(IShootStyle shootBehaviour) => this.shootBehaviour = shootBehaviour;

        public void Shoot(float spread, float damageMultiplier, float shakeMultiplier)
        {
            PickNextShot();
            shootBehaviour?.Shoot(spread, damageMultiplier, shakeMultiplier);
        }

        public void ReduceAmmo() => reduceAmmo?.Invoke();   

        public void ReduceOverheatAmmo()
        {
            heatRatio += (float)1f / magazineSize;
        }

        public void ReduceDefaultAmmo()
        {
            if (!weapon.infiniteBullets)
            {
                bulletsLeftInMagazine -= weapon.ammoCostPerFire;
                if (bulletsLeftInMagazine < 0)
                {
                    bulletsLeftInMagazine = 0;
                }
            }
        }

        public int CurrentShotIndex { get; private set; }

        public void PickNextShot()
        {
            CurrentShotIndex = Random.Range(0, weapon.amountOfShootAnimations);
        }

        public string GetCurrentShotAnimation()
        {
            if (weapon.amountOfShootAnimations <= 1) return "shooting";
            return CurrentShotIndex == 0 ? "shooting" : "shooting" + (CurrentShotIndex + 1).ToString();
        }

        public AudioClip GetFireSFX()
        {
            if (fireSFXs == null || fireSFXs.Length == 0) return null;

            // If the amount of animations matches the amount of sounds, they are paired 1:1.
            if (fireSFXs.Length == weapon.amountOfShootAnimations && weapon.amountOfShootAnimations > 1)
                return fireSFXs[CurrentShotIndex];

            // Otherwise, pick a random sound from the array for variety.
            return fireSFXs[Random.Range(0, fireSFXs.Length)];
        }


        // Returns the attachment state manager for this weapon.
        public AttachmentStateManager AttachmentState => attachmentStateManager;

#if UNITY_EDITOR

        #region Gizmos

        // Additional Weapon Information on the editor view
        Vector3 boxSize = new Vector3(0.1841836f, 0.14f, 0.54f);
        Vector3 boxPosition = new Vector3(0,-.2f,.6f);

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying)
            {
                Gizmos.color = new Color(1, 0, 0, 0.3f);

                Gizmos.DrawWireCube(transform.position + boxPosition, boxSize);
                Handles.Label(transform.position + boxPosition + Vector3.up * (boxSize.y / 2 + 0.1f), "Approximate Weapon Location");

                if(aimPoint)
                {
                    Gizmos.color = Color.blue;
                    Gizmos.DrawWireCube(aimPoint.position , Vector3.one * .02f);
                    Handles.Label(aimPoint.position + Vector3.up * .05f, "Aim Point");

                }

                for(int i = 0; i < FirePoint.Length; i++)
                {
                    if (FirePoint[i] != null)
                    {
                        Gizmos.color = Color.green;
                        Gizmos.DrawWireCube(FirePoint[i].position, Vector3.one * .02f);
                        Handles.Label(FirePoint[i].position + Vector3.up * .05f, "Fire Point " + (i + 1));

                    }
                }

                if (shellEjectPoint)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireCube(shellEjectPoint.position, Vector3.one * .02f);
                    Handles.Label(shellEjectPoint.position + Vector3.up * .05f, "Shell Eject Point");
                }
            }
        }
        #endregion
#endif
    }
}

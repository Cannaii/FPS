using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic; 

namespace cowsins
{
    [System.Serializable]
    public class WeaponControllerSettings
    {
        public enum HolsterBehaviour
        {
            None = 0,
            OnlyWhenSelectingNull = 1,
            Always = 2
        }
        [System.Serializable]
        public class Events
        {
            public UnityEvent OnShoot, OnStartAim, OnAiming, OnStopAim, onStartReload, OnFinishReload, OnSecondaryMelee, OnHit, OnCriticalHit, 
                OnAttachAttachment, OnUnholster, OnInventorySlotChanged, OnSelectWeapon;
        }
        [System.Serializable]
        public class CustomShotMethods
        {
            public Weapon_SO weapon;
            public UnityEvent OnShoot;
        }
        [System.Serializable]
        public class ImpactEffectEntry
        {
            public string layerName;
            public GameObject impact;

            [HideInInspector]
            public int cachedLayerIndex = -1;
        }

        [System.Serializable]
        public class ImpactEffects
        {
            public GameObject defaultImpact;

            public List<ImpactEffectEntry> impacts = new List<ImpactEffectEntry>();
        }

        // ---------- INVENTORY ----------

        [Tooltip("max amount of weapons you can have")] public int inventorySize;
        [Tooltip("An array that includes all your initial weapons.")] public Weapon_SO[] initialWeapons;
        public bool allowMouseWheelWeaponSwitch;
        public bool allowNumberKeyWeaponSwitch;


        // ---------- REFERENCES ----------

        [Tooltip("Attach your main camera")] public Camera mainCamera;
        [Tooltip("Attach your camera pivot object")] public Transform cameraPivot;
        [Tooltip("Attach your weapon holder")] public Transform weaponHolder;


        // ---------- QUICK ACTIONS (MELEE) ----------

        public bool canMelee;
        public GameObject meleeObject;
        public Transform meleeHeadBone;
        public float meleeAttackDamage;
        public float meleeRange;
        public float meleeCamShakeAmount;
        public float meleeDelay;
        public float reEnableMeleeAfterAction;
        public AudioClip meleeAudioClip;


        // ---------- RELOAD ----------

        [Tooltip("If true you won�t have to press the reload button when you run out of bullets")] public bool autoReload;
        [Tooltip("Time (in seconds) it takes to reload automatically when Auto Reload is triggered."), Min(0)] public float autoReloadDelay = .1f;
        [Tooltip("If true, the player will reload when pressing the reload key even if it�s currently unholstering a weapon.")] public bool allowReloadWhileUnholstering;


        // ---------- AIM ----------

        [Tooltip("If false, hold to aim, and release to stop aiming.")] public bool alternateAiming;


        // ---------- HIT ----------

        [Tooltip("What objects should be hit")] public LayerMask hitLayer;


        // ---------- OTHERS ----------

        [Tooltip("Controls when holster animations should be played when switching weapons.")]
        public HolsterBehaviour holsterBehaviour = HolsterBehaviour.OnlyWhenSelectingNull;

        public Events userEvents;
        public ImpactEffects impactEffects; 
        [Tooltip("Used for weapons with custom shot method. Here, " +
            "you can attach your scriptable objects and assign the method you want to call on shoot. " +
            "Please only assign those scriptable objects that use custom shot methods, Otherwise it won�t work or you will run into issues.")]
        public CustomShotMethods[] customPrimaryShot;
    }
}
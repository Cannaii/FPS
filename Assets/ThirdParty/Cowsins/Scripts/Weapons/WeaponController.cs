/// <summary>
/// This script belongs to cowsins™ as a part of the cowsins´ FPS Engine. All rights reserved. 
/// </summary>
using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor.Presets;
#endif

namespace cowsins
{
    [UnityEngine.Scripting.Preserve]
    public class WeaponController : MonoBehaviour, IWeaponReferenceProvider, IWeaponBehaviourProvider, IWeaponRecoilProvider, IWeaponEventsProvider
    {
        #region VARIABLES

        public WeaponControllerSettings settings = new WeaponControllerSettings();

        #endregion

        #region INTERNAL USE

        private PlayerDependencies playerDependencies;
        private IPlayerMovementStateProvider playerMovement; // IPlayerMovementStateProvider is implemented in PlayerMovement.cs
        private IPlayerMovementEventsProvider playerMovementEvents; // IPlayerMovementEventsProvider is implemented in PlayerMovement.cs
        private IInteractEventsProvider interactEvents; // IInteractEventsProvider is implemented in InteractManager.cs
        private IPlayerStatsProvider statsProvider; // IPlayerStatsProvider is implemented in PlayerStats.cs
        private IPlayerControlProvider playerControl; // IPlayerControlProvider is implemented in PlayerControl.cs
        private PlayerMultipliers playerMultipliers;
        private WeaponStates weaponStates;
        private PlayerMultipliers multipliers;

        #endregion

        #region INTERFACES
        public WeaponControllerEvents Events { get; private set; } = new WeaponControllerEvents();
        public Camera MainCamera => settings.mainCamera;
        public Weapon_SO Weapon { get; set; }
        public WeaponIdentification Id { get; set; }
        public int CurrentWeaponIndex { get; set; }
        public WeaponIdentification[] Inventory { get; set; }
        public AudioSource AudioSource { get; private set; }

        public bool IsMeleeAvailable { get; set; }
        public bool IsAiming { get; set; }
        public bool IsShooting { get; set; }
        public bool IsReloading { get; set; }
        public bool AlternateAiming => settings.alternateAiming;
        public float AimingCamShakeMultiplier { get; } = 1;
        public float CrouchingCamShakeMultiplier { get; set; } = 1;

        #endregion

        #region INITIALIZATION

        public RecoilSystem recoilSystem { get; private set; }
        public SpreadSystem spreadSystem { get; private set; }
        public HitDetectionSystem hitDetectionSystem { get; private set; }
        public WeaponWeightSystem weaponWeightSystem { get; private set; }
        public AttachmentsSystem attachmentsSystem { get; private set; }
        public WeaponEffectsSystem weaponEffectsSystem { get; private set; }
        public WeaponContext weaponContext { get; private set; }
        public AimBehaviour aimBehaviour { get; private set; }
        public ReloadBehaviour reloadBehaviour { get; private set; }
        public ShootBehaviour shootBehaviour { get; private set; }
        public QuickActionBehaviour quickActionBehaviour { get; private set; }
        public WeaponInventorySystem weaponInventoryBehaviour { get; private set; }
        
        private void Start()
        {
            GetReferences();
            weaponContext = new WeaponContext
            {
                Transform = this.transform,
                InputManager = playerDependencies.InputManager,
                Dependencies = playerDependencies,
                WeaponHolder = settings.weaponHolder,
                CoroutineRunner = this.GetComponent<MonoBehaviour>(),
                Settings = settings,
                AudioSource = AudioSource,
            };

            InitializeBehaviours();
            Events.OnInitializeWeaponSystem?.Invoke(settings.inventorySize);
            weaponInventoryBehaviour.GetInitialWeapons();

            InitialSettings();

            interactEvents.Events.OnDrop.AddListener(ReleaseCurrentWeapon);

            playerMovementEvents.Events.OnCrouchStart.AddListener(SetCrouchCamShakeMultiplier);
            playerMovementEvents.Events.OnCrouchStop.AddListener(ResetCrouchCamShakeMultiplier);
        }


        private void OnDisable()
        {
            // Unsubscribe from the method to avoid issues
            if (attachmentsSystem != null)
                UIEvents.onAttachmentUIElementClickedNewAttachment -= attachmentsSystem.AssignNewAttachment;
        }

        private void OnDestroy()
        {
            if (interactEvents != null && interactEvents.Events != null)
                interactEvents.Events.OnDrop.RemoveListener(ReleaseCurrentWeapon);

            if (playerMovementEvents != null && playerMovementEvents.Events != null)
            {
                playerMovementEvents.Events.OnCrouchStart.RemoveListener(SetCrouchCamShakeMultiplier);
                playerMovementEvents.Events.OnCrouchStop.RemoveListener(ResetCrouchCamShakeMultiplier);
            }
        }

        private void GetReferences()
        {
            playerDependencies = GetComponent<PlayerDependencies>();
            playerMovement = playerDependencies.PlayerMovementState;
            playerMovementEvents = playerDependencies.PlayerMovementEvents;
            interactEvents = playerDependencies.InteractEvents;
            playerControl = playerDependencies.PlayerControl;
            statsProvider = playerDependencies.PlayerStats;
            playerMultipliers = GetComponent<PlayerMultipliers>();
            weaponStates = GetComponent<WeaponStates>();
            multipliers = GetComponent<PlayerMultipliers>();

            AudioSource = GetComponent<AudioSource>();
            if (AudioSource == null) AudioSource = gameObject.AddComponent<AudioSource>();

            playerDependencies.InputManager.SetWeaponInputModes(settings);
        }

        private void InitialSettings()
        {
            CurrentWeaponIndex = 0;
        }

        private void InitializeBehaviours()
        {
            hitDetectionSystem = new HitDetectionSystem(weaponContext, settings);
            spreadSystem = new SpreadSystem(weaponContext);
            attachmentsSystem = new AttachmentsSystem(weaponContext, settings);
            weaponEffectsSystem = new WeaponEffectsSystem(weaponContext, settings);
            recoilSystem = new RecoilSystem(weaponContext);
            weaponWeightSystem = new WeaponWeightSystem(weaponContext);
            weaponInventoryBehaviour = new WeaponInventorySystem(weaponContext, settings);

            aimBehaviour = new AimBehaviour(weaponContext);
            reloadBehaviour = new ReloadBehaviour(weaponContext);
            shootBehaviour = new ShootBehaviour(weaponContext);
            quickActionBehaviour = new QuickActionBehaviour(weaponContext);
        }
        #endregion

        private void Update()
        {
            HandleCrosshairEnemySpot();
            recoilSystem?.Tick();
            reloadBehaviour?.HandleHeatRatio();
        }

        #region WEAPON UNHOLSTER & SET-UP 
        public void InstantiateWeapon(Weapon_SO newWeapon, int inventoryIndex) => InstantiateWeapon(newWeapon, inventoryIndex, null, null, null);

        public void InstantiateWeapon(Weapon_SO newWeapon, int inventoryIndex, int? _bulletsLeftInMagazine, int? _totalBullets) 
            => InstantiateWeapon(newWeapon, inventoryIndex, _bulletsLeftInMagazine, _totalBullets, null);

        public void InstantiateWeapon(Weapon_SO newWeapon, int inventoryIndex, int? _bulletsLeftInMagazine, int? _totalBullets, List<AttachmentIdentifier_SO> attachmentsToAssign)
        {
            weaponInventoryBehaviour.InstantiateWeapon(newWeapon, inventoryIndex, _bulletsLeftInMagazine, _totalBullets, attachmentsToAssign);
        }

        public void ReleaseCurrentWeapon() => ReleaseWeapon(CurrentWeaponIndex);

        public void ReleaseWeapon(int index) => weaponInventoryBehaviour.ReleaseWeapon(index);

        #endregion

        #region MODIFIERS
   
        private void SetCrouchCamShakeMultiplier()
        {
            CrouchingCamShakeMultiplier = Weapon ? Weapon.camShakeCrouchMultiplier : 1;
        }

        private void ResetCrouchCamShakeMultiplier()
        {
            CrouchingCamShakeMultiplier = 1;
        }
        #endregion

        #region UI
        private void HandleCrosshairEnemySpot()
        {
            // If we dont own a weapon yet, do not continue
            // If we dont use a crosshair stop right here
            if (Weapon == null)
            {
                Events.OnEnemySpotted?.Invoke(false);
                return;
            }

            // Detect enemies on aiming
            RaycastHit hit;

            if (Physics.Raycast(MainCamera.transform.position, MainCamera.transform.forward, out hit, Weapon.bulletRange))
            {
                if(hit.collider.CompareTag("Enemy") || hit.collider.CompareTag("Critical"))
                {
                    Events.OnEnemySpotted?.Invoke(true);
                    return;
                }
            }

            Events.OnEnemySpotted?.Invoke(false);
        }

        #endregion

        #region RECOIL

        public float RecoilPitchOffset => recoilSystem.recoilPitchOffset;
        public float RecoilYawOffset => recoilSystem.recoilYawOffset;

        #endregion

        #region UTILS
        public bool AddDuplicateWeaponAmmo(int amount)
        {
            return weaponInventoryBehaviour.AddDuplicateWeaponAmmo(amount);
        }

        public bool TryToAddWeapons(Weapon_SO weapon, int currentBullets, int totalBullets, List<AttachmentIdentifier_SO> attachments)
        {
            return weaponInventoryBehaviour.TryToAddWeapons(weapon, currentBullets, totalBullets, attachments);
        }

        public (Weapon_SO, int, int) SwapWeapons(Weapon_SO weapon, int currentBullets, int totalBullets, List<AttachmentIdentifier_SO> attachments)
        {
            return weaponInventoryBehaviour.SwapWeapons(weapon, currentBullets, totalBullets, attachments);
        }

        #endregion
    }
}
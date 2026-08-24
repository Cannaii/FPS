using UnityEngine;

namespace cowsins
{
    /// <summary>
    /// Stores references to all required dependencies for the Player to function properly.
    /// This class must not be removed and all the interfaces must be implemented.
    /// </summary>
    public class PlayerDependencies : MonoBehaviour
    {
        [SerializeField] private InputManager inputManager;
        [SerializeField] private CameraFOVManager cameraFOVManager;
        [SerializeField] private CameraEffects cameraEffects;
        [SerializeField] private WeaponEffects weaponEffects;
        [SerializeField] private UIController uiController;
        [SerializeField] private Crosshair crosshair;
        [SerializeField] private UIEffects uIEffects;

        public InputManager InputManager => inputManager;
        public CameraFOVManager CameraFOVManager => cameraFOVManager;
        public Crosshair Crosshair => crosshair;

        // PlayerMovement
        public IPlayerMovementEventsProvider PlayerMovementEvents { get; private set; }
        public IPlayerMovementStateProvider PlayerMovementState { get; private set; }

        // WeaponController
        public IWeaponReferenceProvider WeaponReference { get; private set; }
        public IWeaponBehaviourProvider WeaponBehaviour { get; private set; }
        public IWeaponRecoilProvider WeaponRecoil { get; private set; }
        public IWeaponEventsProvider WeaponEvents { get; private set; }

        // PlayerStats
        public IDamageable Damageable { get; private set; }
        public IPlayerStatsProvider PlayerStats { get; private set; }
        public IPlayerStatsEventsProvider PlayerStatsEvents { get; private set; }
        public IFallHeightProvider FallHeight { get; private set; }

        // InteractManager
        public IInteractManagerProvider InteractManager { get; private set; }
        public IInteractEventsProvider InteractEvents { get; private set; }

        // PlayerControl
        public IPlayerControlProvider PlayerControl { get; private set; }

        // PlayerMultipliers
        public IPlayerMultipliers PlayerMultipliers { get; private set; }


        private void Awake()
        {
            PlayerMovementEvents = GetRequiredComponent<IPlayerMovementEventsProvider>();
            PlayerMovementState = GetRequiredComponent<IPlayerMovementStateProvider>();

            WeaponReference = GetRequiredComponent<IWeaponReferenceProvider>();
            WeaponBehaviour = GetRequiredComponent<IWeaponBehaviourProvider>();
            WeaponRecoil = GetRequiredComponent<IWeaponRecoilProvider>();
            WeaponEvents = GetRequiredComponent<IWeaponEventsProvider>();

            Damageable = GetRequiredComponent<IDamageable>();
            PlayerStats = GetRequiredComponent<IPlayerStatsProvider>();
            PlayerStatsEvents = GetRequiredComponent<IPlayerStatsEventsProvider>();
            FallHeight = GetRequiredComponent<IFallHeightProvider>();

            InteractManager = GetRequiredComponent<IInteractManagerProvider>();
            InteractEvents = GetRequiredComponent<IInteractEventsProvider>();

            PlayerControl = GetRequiredComponent<IPlayerControlProvider>();

            PlayerMultipliers = GetRequiredComponent<IPlayerMultipliers>();

            inputManager?.SetPlayer(this);
            cameraFOVManager?.Initialize(this);
            cameraEffects?.Initialize(this);
            weaponEffects?.Initialize(this);
            uiController?.Initialize(this);
            uIEffects?.Initialize(this);
        }
        private T GetRequiredComponent<T>() where T : class
        {
            var component = GetComponent<T>();
            if (component == null) CowsinsUtilities.LogError("Missing required component of type {typeof(T).Name} on {gameObject.name}");
            return component;
        }
    }
}
using UnityEngine;

namespace cowsins
{
    public class PlayerDebugger : MonoBehaviour
    {
        private const float TopMargin = 130f;
        private const float BoxWidth = 320f;
        private const float LabelHeight = 20f;
        private const float LabelPadding = 5f;

        // Player Components
        private PlayerDependencies playerDependencies;
        private PlayerMovement playerMovement;
        private IPlayerStatsProvider playerStatsProvider; // IPlayerStatsProvider is implemented in PlayerStats.cs
        private IPlayerControlProvider playerControlProvider; // IPlayerControlProvider is implemented in PlayerControl.cs
        private IPlayerMovementStateProvider playerMovementProvider; // IPlayerMovementStateProvider is implemented in PlayerMovement.cs
        private IWeaponReferenceProvider weaponController; // IWeaponReferenceProvider is implemented in WeaponController.cs
        private IWeaponBehaviourProvider weaponBehaviour; // IWeaponBehaviourProvider is implemented in WeaponController.cs
        private IInteractManagerProvider interactManager; // IInteractManagerProvider is implemented in InteractManager.cs
        private PlayerStates playerStates;
        private WeaponStates weaponStates;
        private Rigidbody rb;
        private InputManager inputManager;

        // Get references
        private void Start()
        {
            playerDependencies = GetComponent<PlayerDependencies>();
            playerStates = GetComponent<PlayerStates>();
            weaponStates = GetComponent<WeaponStates>();
            rb = GetComponent<Rigidbody>();

            playerMovement = GetComponent<PlayerMovement>();
            playerStatsProvider = playerDependencies.PlayerStats;
            playerControlProvider = playerDependencies.PlayerControl;
            playerMovementProvider = playerDependencies.PlayerMovementState;
            weaponController = playerDependencies.WeaponReference;
            weaponBehaviour = playerDependencies.WeaponBehaviour;
            interactManager = playerDependencies.InteractManager;
            inputManager = playerDependencies.InputManager;
        }

        private float BeginBox(string title, ref float currentY)
        {
            float boxStartY = currentY;
            currentY += LabelHeight + LabelPadding;
            GUI.Box(new Rect(10, boxStartY, BoxWidth, 0), title);
            return boxStartY;
        }

        private void DrawLabel(ref float currentY, string label)
        {
            GUI.Label(new Rect(20, currentY, BoxWidth - 20, LabelHeight), label);
            currentY += LabelHeight + LabelPadding;
        }

        private void EndBox(float boxStartY, float currentY, string title)
        {
            float height = currentY - boxStartY;
            GUI.Box(new Rect(10, boxStartY, BoxWidth, height), title);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        void OnGUI()
        {
            float currentY = TopMargin;

            // Player Information Box
            float boxStartY = BeginBox("Player Information", ref currentY);
            DrawLabel(ref currentY, $"Player Is Idle: {playerMovementProvider.IsIdle}");
            DrawLabel(ref currentY, $"Player Current Speed: {Mathf.Round(playerMovementProvider.CurrentSpeed)}");
            DrawLabel(ref currentY, $"Player Velocity: {Mathf.Round(rb.velocity.magnitude)}");
            DrawLabel(ref currentY, $"Player Position: {transform.position}");
            DrawLabel(ref currentY, $"Player Orientation: {playerMovementProvider.Orientation.Forward}");
            DrawLabel(ref currentY, $"Player State: {playerStates?.CurrentState}");
            DrawLabel(ref currentY, $"Grounded: {playerMovementProvider.Grounded}");
            if(playerMovement != null)
            {
                DrawLabel(ref currentY, $"HasJumped: {playerMovement.movementContext.HasJumped}");
            }
            DrawLabel(ref currentY, $"Crouching: {playerMovementProvider.IsCrouching}");
            DrawLabel(ref currentY, $"Is Sliding: {playerMovementProvider.IsSliding}");
            if (playerMovement != null)
            {
                DrawLabel(ref currentY, $"Is Player On Slope: {playerMovement.movementContext.IsPlayerOnSlope}");
                DrawLabel(ref currentY, $"Current Jumps: {playerMovement.jumpBehaviour.jumpCount}");
                DrawLabel(ref currentY, $"Wall Running: {playerMovement.IsWallRunning}");
            }
            EndBox(boxStartY, currentY, "Player Information");

            // Weapon Information Box
            boxStartY = BeginBox("Weapon Information", ref currentY);
            DrawLabel(ref currentY, $"Weapon_SO: {weaponController.Weapon}");
            DrawLabel(ref currentY, $"Weapon Object: {weaponController.Id}");
            DrawLabel(ref currentY, $"Weapon Total Bullets: {weaponController.Id?.totalBullets}");
            DrawLabel(ref currentY, $"Weapon Current Bullets: {weaponController.Id?.bulletsLeftInMagazine}");
            DrawLabel(ref currentY, $"Reloading: {weaponBehaviour.IsReloading}");
            DrawLabel(ref currentY, $"Weapon State: {weaponStates?.CurrentState}");
            DrawLabel(ref currentY, $"Weapon Aiming: {weaponBehaviour.IsAiming}");
            EndBox(boxStartY, currentY, "Weapon Information");

            // Player Stats Information Box
            boxStartY = BeginBox("Player Stats Information", ref currentY);
            DrawLabel(ref currentY, $"Health: {Mathf.Round(playerStatsProvider.Health)} / {playerStatsProvider.MaxHealth}");
            DrawLabel(ref currentY, $"Shield: {Mathf.Round(playerStatsProvider.Shield)} / {playerStatsProvider.MaxShield}");
            DrawLabel(ref currentY, $"Controllable: {playerControlProvider.IsControllable}");
            EndBox(boxStartY, currentY, "Player Stats Information");

            // Interact Manager Information Box
            boxStartY = BeginBox("Interact Manager Information", ref currentY);
            DrawLabel(ref currentY, $"Highlighted Interactable: {interactManager.HighlightedInteractable?.name}");
            DrawLabel(ref currentY, $"Interact Progress: {interactManager.ProgressElapsed:F1}");
            EndBox(boxStartY, currentY, "Interact Manager Information");

            // Input Manager Box
            boxStartY = BeginBox("Input Manager", ref currentY);
            DrawLabel(ref currentY, $"Movement: ({inputManager.X:F1},{inputManager.Y:F1})");
            DrawLabel(ref currentY, $"Look: ({inputManager.Mousex:F1},{inputManager.Mousey:F1})");
            DrawLabel(ref currentY, $"Gamepad Look: ({inputManager.Controllerx:F1},{inputManager.Controllery:F1})");
            DrawLabel(ref currentY, $"Shooting: {inputManager.Shooting}");
            DrawLabel(ref currentY, $"Reloading: {inputManager.Reloading}");
            DrawLabel(ref currentY, $"Aiming: {inputManager.Aiming}");
            DrawLabel(ref currentY, $"Sprinting: {inputManager.Sprinting}");
            DrawLabel(ref currentY, $"Crouching: {inputManager.Crouching}");
            DrawLabel(ref currentY, $"Interacting: {inputManager.Interacting}");
            EndBox(boxStartY, currentY, "Input Manager");
        }
#endif
    }
}

/// <summary>
/// This script belongs to cowsins™ as a part of the cowsins´ FPS Engine. All rights reserved. 
/// </summary>
using UnityEngine;
using UnityEngine.Events;

namespace cowsins
{
    public class Crosshair : MonoBehaviour
    {
        [System.Serializable]   
        public class CrosshairEvents
        {
            public UnityEvent OnEnemySpotted, OnVisibilityChanged, OnCrosshairResized, OnCrosshairReset;
        }

        #region variables

        [Title("Variables"), Tooltip("Attach your PlayerMovement player "), SerializeField] private PlayerDependencies playerDependencies;
        [SerializeField, Tooltip("If true, the crosshair will resize to the default spread even if shooting.")] private bool resizeToDefaultIfShooting;
        [SerializeField, Tooltip("If enabled, the crosshair will not be displayed when the game is paused.")] private bool hideCrosshairOnPaused;
        [SerializeField, Tooltip("If enabled, the crosshair will not be displayed when the player is inspecting.")] private bool hideCrosshairOnInspecting;

        [Tooltip(" How much space it takes from your screen"), SerializeField]
        private float size = 10f;

        [Tooltip(" Thickness of the crosshair  "), SerializeField]
        private float width = 2f;

        [Tooltip(" Original spread you want to start with "), SerializeField]
        private float defaultSpread = 10f;

        [SerializeField] private bool resizeCrosshair;
        [SerializeField] private float walkSpread, runSpread, crouchSpread, jumpSpread;
        [SerializeField, Tooltip("Do not draw the crosshair when aiming a weapon")] private bool removeCrosshairOnAiming;

        [Tooltip(" Crosshair Color "), SerializeField]
        private Color defaultColor;

        [Tooltip(" Color of the crosshair whenever you aim at an enemy "), SerializeField]
        private Color enemySpottedColor;

        [SerializeField] private float enemySpottedWidth;

        [SerializeField] private float resizeSpeed = 3f;

        [SerializeField, Title("Events", upMargin = 10)] private CrosshairEvents crosshairEvents;

        private IPlayerStatsProvider playerStatsProvider; // IPlayerStatsProvider is implemented in PlayerStats.cs
        private IPlayerMovementStateProvider playerProvider; // IPlayerMovementStateProvider is implemented in PlayerMovement.cs
        private IWeaponReferenceProvider weaponController; // IWeaponReferenceProvider is implemented in WeaponController.cs
        private IWeaponBehaviourProvider weaponBehaviour; // IWeaponBehaviourProvider is implemented in WeaponController.cs
        private IWeaponEventsProvider weaponEvents; // IWeaponEventsProvider is implemented in WeaponController.cs
        private IInteractManagerProvider interactManager; // IInteractManagerProvider is implemented in InteractManager.cs
        private CrosshairShape crosshairShape;

        private Texture2D crosshairTexture;

        private bool isVisible = true;
        private float spread;
        private float originalWidth;
        private Color color = Color.grey;
        public bool IsVisible => isVisible;

        #endregion

        private void Awake()
        {
            ResetCrosshair();

            crosshairShape = GetComponent<CrosshairShape>();

            crosshairTexture = new Texture2D(1, 1);
            crosshairTexture.wrapMode = TextureWrapMode.Repeat;
        }

        private void Start()
        {
            playerStatsProvider = playerDependencies.PlayerStats;
            playerProvider = playerDependencies.PlayerMovementState;
            weaponController = playerDependencies.WeaponReference;
            weaponBehaviour = playerDependencies.WeaponBehaviour;
            weaponEvents = playerDependencies.WeaponEvents;
            interactManager = playerDependencies.InteractManager;

            weaponEvents.Events.OnShootHitscanProjectile.AddListener(Resize);
            weaponEvents.Events.OnShoot.AddListener(HideEnemySpotted);
            weaponEvents.Events.OnEnemySpotted.AddListener(SpotEnemy);
        }

        private void OnDestroy()
        {
            if (weaponEvents != null && weaponEvents.Events != null)
            {
                weaponEvents.Events.OnShootHitscanProjectile.RemoveListener(Resize);
                weaponEvents.Events.OnShoot.RemoveListener(HideEnemySpotted);
                weaponEvents.Events.OnEnemySpotted.RemoveListener(SpotEnemy);
            }
        }

        private void Update()
        {
            // If we are shooting do not continue
            if (weaponBehaviour.IsShooting && !resizeToDefaultIfShooting) return;   

            if (spread != defaultSpread) spread = Mathf.MoveTowards(spread, defaultSpread, resizeSpeed * Time.deltaTime / 10); // if this is not the current spread, fall back to the original one

            // Manage different sizes
            if (playerProvider.Grounded)
            {
                if (playerProvider.CurrentSpeed == playerProvider.RunSpeed && !playerProvider.IsIdle) Resize(runSpread);
                else
                {
                    if (playerProvider.CurrentSpeed == playerProvider.WalkSpeed)
                    {
                        if (playerProvider.IsIdle) Resize(defaultSpread);
                        else Resize(walkSpread);
                    }

                    if (playerProvider.CurrentSpeed == playerProvider.CrouchSpeed) Resize(crouchSpread);
                }
            }
            else Resize(jumpSpread);
        }

        /// <summary>
        /// Draw the crosshair as our UI
        /// </summary>
        void OnGUI()
        {
            if (playerStatsProvider.IsDead
                || weaponController.Weapon != null && weaponBehaviour.IsAiming && removeCrosshairOnAiming
                || PauseMenu.Instance != null && PauseMenu.isPaused && hideCrosshairOnPaused
                || interactManager.Inspecting && hideCrosshairOnInspecting
                || !isVisible) return;

            crosshairTexture.SetPixel(0, 0, color);
            crosshairTexture.Apply();

            CrosshairParts tempCrosshairParts = crosshairShape.CurrentCrosshairParts;

            if(tempCrosshairParts == null) return;

            if (tempCrosshairParts.downPart) GUI.DrawTexture(new Rect(Screen.width / 2 - width / 2, (Screen.height / 2 - size / 2) + spread / 2, width, size), crosshairTexture);

            if (tempCrosshairParts.topPart) GUI.DrawTexture(new Rect(Screen.width / 2 - width / 2, (Screen.height / 2 - size / 2) - spread / 2, width, size), crosshairTexture);

            if (tempCrosshairParts.rightPart) GUI.DrawTexture(new Rect((Screen.width / 2 - size / 2) + spread / 2, Screen.height / 2 - width / 2, size, width), crosshairTexture);

            if (tempCrosshairParts.leftPart) GUI.DrawTexture(new Rect((Screen.width / 2 - size / 2) - spread / 2, Screen.height / 2 - width / 2, size, width), crosshairTexture);

            Vector2 center = new Vector2(Screen.width / 2, Screen.height / 2);
            if (tempCrosshairParts.center)
            {
                float radius = Mathf.Min(width, size) / 2;
                Rect circleRect = new Rect(center.x - radius, center.y - radius, radius * 2, radius * 2);

                GUI.DrawTexture(circleRect, crosshairTexture);
            }

            if(tempCrosshairParts.topLeftBracket)
            {
                GUI.DrawTexture(new Rect(center.x - spread, center.y - spread, size, width), crosshairTexture);
                GUI.DrawTexture(new Rect(center.x - spread, center.y - spread, width, size), crosshairTexture);
            }

            if (tempCrosshairParts.topRightBracket)
            {
                GUI.DrawTexture(new Rect(center.x + spread - size, center.y - spread, size, width), crosshairTexture);
                GUI.DrawTexture(new Rect(center.x + spread - width, center.y - spread, width, size), crosshairTexture);
            }

            if (tempCrosshairParts.bottomLeftBracket)
            {
                GUI.DrawTexture(new Rect(center.x - spread, center.y + spread - width, size, width), crosshairTexture);
                GUI.DrawTexture(new Rect(center.x - spread, center.y + spread - size, width, size), crosshairTexture);
            }

            if (tempCrosshairParts.bottomRightBracket)
            {
                GUI.DrawTexture(new Rect(center.x + spread - size, center.y + spread - width, size, width), crosshairTexture);
                GUI.DrawTexture(new Rect(center.x + spread - width, center.y + spread - size, width, size), crosshairTexture);
            }
        }

        private void ResetCrosshair()
        {
            spread = defaultSpread;
            color = defaultColor;
            originalWidth = width;

            crosshairEvents.OnCrosshairReset?.Invoke();
        }

        /// <summary>
        /// Resize the crosshair based on the current weapon ( if it exists )
        /// </summary>
        public void Resize()
        {
            if(weaponController == null || weaponController.Weapon == null) return;

            Resize(weaponController.Weapon.crosshairResize * 10);
        }

        /// <summary>
        /// Resize the crosshair to a new value.
        /// </summary>
        public void Resize(float newSize)
        {
            if (!resizeCrosshair) return;

            spread = Mathf.Lerp(spread, newSize, resizeSpeed * Time.deltaTime);
            crosshairEvents.OnCrosshairResized?.Invoke();
        }
        /// <summary>
        /// Change color of the crosshair on spotting an enemy
        /// </summary>
        public void SpotEnemy(bool condition)
        {
            color = (condition) ? enemySpottedColor : defaultColor;
            width = (condition) ? Mathf.Lerp(width, enemySpottedWidth, resizeSpeed) : Mathf.Lerp(width, originalWidth, resizeSpeed);

            if (condition)
                crosshairEvents.OnEnemySpotted?.Invoke();
        }

        public void ShowEnemySpotted()
        {
            color = enemySpottedColor;
            width = Mathf.Lerp(width, enemySpottedWidth, resizeSpeed);

            crosshairEvents.OnEnemySpotted?.Invoke();
        }

        public void HideEnemySpotted()
        {
            color = defaultColor;
            width = Mathf.Lerp(width, originalWidth, resizeSpeed);
        }

        public void SetVisibility(bool visible)
        {
            isVisible = visible;

            crosshairEvents.OnVisibilityChanged?.Invoke();
        }
    }
}
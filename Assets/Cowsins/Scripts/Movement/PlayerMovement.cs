/// <summary>
/// This script belongs to cowsins™ as a part of the cowsins´ FPS Engine. All rights reserved. 
/// </summary>
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace cowsins
{
    // Add a rigidbody if needed, PlayerMovement.cs requires a rigidbody to work 
    [RequireComponent(typeof(Rigidbody))]
    //[RequireComponent(typeof(____))] Player Movement also requires a non trigger collider. Attach your preffered collider method
    public class PlayerMovement : MonoBehaviour, IPlayerMovementStateProvider, IPlayerMovementEventsProvider
    {
        #region Settings
        public PlayerMovementEvents Events { get; private set; } = new PlayerMovementEvents();

        public PlayerMovementSettings playerSettings = new PlayerMovementSettings();

        #endregion

        #region IPlayerMovementProvider

        // We need to satisfy the required interfaces
        public PlayerOrientation Orientation { get { return orientation; } set { orientation = value; } }
        public bool IsIdle => new Vector3(rb.velocity.x, 0, rb.velocity.z).magnitude < .1f;
        public float CurrentSpeed { get; set; }
        public float RunSpeed => playerSettings.runSpeed;
        public float WalkSpeed => playerSettings.walkSpeed;
        public float CrouchSpeed => playerSettings.crouchSpeed;
        public bool Grounded { get; set; }
        public bool IsCrouching { get; set; }
        public bool IsSliding { get; set; }  
        public bool IsClimbing { get; set; }
        public bool IsWallRunning { get; set; }
        public bool IsDashing { get; set; }
        public bool CanShootWhileDashing => playerSettings.canShootWhileDashing;
        public bool DamageProtectionWhileDashing => playerSettings.damageProtectionWhileDashing;
        public float NormalFOV => playerSettings.normalFOV;
        public float FadeFOVAmount => playerSettings.fadeFOVAmount;
        public float WallRunningFOV => playerSettings.wallrunningFOV;
        public float RunningFOV => playerSettings.runningFOV;
        public bool AlternateSprint => playerSettings.alternateSprint;
        public bool AlternateCrouch => playerSettings.alternateCrouch;

        private PlayerOrientation orientation = new PlayerOrientation(Vector3.zero, Quaternion.identity);

        #endregion

        #region Internal References

        private PlayerDependencies playerDependencies;
        private PlayerMultipliers playerMultipliers;
        private Rigidbody rb;
        private CapsuleCollider playerCapsuleCollider;
        private PlayerStates playerStates;
        private InputManager inputManager;

        #endregion

        #region Others

        private const float extraGravityForce = 30.19f;
        public bool showCapsuleGroundCheckDebugInfo = false;

        #endregion

        #region Behaviours
        public MovementContext movementContext { get; private set; }
        public StaminaBehaviour staminaBehaviour { get; private set; }
        public GroundDetectionBehaviour groundDetectionBehaviour { get; private set; }
        public SpeedLinesBehaviour speedLinesBehaviour { get; private set; }
        public FootstepsBehaviour footstepsBehaviour { get; private set; }
        public VelocityHandlerBehaviour velocityHandlerBehaviour { get; private set; }
        public BasicMovementBehaviour basicMovementBehaviour { get; private set; }
        public CameraLookBehaviour cameraLookBehaviour { get; private set; }
        public JumpBehaviour jumpBehaviour { get; private set; }
        public CrouchSlideBehaviour crouchSlideBehaviour { get; private set; }
        public DashBehaviour dashBehaviour { get; private set; }
        public WallBounceBehaviour wallBounceBehaviour { get; private set; }
        public ClimbLadderBehaviour climbLadderBehaviour { get; private set; }
        public WallRunBehaviour wallRunBehaviour { get; private set; }
        public GrapplingHookBehaviour grapplingHookBehaviour { get; private set; }

        #endregion

        #region Basic
        private void OnEnable() => Events.OnRespawn.AddListener(TeleportPlayer);

        private void OnDisable() => Events.OnRespawn.RemoveListener(TeleportPlayer);

        private void OnDestroy()
        {
            cameraLookBehaviour?.Dispose();
            staminaBehaviour?.Dispose();
            jumpBehaviour?.Dispose();
            footstepsBehaviour?.Dispose();
            wallRunBehaviour?.Dispose();
            crouchSlideBehaviour?.Dispose();
            velocityHandlerBehaviour?.Dispose();
            groundDetectionBehaviour?.Dispose();
            speedLinesBehaviour?.Dispose();
        }

        private void Start()
        {
            GetDependencies();
            ConfigureRigidbody();
            playerSettings.events.OnSpawn.Invoke();

            inputManager.SetPlayerInputModes(playerSettings);

            InitializeBehaviours();
        }

        private void Update()
        {
            groundDetectionBehaviour?.Tick();
            jumpBehaviour?.Tick();
        }

        private void FixedUpdate()
        {
            if (playerDependencies.PlayerStats.IsDead) return;

            // Added Gravity, only if we are not climbing to prevent unvoluntary sliding
            if (!IsClimbing && !movementContext.IsPlayerOnSlope) rb.AddForce(Vector3.down * extraGravityForce, ForceMode.Acceleration);

            if (!rb.isKinematic && rb.velocity.magnitude > playerSettings.maxSpeedAllowed) rb.velocity = Vector3.ClampMagnitude(rb.velocity, playerSettings.maxSpeedAllowed);

            staminaBehaviour?.Tick();
        }

        /// <summary>
        /// Basically find everything the script needs to work
        /// </summary>
        private void GetDependencies()
        {
            playerDependencies = GetComponent<PlayerDependencies>();
            rb = GetComponent<Rigidbody>();
            playerStates = GetComponent<PlayerStates>();
            playerMultipliers = GetComponent<PlayerMultipliers>();
            playerCapsuleCollider = GetComponent<CapsuleCollider>();
            inputManager = playerDependencies.InputManager;

            if (playerSettings.playerCam == null) CowsinsUtilities.LogWarning("PlayerCam is null in Player > PlayerMovement > Assignables. Skipping Camera Look", this);
            if (playerSettings.cameraFOVManager == null) CowsinsUtilities.LogWarning("CameraFOVManager is null in Player > PlayerMovement > Assignables.", this);
            if (playerSettings.useSpeedLines && playerSettings.speedLines == null) 
                CowsinsUtilities.LogWarning("SpeedLines Particle Effect is null in Player > PlayerMovement > Assignables.", this);
            if (playerSettings.usesStamina && playerSettings.staminaSlider == null)
                CowsinsUtilities.LogWarning("Stamina Slider is null in Player > PlayerMovement > Stamina. Skipping Stamina UI", this);
            if (!playerSettings.usesStamina && playerSettings.staminaSlider != null)
                playerSettings.staminaSlider.gameObject.SetActive(false);
        }

        private void ConfigureRigidbody()
        {
            if (rb == null) return;

            rb.freezeRotation = true;

            // Use continuous dynamic for better collision detection
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // Set interpolation for smooth movement
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            // Ensure no drag, we handle friction manually
            rb.drag = 0f;
            rb.angularDrag = 0.05f;
        }

        private void InitializeBehaviours()
        {
            movementContext = new MovementContext
            {
                Transform = this.transform,
                Rigidbody = rb,
                Capsule = playerCapsuleCollider,
                Camera = playerSettings.playerCam,
                WhatIsGround = playerSettings.whatIsGround,
                InputManager = inputManager,
                Settings = playerSettings,
                Dependencies = playerDependencies,
            };

            groundDetectionBehaviour = new GroundDetectionBehaviour(movementContext);
            basicMovementBehaviour = new BasicMovementBehaviour(movementContext);
            velocityHandlerBehaviour = new VelocityHandlerBehaviour(movementContext);
            cameraLookBehaviour = new CameraLookBehaviour(movementContext);
            jumpBehaviour = new JumpBehaviour(movementContext);
            crouchSlideBehaviour = new CrouchSlideBehaviour(movementContext);
            dashBehaviour = new DashBehaviour(movementContext);
            wallBounceBehaviour = new WallBounceBehaviour(movementContext);
            climbLadderBehaviour = new ClimbLadderBehaviour(movementContext);
            wallRunBehaviour = new WallRunBehaviour(movementContext);
            grapplingHookBehaviour = new GrapplingHookBehaviour(movementContext);
            staminaBehaviour = new StaminaBehaviour(movementContext);
            footstepsBehaviour = new FootstepsBehaviour(movementContext);
            speedLinesBehaviour = new SpeedLinesBehaviour(movementContext);
        }
        #endregion

        #region Collisions
        void OnCollisionEnter(Collision collision)
        {
            if (collision.gameObject.CompareTag("Weapons"))
            {
                Physics.IgnoreCollision(collision.collider, playerCapsuleCollider);
            }
        }

        #endregion

        #region Utils

        /// <summary>
        /// Teleport the player to the specified position and rotation.
        /// </summary>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        public void TeleportPlayer(Vector3 position, Quaternion rotation, bool resetStamina, bool resetDashes)
        {
            rb.position = position;
            playerSettings.playerCam.rotation = rotation;

            if(resetStamina) staminaBehaviour?.ResetStamina();
            if(resetDashes) dashBehaviour?.ResetDashes();

            playerStates.ForceChangeState(playerStates._States.Default());
        }
        #endregion

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (playerCapsuleCollider == null)
            {
                if (!TryGetComponent<CapsuleCollider>(out playerCapsuleCollider))
                    return;
            }

            Vector3 center = playerCapsuleCollider.transform.TransformPoint(playerCapsuleCollider.center);
            float halfHeight = Mathf.Max(0, (playerCapsuleCollider.height * 0.5f) - playerCapsuleCollider.radius);
            float radius = playerCapsuleCollider.radius * 0.95f;

            Vector3 bottom = center - Vector3.up * halfHeight;
            Vector3 top = center + Vector3.up * halfHeight;

            Vector3 castOffset = Vector3.down * playerSettings.groundCheckDistance;
            Vector3 bottomCast = bottom + castOffset;
            Vector3 topCast = top + castOffset;

            if(showCapsuleGroundCheckDebugInfo)
                DrawCapsule(bottomCast, topCast, radius, new Color(0f, 0f, 1f, 0.3f));
        }

        private void DrawCapsule(Vector3 bottom, Vector3 top, float radius, Color color)
        {
            Gizmos.color = color;

            Gizmos.DrawWireSphere(bottom, radius);
            Gizmos.DrawWireSphere(top, radius);

            Gizmos.DrawLine(bottom + Vector3.forward * radius, top + Vector3.forward * radius);
            Gizmos.DrawLine(bottom - Vector3.forward * radius, top - Vector3.forward * radius);
            Gizmos.DrawLine(bottom + Vector3.right * radius, top + Vector3.right * radius);
            Gizmos.DrawLine(bottom - Vector3.right * radius, top - Vector3.right * radius);
        }

#endif
    }
}
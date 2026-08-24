using UnityEngine;
using System.Collections;

namespace cowsins
{
    public class CameraEffects : MonoBehaviour
    {
        [SerializeField, Header("SHARED REFERENCES")] private Transform playerCamera;
        [SerializeField] private Transform camShakeTarget;

        [SerializeField, Header("TILT")] private float tiltSpeed;
        [SerializeField] private float tiltAmount;

        [SerializeField, Tooltip("Maximum Head Bob"), Header("HEAD BOB")] private float headBobAmplitude = 0.2f;
        [SerializeField, Tooltip("Speed to reach the Maximum Head Bob ( headBobAmplitude)")] private float headBobFrequency = 2f;
        [SerializeField, Range(0,1)] private float headBobCrouchMultiplier;

        [SerializeField, Tooltip("Maximum Breathing Amount"), Header("BREATHING EFFECT")] private float breathingAmplitude = 0.2f;
        [SerializeField, Tooltip("Breathing Speed")] private float breathingFrequency = 2f;
        [SerializeField, Tooltip("Enables Rotation for the Breathing Effect")] private bool applyBreathingRotation;




        [SerializeField, Header("LAND CAMERA SHAKE")] private float landShakeIntensity;
        [SerializeField] private float landShakeDuration;

        // Camera Shake
        private float trauma;
        public float Trauma { get { return trauma; } set { trauma = Mathf.Clamp01(value); } }

        private float power = 16;
        private float movementAmount = 0.8f;
        private float rotationAmount = 17f;

        private float traumaDepthMag = 0.6f;
        private float traumaDecay = 1.3f;

        float timeCounter = 0;

        private Coroutine landingShakeRoutine;

        private IPlayerMovementStateProvider player; // IPlayerMovementStateProvider is implemented in PlayerMovement.cs
        private IPlayerControlProvider playerControlProvider; // IPlayerControlProvider is implemented in PlayerControl.cs
        private IWeaponEventsProvider weaponEvents; // IWeaponEventsProvider is implemented in WeaponController.cs
        private Rigidbody rb;
        private InputManager inputManager;

        private Vector3 origPos;
        private Quaternion origRot;

        public void Initialize(PlayerDependencies playerDependencies)
        {
            player = playerDependencies.PlayerMovementState;
            playerControlProvider = playerDependencies.PlayerControl;
            weaponEvents = playerDependencies.WeaponEvents;
            rb = GetComponent<Rigidbody>();
            this.inputManager = playerDependencies.InputManager;

            playerDependencies.PlayerMovementEvents.Events.OnLand.AddListener(LandingShake);
            weaponEvents.Events.OnShootShake.AddListener(ShootShake);
        }

        private void OnEnable()
        {
            if (playerCamera == null)
            {
                CowsinsUtilities.LogErrorFormat("No <b><color=cyan>PlayerCamera</color></b> reference found in CameraEffects. " +
                    "Please assign this reference accordingly to fix this error.");
                return;
            }

            if (camShakeTarget == null)
            {
                CowsinsUtilities.LogErrorFormat("No <b><color=cyan>CamShakeTarget</color></b> reference found in CameraEffects. " +
                    "Please assign this reference accordingly to fix this error.");
                return;
            }

            origPos = playerCamera.localPosition;
            origRot = playerCamera.localRotation;
        }
        private void Update()
        {
            if (!playerControlProvider.IsControllable || playerCamera == null || camShakeTarget == null) return;

            UpdateTilt();

            UpdateHeadBob();
            UpdateBreathing();

            HandleCamShake();
        }

        private void UpdateTilt()
        {
            if (player.CurrentSpeed == 0) return;

            Quaternion rot = CalculateTilt();
            playerCamera.localRotation = Quaternion.Lerp(playerCamera.localRotation, rot, Time.deltaTime * tiltSpeed);
        }

        private Quaternion CalculateTilt()
        {
            float x = inputManager.X;
            float y = inputManager.Y;

            Vector3 vector = new Vector3(y, 0, -x).normalized * tiltAmount;

            return Quaternion.Euler(vector);
        }

        private void UpdateHeadBob()
        {
            if (player.IsIdle || inputManager.Jumping)
            {

                playerCamera.localPosition = Vector3.Lerp(playerCamera.localPosition, origPos, Time.deltaTime * 2f);
                playerCamera.localRotation = Quaternion.Lerp(playerCamera.localRotation, origRot, Time.deltaTime * 2f);
                return;
            }

            float angle = Time.timeSinceLevelLoad * headBobFrequency;
            float amplitude = player.IsCrouching ? headBobAmplitude * headBobCrouchMultiplier : headBobAmplitude;
            float distanceY = amplitude * Mathf.Sin(angle) / 400f;
            float distanceX = amplitude * Mathf.Cos(angle) / 100f;








            playerCamera.position = new Vector3(playerCamera.position.x, playerCamera.position.y + distanceY, playerCamera.position.z);
            playerCamera.Rotate(distanceX, 0, 0, Space.Self);
        }

        private void UpdateBreathing()
        {
            float angle = Time.timeSinceLevelLoad * breathingFrequency;
            float distance = breathingAmplitude * Mathf.Sin(angle) / 400f;
            float distanceRot = breathingAmplitude * Mathf.Cos(angle) / 100f;

            playerCamera.position = new Vector3(playerCamera.position.x, playerCamera.position.y + distance, playerCamera.position.z);

            if (applyBreathingRotation)
            {
                playerCamera.Rotate(distanceRot, 0, 0, Space.Self);
            }
        }

        #region CameraShake
        private float GetFloat(float seed) { return (Mathf.PerlinNoise(seed, timeCounter) - 0.5f) * 2f; }

        private Vector3 GetVec3() { return new Vector3(GetFloat(1), GetFloat(10), GetFloat(100) * traumaDepthMag); }

        private void HandleCamShake()
        {
            if (Trauma > 0)
            {
                timeCounter += Time.deltaTime * Mathf.Pow(Trauma, 0.3f) * power;

                Vector3 newPos = GetVec3() * movementAmount * Trauma;
                camShakeTarget.localPosition = newPos;

                camShakeTarget.localRotation = Quaternion.Euler(newPos * rotationAmount);

                Trauma -= Time.deltaTime * traumaDecay * (Trauma + 0.3f);
            }
            else
            {
                //lerp back towards default position and rotation once shake is done
                Vector3 newPos = Vector3.Lerp(camShakeTarget.localPosition, Vector3.zero, Time.deltaTime);
                camShakeTarget.localPosition = newPos;
                camShakeTarget.localRotation = Quaternion.Euler(newPos * rotationAmount);
            }
        }

        public void Shake(float amount, float _power, float _movementAmount, float _rotationAmount)
        {
            Trauma = amount;
            power = _power;
            movementAmount = _movementAmount;
            rotationAmount = _rotationAmount;
        }

        public void ShootShake(float amount)
        {
            Trauma += amount;
            power = 20;
            movementAmount = .8f;
            rotationAmount = 17f;
        }

        public void ExplosionShake(float distance)
        {
            Trauma += 10f / distance;
            power = 30;
            movementAmount = 1f;
            rotationAmount = 30f;
        }

        /// <summary>
        /// Triggers a vertical shake to simulate landing impact.
        /// </summary>
        public void LandingShake()
        {
            if (landingShakeRoutine != null) StopCoroutine(landingShakeRoutine);
            landingShakeRoutine = StartCoroutine(LandingShakeRoutine(landShakeIntensity, landShakeDuration));
        }

        private IEnumerator LandingShakeRoutine(float intensity, float duration)
        {
            if (playerCamera == null) yield return null;

            float elapsed = 0f;
            Vector3 startPos = playerCamera.localPosition;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float normalized = elapsed / duration;

                // Quick "down then up" bounce curve
                float curve = Mathf.Sin(normalized * Mathf.PI);
                // Exponential decay
                float decay = 1f - (normalized * normalized);
                float displacement = curve * decay * -intensity;

                // Apply displacement in world space
                Vector3 worldDisplacement = Vector3.up * displacement;
                Vector3 localDisplacement = playerCamera.parent.InverseTransformDirection(worldDisplacement);

                playerCamera.localPosition = startPos + localDisplacement;

                yield return null;
            }

            // Ensure reset
            playerCamera.localPosition = startPos;
        }

        #endregion
    }
}

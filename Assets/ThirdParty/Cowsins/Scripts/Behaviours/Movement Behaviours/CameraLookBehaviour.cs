using cowsins.Settings;
using UnityEngine;

namespace cowsins
{
    public class CameraLookBehaviour
    {
        private MovementContext context;
        private InputManager inputManager;
        private Rigidbody rb;
        private Transform camera;
        private IPlayerMovementStateProvider playerMovement;
        private IPlayerMovementEventsProvider playerEvents;
        private IWeaponBehaviourProvider weaponBehaviourProvider;
        private IWeaponReferenceProvider weaponReference;
        private IWeaponRecoilProvider weaponRecoil;
        private PlayerMovementSettings playerSettings;

        private float cameraPitch;
        private float cameraYaw;
        private float cameraRoll;

        // Controls the current sensitivity.
        // Sensitivity can be overrided by the Game Settings Manager
        private float currentSensX, currentSensY, currentControllerSensX, currentControllerSensY;
        private bool invertYLook, invertYLookController; 

        private PlayerOrientation orientation => playerMovement.Orientation;

        private bool subscribedToSettings = false;

        public CameraLookBehaviour(MovementContext context)
        {
            this.context = context;
            this.rb = context.Rigidbody;
            this.camera = context.Camera;

            this.inputManager = context.InputManager;

            this.playerMovement = context.Dependencies.PlayerMovementState;
            this.playerEvents = context.Dependencies.PlayerMovementEvents;
            this.weaponBehaviourProvider = context.Dependencies.WeaponBehaviour;
            this.weaponReference = context.Dependencies.WeaponReference;
            this.weaponRecoil = context.Dependencies.WeaponRecoil;
            this.playerSettings = context.Settings;

            GatherSensitivityValues();
            CheckSettingsSubscription();
        }

        private void CheckSettingsSubscription()
        {
            if (!subscribedToSettings && GameSettingsManager.Instance != null)
            {
                GameSettingsManager.Instance.OnSettingsLoaded += GatherSensitivityValues;
                GameSettingsManager.Instance.OnSettingsReset += GatherSensitivityValues;
                GameSettingsManager.Instance.OnSettingsApplied += GatherSensitivityValues;
                GatherSensitivityValues(); // Fetch again now that we found the instance
                subscribedToSettings = true;
            }
        }

        public void Dispose()
        {
            if (subscribedToSettings && GameSettingsManager.Instance != null)
            {
                GameSettingsManager.Instance.OnSettingsLoaded -= GatherSensitivityValues;
                GameSettingsManager.Instance.OnSettingsReset -= GatherSensitivityValues;
                GameSettingsManager.Instance.OnSettingsApplied -= GatherSensitivityValues;
                subscribedToSettings = false;
            }
        }

        public void Tick()
        {
            if (camera == null) return;
            CheckSettingsSubscription();

            int sensYInverted = invertYLook ? -1 : 1;
            int sensYInvertedController = invertYLookController ? 1 : -1;
            float sensitivityMultiplier = weaponBehaviourProvider.IsAiming ? playerSettings.aimingSensitivityMultiplier : 1;

            // Grab the Inputs from the user.
            float rawMouseX = inputManager.GatherRawMouseX(currentSensX, currentControllerSensX);
            float rawMouseY = inputManager.GatherRawMouseY(sensYInverted, sensYInvertedController, currentSensY, currentControllerSensY);
            float mouseX = rawMouseX * sensitivityMultiplier;
            float mouseY = rawMouseY * sensitivityMultiplier;

            // Calculate new yaw rotation ( around the y axis )
            cameraYaw = camera.rotation.eulerAngles.y + mouseX + weaponRecoil.RecoilYawOffset * Time.deltaTime;
            //Rotate Camera Pitch ( around x axis )
            cameraPitch -= mouseY - weaponRecoil.RecoilPitchOffset * Time.deltaTime;
            // Make sure we dont over- or under-rotate.
            // The reason why the value is 89.7 instead of 90 is to prevent errors with the wallrun
            cameraPitch = Mathf.Clamp(cameraPitch, -playerSettings.maxCameraAngle, playerSettings.maxCameraAngle);

            CalculateCameraRoll();

            ApplyCameraRotation();

            HandleAimAssist();
        }

        public void VerticalLook()
        {
            if (PauseMenu.isPaused || !playerSettings.allowVerticalLookWhileClimbing) return;
            CheckSettingsSubscription();

            int sensYInverted = invertYLook ? -1 : 1;
            int sensYInvertedController = invertYLookController ? -1 : 1;
            float sensitivityMultiplier = weaponBehaviourProvider.IsAiming ? playerSettings.aimingSensitivityMultiplier : 1;

            float rawMouseY = inputManager.GatherRawMouseY(
                sensYInverted,
                sensYInvertedController,
                currentSensY,
                currentControllerSensY
            );

            float mouseY = rawMouseY * sensitivityMultiplier;

            cameraPitch -= mouseY;
            cameraPitch = Mathf.Clamp(cameraPitch, -playerSettings.maxCameraAngle, playerSettings.maxCameraAngle);

            ApplyCameraRotation();
        }

        private void ApplyCameraRotation()
        {
            camera.rotation = Quaternion.Euler(cameraPitch, cameraYaw, cameraRoll);
            orientation.UpdateOrientation(context.Rigidbody.transform.position, cameraYaw);
        }


        private void CalculateCameraRoll()
        {
            if (playerMovement.IsWallRunning) cameraRoll = context.WallLeft ? Mathf.Lerp(cameraRoll, -playerSettings.wallrunCameraTiltAmount, Time.deltaTime * playerSettings.cameraTiltTransitionSpeed) : Mathf.Lerp(cameraRoll, playerSettings.wallrunCameraTiltAmount, Time.deltaTime * playerSettings.cameraTiltTransitionSpeed);
            else if (playerMovement.IsCrouching && playerMovement.CurrentSpeed >= playerMovement.WalkSpeed && playerEvents.Events.InvokeAllowSlide() && !context.HasJumped) cameraRoll = Mathf.Lerp(cameraRoll, playerSettings.slidingCameraTiltAmount, Time.deltaTime * playerSettings.cameraTiltTransitionSpeed);
            else cameraRoll = Mathf.Lerp(cameraRoll, 0, Time.deltaTime * playerSettings.cameraTiltTransitionSpeed);
        }

        private Transform currentAimAssistTarget;
        private float targetLockTimer;

        private void HandleAimAssist()
        {
            // Check if aim assist is enabled
            if (!playerSettings.applyAimAssist) return;

            // Only assist when having a weapon. Optional setting
            if (playerSettings.assistOnlyWithWeapons && weaponReference.Weapon == null) return;

            // Only assist when aiming down sights. Optional setting
            if (playerSettings.assistOnlyWhenAiming && !weaponBehaviourProvider.IsAiming)
            {
                currentAimAssistTarget = null;
                return;
            }

            // Handle target locking & maintain current target for smooth tracking
            Transform target = null;

            if (currentAimAssistTarget != null && targetLockTimer > 0)
            {
                // Check if current target is valid
                float distanceToCurrent = Vector3.Distance(currentAimAssistTarget.position, rb.transform.position);
                Vector3 _targetAimPoint = GetTargetAimPoint(currentAimAssistTarget);
                Vector3 directionToCurrent = (_targetAimPoint - camera.position).normalized;
                float angleToCurrent = Vector3.Angle(camera.forward, directionToCurrent);

                // Keep locked target if still in range and reasonable angle
                if (distanceToCurrent <= playerSettings.maximumDistanceToAssistAim &&
                    angleToCurrent <= playerSettings.aimAssistActivationAngle * 2f) // Allow wider angle for locked target
                {
                    target = currentAimAssistTarget;
                    targetLockTimer -= Time.deltaTime;
                }
                else
                {
                    // Lost the target, we need to find a new one
                    currentAimAssistTarget = null;
                    targetLockTimer = 0;
                }
            }

            // Find new target if we don't have a locked one just yet
            if (target == null)
            {
                target = AimAssistHit();
                if (target != null)
                {
                    currentAimAssistTarget = target;
                    targetLockTimer = playerSettings.targetLockDuration;
                }
                else
                {
                    currentAimAssistTarget = null;
                    return;
                }
            }

            // Calculate assist
            float distance = Vector3.Distance(target.position, rb.transform.position);
            Vector3 targetAimPoint = GetTargetAimPoint(target);
            Vector3 directionToTarget = (targetAimPoint - camera.position).normalized;
            float angleToTarget = Vector3.Angle(camera.forward, directionToTarget);

            // Only assist if player is aiming close to the target
            if (angleToTarget > playerSettings.aimAssistActivationAngle)
            {
                currentAimAssistTarget = null;
                return;
            }

            // Calculate assist strength with multiple falloff factors
            float distanceWeight = 1f - Mathf.Clamp01(distance / playerSettings.maximumDistanceToAssistAim);
            float angleWeight = 1f - Mathf.Clamp01(angleToTarget / playerSettings.aimAssistActivationAngle);

            // Use animation curve for more control over falloff. Falls back if no values are provided to avoid weird behaviours
            if (playerSettings.aimAssistFalloffCurve != null && playerSettings.aimAssistFalloffCurve.keys.Length > 0)
            {
                distanceWeight = playerSettings.aimAssistFalloffCurve.Evaluate(distance / playerSettings.maximumDistanceToAssistAim);
            }

            float assistStrength = distanceWeight * angleWeight;

            // Apply rotation assist
            if (assistStrength > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);

                camera.rotation = Quaternion.Slerp(
                    camera.rotation,
                    targetRotation,
                    Time.deltaTime * playerSettings.aimAssistSpeed * assistStrength
                );

                // Update yaw and pitch to match the assisted rotation
                Vector3 eulerAngles = camera.rotation.eulerAngles;
                cameraYaw = eulerAngles.y;
                cameraPitch = eulerAngles.x;

                if (cameraPitch > 180f) cameraPitch -= 360f;
            }
        }

        private Transform AimAssistHit()
        {
            float range = weaponReference.Weapon != null
                ? weaponReference.Weapon.bulletRange
                : 40f;

            // Find all potential enemy targets within range
            Collider[] nearbyEnemies = Physics.OverlapSphere(
                camera.position,
                range,
                LayerMask.GetMask("Enemy")
            );

            if (nearbyEnemies.Length == 0) return null;

            Transform bestTarget = null;
            float bestScore = float.MaxValue;

            foreach (var enemyCollider in nearbyEnemies)
            {
                // Gather the aim point from the target
                Vector3 targetPosition = GetTargetAimPoint(enemyCollider.transform);

                // Exclude Enemy layer from obstruction check to avoid hitting the target itself
                int obstructionMask = playerSettings.whatIsGround & ~LayerMask.GetMask("Enemy");
                RaycastHit obstacleHit;
                if (Physics.Linecast(camera.position, targetPosition, out obstacleHit, obstructionMask))
                    continue;

                // Skip if not visible in camera view
                Vector3 viewportPoint = Camera.main.WorldToViewportPoint(targetPosition);
                if (viewportPoint.z < 0 || viewportPoint.x < 0 || viewportPoint.x > 1 ||
                    viewportPoint.y < 0 || viewportPoint.y > 1)
                    continue;

                // Calculate direction
                Vector3 directionToEnemy = (targetPosition - camera.position).normalized;
                float angle = Vector3.Angle(camera.forward, directionToEnemy);

                // Skip enemies outside activation cone
                if (angle > playerSettings.aimAssistActivationAngle)
                    continue;

                float distance = Vector3.Distance(camera.position, targetPosition);

                // Score prioritizes enemies closer to crosshair over distance
                // Lower score is better
                float score = angle + (distance * 0.05f);

                if (score < bestScore)
                {
                    bestScore = score;
                    bestTarget = enemyCollider.transform;
                }
            }

            return bestTarget;
        }

        /// <summary>
        /// Gets a proper aim point on a target
        /// </summary>
        private Vector3 GetTargetAimPoint(Transform target)
        {
            // Find a collider
            if (target.TryGetComponent<Collider>(out var targetCollider))
            {
                Vector3 bounds = targetCollider.bounds.center;
                float heightOffset = targetCollider.bounds.extents.y * playerSettings.aimAssistHeightMultiplier;
                return new Vector3(bounds.x, bounds.y + heightOffset, bounds.z);
            }

            // Fallback
            return target.position + Vector3.up * playerSettings.aimAssistDefaultHeightOffset;
        }
        private void GatherSensitivityValues()
        {
            var inst = GameSettingsManager.Instance;
            if (inst)
            {
                currentSensX = inst.Controls.MouseSensitivityX.Value;
                currentSensY = inst.Controls.MouseSensitivityY.Value;
                currentControllerSensX = inst.Controls.ControllerSensitivityX.Value;
                currentControllerSensY = inst.Controls.ControllerSensitivityY.Value;
                invertYLook = inst.Controls.InvertY.Value;
                invertYLookController = inst.Controls.InvertYController.Value;
            }
            else
            {
                currentSensX = playerSettings.sensitivityX;
                currentSensY = playerSettings.sensitivityY;
                currentControllerSensX = playerSettings.controllerSensitivityX;
                currentControllerSensY = playerSettings.controllerSensitivityY;
                invertYLook = playerSettings.invertYSensitivty;
                invertYLookController = playerSettings.invertYControllerSensitivty;
            }
        }
    }

}
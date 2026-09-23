using UnityEngine;

namespace cowsins
{
    public class CameraFOVManager : MonoBehaviour
    {
        private float normalFOV;
        private float wallrunningFOV;
        private float runningFOV;
        private Camera cam;
        private IPlayerMovementStateProvider playerMovement; // IPlayerMovementStateProvider is implemented in PlayerMovement.cs
        private IWeaponReferenceProvider weaponReference; // IWeaponReferenceProvider is implemented in WeaponController.cs
        private IWeaponBehaviourProvider weaponBehaviour; // IWeaponBehaviourProvider is implemented in WeaponController.cs
        private IWeaponEventsProvider weaponEvents; // IWeaponEventsProvider is implemented in WeaponController.cs
        private float targetFOV;
        private float lerpSpeed;

        public void Initialize(PlayerDependencies playerDependencies)
        {
            cam = GetComponent<Camera>();
            playerMovement = playerDependencies.PlayerMovementState;
            weaponReference = playerDependencies.WeaponReference;
            weaponBehaviour = playerDependencies.WeaponBehaviour;
            weaponEvents = playerDependencies.WeaponEvents;

            normalFOV = playerMovement.NormalFOV; // Initialize baseFOV once in Start
            wallrunningFOV = playerMovement.WallRunningFOV;
            runningFOV = playerMovement.RunningFOV;
            targetFOV = normalFOV;

            cam.fieldOfView = normalFOV;

            playerDependencies.PlayerMovementEvents.Events.OnWallRunStart.AddListener(SetFOVToWallrun);
            playerDependencies.PlayerMovementEvents.Events.OnWallRunStop.AddListener(SetFOVToNormal);
            weaponEvents.Events.OnAimStop.AddListener(OnAimStop);
            weaponEvents.Events.OnAimStart.AddListener(OnAimStart);
            weaponEvents.Events.OnShootApplyFOV.AddListener(ForceAddFOV);
            weaponEvents.Events.OnSelectWeapon.AddListener(SetFOVOnUnholster);
        }

        private void Update()
        {
            // Smoothly interpolate FOV towards the target value
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, Time.deltaTime * lerpSpeed);
        }
        private bool AllowChangeFOV()
        {
            return weaponBehaviour.IsAiming && weaponReference.Weapon != null;
        }

        public void SetFOV(float fov, float speed)
        {
            if (AllowChangeFOV())
                return; // Not applicable if aiming
            targetFOV = fov;
            lerpSpeed = speed;  
        }

        public void SetFOV(float? fov)
        {
            if (AllowChangeFOV())
                return; // Not applicable if aiming
            targetFOV = fov.Value;
            lerpSpeed = playerMovement.FadeFOVAmount; 
        }

        public void ForceAddFOV(float fov)
        {
            cam.fieldOfView -= fov;
            lerpSpeed = playerMovement.FadeFOVAmount;
        }

        public void SetFOVToNormal() => SetFOV(normalFOV);
        public void SetFOVToWallrun() => SetFOV(wallrunningFOV);
        public void SetFOVToRun() => SetFOV(runningFOV);
        private void OnAimStart(float aimingOutSpeed)
        {
            Weapon_SO weapon = weaponReference.Weapon;
            if(weapon == null) return;

            SetFOV(weapon.aimingFOV, aimingOutSpeed);
        }
        private void OnAimStop()
        {
            SetFOV(playerMovement.IsWallRunning ? playerMovement.WallRunningFOV : playerMovement.NormalFOV);
        }

        private void SetFOVOnUnholster()
        {
            SetFOV(weaponBehaviour.IsAiming && weaponReference.Weapon != null ? weaponReference.Weapon.aimingFOV : playerMovement.NormalFOV); 
        }
    }
}

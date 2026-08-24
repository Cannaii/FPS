using UnityEngine;

namespace cowsins
{
    public class AimBehaviour
    {
        private WeaponContext context;
        private InputManager inputManager;
        private IWeaponBehaviourProvider weaponBehaviour;
        private IWeaponReferenceProvider weaponReference;
        private IWeaponEventsProvider weaponEvents;
        private IPlayerMovementStateProvider playerMovement;

        private float aimingOutSpeed;
        private float aimingCamShakeMultiplier;

        private Weapon_SO weapon => weaponReference.Weapon;
        private WeaponIdentification id => weaponReference.Id;
        private Camera mainCamera => weaponReference.MainCamera;
        private Transform weaponHolder => context.WeaponHolder;

        private WeaponControllerSettings settings;

        public AimBehaviour(WeaponContext context)
        {
            this.context = context;
            this.inputManager = context.InputManager;
            this.weaponBehaviour = context.Dependencies.WeaponBehaviour;
            this.weaponReference = context.Dependencies.WeaponReference;
            this.weaponEvents = context.Dependencies.WeaponEvents;
            this.playerMovement = context.Dependencies.PlayerMovementState;
            this.settings = context.Settings;

            weaponEvents.Events.OnSwitchingWeapon.AddListener(ForceAimReset);
        }

        public void Tick()
        {
            if (!weaponBehaviour.IsAiming)
            {
                settings.userEvents.OnStartAim?.Invoke();
                aimingOutSpeed = (weapon != null) ? id.aimSpeed : 2;
                
                weaponEvents.Events.OnAimStart?.Invoke(aimingOutSpeed);
            }
            weaponBehaviour.IsAiming = true;

            aimingCamShakeMultiplier = weapon.camShakeAimMultiplier;

            // Get distance from camera to weapons && Calculate new Aim Position locally
            float cameraDistance = mainCamera.nearClipPlane + weapon.aimDistance;
            Vector3 localForwardCamera = mainCamera.transform.position + mainCamera.transform.forward * cameraDistance;

            // Calculate Scope Offset if there�s any scope available
            Vector3 scopeOffset = Vector3.zero;
            Attachment scopeAttachment = id.AttachmentState.GetCurrent(AttachmentType.Scope);
            if (scopeAttachment)
            {
                Scope scopeComponent = scopeAttachment.GetComponent<Scope>();
                scopeOffset = mainCamera.transform.TransformVector(scopeComponent.aimingOffset);
            }
            Vector3 aimPosition = localForwardCamera + scopeOffset;

            if(!weapon.ignoreAimPoint)
            {
                id.aimPoint.position = Vector3.Lerp(id.aimPoint.position, aimPosition, id.aimSpeed * Time.deltaTime);
                id.aimPoint.localRotation = Quaternion.Lerp(id.aimPoint.localRotation, Quaternion.Euler(id.aimingRotation), id.aimSpeed * Time.deltaTime);
            }

            weaponEvents.Events.OnAiming?.Invoke();
            settings.userEvents.OnAiming?.Invoke();
        }

        public void Exit()
        {
            if (weaponBehaviour.IsAiming)
            {
                weaponBehaviour.IsAiming = false;

                settings.userEvents.OnStopAim?.Invoke();
                weaponEvents.Events.OnAimStop?.Invoke();
            }

            weaponBehaviour.IsAiming = false;
            aimingCamShakeMultiplier = 1;

            if (id == null || weapon == null) return;
            // Change the position and FOV
            if (!weapon.ignoreAimPoint)
            {
                id.aimPoint.localPosition = Vector3.Lerp(id.aimPoint.localPosition, id.originalAimPointPos, id.aimSpeed * Time.deltaTime);
                id.aimPoint.localRotation = Quaternion.Lerp(id.aimPoint.localRotation, Quaternion.Euler(id.originalAimPointRot), aimingOutSpeed * Time.deltaTime);
            }

            weaponHolder.localRotation = Quaternion.Lerp(weaponHolder.transform.localRotation, Quaternion.Euler(Vector3.zero), aimingOutSpeed * Time.deltaTime);
        }

        /// <summary>
        /// Forces the Weapon to go back to its initial position 
        /// </summary>
        public void ForceAimReset()
        {
            weaponBehaviour.IsAiming = false;
            if (weaponReference.Id?.aimPoint)
            {
                weaponReference.Id.aimPoint.localPosition = weaponReference.Id.originalAimPointPos;
                weaponReference.Id.aimPoint.localRotation = Quaternion.Euler(id.originalAimPointRot);
            }
            context.WeaponHolder.localRotation = Quaternion.Euler(Vector3.zero);
        }
    }

}
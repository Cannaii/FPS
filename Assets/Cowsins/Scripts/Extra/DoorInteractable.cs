/// <summary>
/// This script belongs to cowsins™ as a part of the cowsins´ FPS Engine. All rights reserved. 
/// </summary>
using UnityEngine;
using System.Collections;
namespace cowsins
{
    /// <summary>
    /// Inheriting from Interactable, this means you can interact with the door
    /// Keep in mind that this is highly subject to change on future updates
    /// </summary>
    [RequireComponent(typeof(BoxCollider))] // Require a trigger collider to detect side
    public class DoorInteractable : Interactable
    {
        [SerializeField, Title("DOOR INTERACTABLE", upMargin: 8, divider: true)] private string openInteractionText;

        [SerializeField] private string closeInteractionText;

        [SerializeField] private string lockedInteractionText;

        [SerializeField, SaveField] private bool isLocked;

        [Tooltip("The pivot point for the door"), SerializeField]
        private Transform doorPivot;

        [Tooltip("The Vector3 to add to the door on opened"), SerializeField] private Vector3 offsetPosition;

        [Tooltip("How much you want to rotate the door"), SerializeField]
        private float openedDoorRotation;

        [Tooltip("rotation speed"), SerializeField]
        private float speed;

        [SerializeField] private AudioClip openDoorSFX, closeDoorSFX, lockedDoorSFX;

        private Quaternion closedRot;

        private Vector3 initialPos;

        [SaveField] private int side;

        private Coroutine doorCoroutine;

        private void Start()
        {
            // Initial settings
            initialPos = doorPivot.position;
            closedRot = doorPivot.localRotation;
            interactText = isLocked ? lockedInteractionText : openInteractionText;
        }
 
        /// <summary>
        /// Check for interaction. Overriding from Interactable.cs
        /// </summary>
        public override void Interact(Transform player)
        {
            // Check if its locked
            if (isLocked)
            {
                SoundManager.Instance.PlaySound(lockedDoorSFX, 0, .1f, true);
                return;
            }
            // Change state
            alreadyInteracted = !alreadyInteracted;

            // Display appropriate UI
            interactText = isLocked ? lockedInteractionText :
                (alreadyInteracted ? closeInteractionText : openInteractionText);

            if (alreadyInteracted) SoundManager.Instance.PlaySound(openDoorSFX, 0, .1f, true);
            else SoundManager.Instance.PlaySound(closeDoorSFX, 0, .1f, true);

            IPlayerMovementStateProvider playerProvider = player.GetComponent<IPlayerMovementStateProvider>();
            // Checking the side where we are opening the door from;
            side = (Vector3.Dot(transform.right, playerProvider.Orientation.Forward) > 0) ? 1 : -1;

            // Stop any running coroutines before starting a new one
            if (doorCoroutine != null)
                StopCoroutine(doorCoroutine);

            // Start coroutine for opening/closing the door
            doorCoroutine = StartCoroutine(HandleDoorMovement());

            interactableEvents.OnInteract?.Invoke();
        }

        private IEnumerator HandleDoorMovement()
        {
            Vector3 targetPos = alreadyInteracted ? initialPos + offsetPosition : initialPos;
            Quaternion targetRot = alreadyInteracted
                ? Quaternion.Euler(new Vector3(doorPivot.localRotation.x, openedDoorRotation * side, doorPivot.localRotation.z))
                : closedRot;

            // Smoothly move and rotate the door until it reaches the target position and rotation
            while (Vector3.Distance(doorPivot.position, targetPos) > 0.01f || Quaternion.Angle(doorPivot.localRotation, targetRot) > 0.1f)
            {
                doorPivot.position = Vector3.Lerp(doorPivot.position, targetPos, Time.deltaTime * speed);
                doorPivot.localRotation = Quaternion.Lerp(doorPivot.localRotation, targetRot, Time.deltaTime * speed);
                yield return null;
            }

            // Ensure the door is exactly at the target position and rotation
            doorPivot.position = targetPos;
            doorPivot.localRotation = targetRot;
        }

        public void Lock()
        {
            isLocked = true;
            interactText = lockedInteractionText;
        }

        public void UnLock()
        {
            isLocked = false;
            interactText = openInteractionText;
        }

        public void ToggleLock()
        {
            isLocked = !isLocked;
            interactText = isLocked ? lockedInteractionText : openInteractionText;
        }

#if SAVE_LOAD_ADD_ON
        // Open or close the door based on whether it is interacted or not
        // InteractedState is called after loading
        public override void LoadedState()
        {
            if (alreadyInteracted)
            {
                doorPivot.position = initialPos + offsetPosition;
                doorPivot.localRotation = Quaternion.Euler(new Vector3(doorPivot.localRotation.x, openedDoorRotation * side, doorPivot.localRotation.z));
            }
            else
            {
                doorPivot.position = initialPos;
                doorPivot.localRotation = closedRot;
            }
        }
#endif
    }
}

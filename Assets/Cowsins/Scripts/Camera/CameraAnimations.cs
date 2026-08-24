using UnityEngine;

namespace cowsins
{
    public class CameraAnimations : MonoBehaviour
    {
        [SerializeField] private InteractManager interactManager;
        [SerializeField] private float rotationResetSpeed = 2;
        private Transform target;
        private Quaternion referenceRotation;

        private void OnEnable() => interactManager.userEvents.onDrop.AddListener(ResetTarget);
        private void OnDisable() => interactManager.userEvents.onDrop.RemoveListener(ResetTarget);

        private void Update()
        {
            if (target == null)
            {
                transform.localRotation = Quaternion.Lerp(transform.localRotation, Quaternion.Euler(Vector3.zero), Time.deltaTime * rotationResetSpeed);
                return;
            }

            // Apply delta from the reference
            Quaternion deltaRotation = Quaternion.Inverse(referenceRotation) * target.localRotation;
            transform.localRotation = deltaRotation;
        }

        /// <summary>
        /// Overrides the camera rotation target
        /// </summary>
        /// <param name="target">New target</param>
        public void SetTarget(Transform target)
        {
            this.target = target;
            referenceRotation = target != null ? target.localRotation : Quaternion.Euler(Vector3.zero);
        }

        public void ResetTarget(Pickeable pickeable) => SetTarget(null);
    }
}
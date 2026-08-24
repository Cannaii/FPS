namespace cowsins
{
    /// <summary>
    /// This script belongs to cowsins™ as a part of the cowsins´ FPS Engine. All rights reserved. 
    /// </summary>
    using UnityEngine;

    /// <summary>
    /// Keep camera in place
    /// </summary>
    public class MoveCamera : MonoBehaviour
    {
        [Tooltip("Reference to our Camera Head Transform, that defines the placement of our Camera"), SerializeField] private Transform cameraHead;

        private void Update() => transform.position = cameraHead.transform.position;
    }
}
using UnityEngine;

namespace cowsins
{
    /// <summary>
    /// Introduced in 1.3.8. Orientation used to be a Transform in the scene that rotated around the y axis ( yaw ). 
    /// Now, orientation information is stored inside PlayerOrientation class. PlayerMovement creates its own instance of PlayerOrientation.
    /// </summary>
    public class PlayerOrientation
    {
        private Vector3 position;
        private Quaternion rotation;

        public PlayerOrientation(Vector3 initialPosition, Quaternion initialRotation)
        {
            position = initialPosition;
            rotation = initialRotation;
        }

        public Vector3 Position
        {
            get => position;
            set => position = value + Vector3.up * 2;
        }

        public Quaternion Rotation
        {
            get => rotation;
            set => rotation = value;
        }


        public Vector3 Forward => rotation * Vector3.forward;
        public Vector3 Right => rotation * Vector3.right;
        public Vector3 Up => rotation * Vector3.up;
        public float Yaw => rotation.eulerAngles.y;


        public void SetRotation(Quaternion newRotation)
        {
            rotation = newRotation;
        }

        public void Rotate(Vector3 eulerAngles)
        {
            rotation *= Quaternion.Euler(eulerAngles);
        }

        public void UpdateOrientation(Vector3 pos, float desiredX)
        {
            position = pos;
            rotation = Quaternion.Euler(0, desiredX, 0);
        }
    }
}
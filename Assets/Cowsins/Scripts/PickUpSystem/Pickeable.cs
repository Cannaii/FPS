/// <summary>
/// This script belongs to cowsins™ as a part of the cowsins´ FPS Engine. All rights reserved. 
/// </summary>
using UnityEngine.UI;
using UnityEngine;
using UnityEngine.Events;
namespace cowsins
{
    public abstract class Pickeable : Interactable
    {
        [System.Serializable]
        public class Events
        {
            public UnityEvent OnPickUp;
        }
        [SerializeField] private Events events;

        [Tooltip("Apply the selected effect")]
        public bool rotates, translates;

        [Tooltip("Change the speed of the selected effect"), SerializeField]
        private float rotationSpeed, translationSpeed;

        [SerializeField] protected Image image;

        [Tooltip("Transform under which the graphics will be stored at when instantiated"), SerializeField]
        protected Transform graphics;

        [HideInInspector] public bool dropped;

        [HideInInspector] protected bool pickeable;

        private float timer = 0f;

        private Rigidbody rb;

        public virtual void Awake()
        {
            pickeable = false;
            rb = GetComponent<Rigidbody>();
        }
        public virtual void FixedUpdate()
        {
            if (rotates || translates) Movement();
        }

        public override void Interact(Transform player) => events.OnPickUp.Invoke();

        /// <summary>
        /// Apply effects, usually for more cartoony, stylized, anime approaches
        /// </summary>
        private void Movement()
        {
            rb?.AddForce(Vector3.down * 15, ForceMode.Force);
            if (graphics == null) return;

            // Rotation
            if (rotates)
                graphics.Rotate(Vector3.up * rotationSpeed * Time.fixedDeltaTime);

            // Translation (Up and Down Motion)
            if (translates)
            {
                timer += Time.deltaTime * translationSpeed;
                float translateMotion = Mathf.Sin(timer) / 500f;
                Vector3 localPos = graphics.localPosition;
                localPos.y += translateMotion;
                graphics.localPosition = localPos;
            }
        }

        public virtual void Drop(PlayerDependencies playerDependencies, PlayerOrientation orientation)
        {
            dropped = true;

            Vector3 force = orientation.Forward * 4;

            if (rb == null)
            {
                CowsinsUtilities.LogError("<b><color=yellow>Rigidbody component not found!</color></b> " +
                    "Please assign a <b><color=cyan>Rigidbody Component</color></b> to your Pickeable Object to fix this error.", this);
            }
            else rb.AddForce(force, ForceMode.VelocityChange);
        }

        public virtual void DestroyGraphics() => Destroy(graphics.GetChild(0).gameObject);
    }
}
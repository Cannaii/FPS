using UnityEngine;
using UnityEngine.Events;

namespace cowsins
{
    public class Experience : Trigger
    {
        [SerializeField] private float minXp, maxXp;

        [SerializeField, Tooltip("Sound on picking up ")] private AudioClip pickUpSFX;

        public UnityEvent onCollect;

        [SerializeField] private Transform graphics;

        [Tooltip("Apply the selected effect")]
        public bool rotates, translates;

        [Tooltip("Change the speed of the selected effect"), SerializeField]
        private float rotationSpeed, translationSpeed;

        private float timer = 0f;

        public override void TriggerEnter(Collider other)
        {
            if (ExperienceManager.Instance == null || !ExperienceManager.Instance.useExperience) return; // If we are not using XP in our game we should not pick up XP or we should not be able to.

            onCollect?.Invoke();

            // Generate a random amount of XP.
            float amount = Random.Range(minXp, maxXp);
            // Add the experience to the player.
            ExperienceManager.Instance.AddExperience(amount);
            UIEvents.onExperienceCollected?.Invoke(true);

            // Play SFX
            SoundManager.Instance.PlaySound(pickUpSFX, 0, 0, false);
            // Destroy the XP.
            Destroy(this.gameObject);
        }
        private void Update() => Movement();

        private void Movement()
        {
            if (!rotates && !translates) return;
            if (rotates) graphics.Rotate(Vector3.up * rotationSpeed * Time.deltaTime); // Rotate over time
            if (translates) // Go up and down
            {
                timer += Time.deltaTime * translationSpeed; // Timer that controls the movement
                float translateMotion = Mathf.Sin(timer) / 2000f;
                graphics.transform.localPosition = new Vector3(graphics.transform.localPosition.x, graphics.transform.localPosition.y + translateMotion, graphics.transform.localPosition.z);
            }
        }

#if SAVE_LOAD_ADD_ON
        public override void LoadedState()
        {
            Destroy(this.gameObject);
        }
#endif
    }
}

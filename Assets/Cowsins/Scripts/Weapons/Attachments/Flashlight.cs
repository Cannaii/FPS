using UnityEngine;
namespace cowsins
{
    public class Flashlight : Attachment
    {
        [Title("FLASHLIGHT")]
        [Tooltip("Light for the flashlight"), SerializeField] private Light lightSource;
        [Tooltip("SFX for turning on and off."), SerializeField] private AudioClip turnOnSFX, turnOffSFX;

        private bool turnedOn;

        public bool TurnedOn { get { return turnedOn; } }

        private void Start()
        {
            turnedOn = false;
        }

        public override void AttachmentAction()
        {
            bool newLightState = !lightSource.gameObject.activeSelf;

            EnableFlashLight(newLightState);
            CheckIfCanTurnOn(newLightState);

            SoundManager.Instance.PlaySound(newLightState ? turnOnSFX : turnOffSFX, 0, 0, true);
        }

        private void CheckIfCanTurnOn(bool cond)
        {
            // Check if we can turn it on
            if (cond)
            {
                // Conditions are met, turn off
                turnedOn = false;
                return;
            }
            // Turn on
            turnedOn = true;
        }

        /// <summary>
        /// Forces the flashlight to turn on.
        /// </summary>
        /// <param name="cond"></param>
        private void EnableFlashLight(bool cond)
        {
            // If the condition is met, enable the flashlight light, if not, disable it and turn it off.
            if (cond) lightSource.gameObject.SetActive(true);
            else
            {
                turnedOn = false;
                lightSource.gameObject.SetActive(false);
            }
        }
    }
}
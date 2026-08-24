using UnityEngine;
using System.Collections;

namespace cowsins
{
    public class ProceduralShot : MonoBehaviour
    {
        [SerializeField] private PlayerDependencies player;
        
        private IWeaponBehaviourProvider weaponController;

        private ProceduralShot_SO pattern;

        static ProceduralShot instance;
        public static ProceduralShot Instance => instance;

        private Coroutine shotCoroutine;

        private void Start()
        {
            instance = this;
            if(player == null)
            {
                CowsinsUtilities.LogError("<b><color=yellow>Player Reference not found!</color></b> " +
                "Ignore this if you are not planning to use the Procedural Shot Feature.");
                return;
            }
            weaponController = player.WeaponBehaviour;

            if (weaponController == null)
            {
                CowsinsUtilities.LogError("<b><color=yellow>Player Reference does not provide a Weapon Controller!</color></b> " +
                "Please, ensure the referenced Player is the Weapon Controller to solve this error.");
            }
        }

        /// <summary>
        /// Start a Procedural Shot motion given a ProceduralShot_SO ( Procedural Shot Pattern )
        /// </summary>
        public void Shoot(ProceduralShot_SO shot)
        {
            pattern = shot;

            if (shotCoroutine != null) StopCoroutine(shotCoroutine); // Stop any ongoing shot motion
            shotCoroutine = StartCoroutine(ApplyShotMotion());
        }

        private IEnumerator ApplyShotMotion()
        {
            float timer = 0; // Reset the timer

            while (timer < 1) // Continue the motion while timer is below 1
            {
                timer += Time.deltaTime * pattern.playSpeed; // Increase the timer

                // Evaluate positions
                float x = pattern.translation.xTranslation.Evaluate(timer);
                float y = pattern.translation.yTranslation.Evaluate(timer);
                float z = pattern.translation.zTranslation.Evaluate(timer);

                // Evaluate rotations
                float xRot = pattern.rotation.xRotation.Evaluate(timer);
                float yRot = pattern.rotation.yRotation.Evaluate(timer);
                float zRot = pattern.rotation.zRotation.Evaluate(timer);

                // Get the aiming multipliers depending on the state of the WeaponController
                float aimingTransl = weaponController.IsAiming && pattern != null ? pattern.aimingTranslationMultiplier : 1;
                float aimingRot = weaponController.IsAiming && pattern != null ? pattern.aimingRotationMultiplier : 1;

                // Apply the motions
                transform.localPosition = new Vector3(
                    x * pattern.translationDistance.x,
                    y * pattern.translationDistance.y,
                    z * pattern.translationDistance.z
                ) * aimingTransl;

                transform.localRotation = Quaternion.Euler(new Vector3(
                    xRot * pattern.rotationDistance.x,
                    yRot * pattern.rotationDistance.y,
                    zRot * pattern.rotationDistance.z
                ) * aimingRot);

                yield return null; // Wait for the next frame
            }
        }
    }
}

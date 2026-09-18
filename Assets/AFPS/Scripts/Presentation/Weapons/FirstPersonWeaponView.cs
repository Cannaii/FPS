using AFPS.Simulation.Characters;
using UnityEngine;

namespace AFPS.Presentation.Weapons
{
    /// <summary>
    /// Drives the local first-person weapon animator from the predicted player state.
    /// This presentation component depends only on AFPS simulation state.
    /// </summary>
    public sealed class FirstPersonWeaponView : MonoBehaviour
    {
        private static readonly int SpeedParameter = Animator.StringToHash("Speed");
        private static readonly int GroundedParameter = Animator.StringToHash("Grounded");
        private static readonly int FireParameter = Animator.StringToHash("Fire");

        [SerializeField] private Animator animator;
        [SerializeField, Min(0.01f)] private float fullSpeed = 6f;
        [SerializeField, Min(0f)] private float speedDampTime = 0.08f;
        [SerializeField] private ParticleSystem muzzleFlash;
        [SerializeField] private AudioSource shotAudio;
        [SerializeField] private Transform recoilPivot;
        [SerializeField, Min(0f)] private float recoilAngle = 3f;
        [SerializeField, Min(0f)] private float recoilReturnSpeed = 18f;
        [SerializeField] private PredictedProjectileVisual predictedProjectilePrefab;
        [SerializeField] private Transform projectileSpawnPoint;
        [SerializeField, Min(0f)] private float predictedProjectileSpeed = 30f;
        [SerializeField, Min(0.01f)] private float predictedProjectileLifetime = 3f;

        private bool hasSpeedParameter;
        private bool hasGroundedParameter;
        private bool hasFireParameter;
        private float currentRecoil;
        private Quaternion recoilBaseRotation;

        private void Awake()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>(true);
            }

            CacheParameters();
            if (recoilPivot == null)
            {
                recoilPivot = transform;
            }

            recoilBaseRotation = recoilPivot.localRotation;
        }

        public void Render(in PlayerState state, float deltaTime)
        {
            if (animator != null && hasSpeedParameter)
            {
                Vector3 planarVelocity = new Vector3(state.Velocity.x, 0f, state.Velocity.z);
                float normalizedSpeed = Mathf.Clamp01(planarVelocity.magnitude / fullSpeed);
                animator.SetFloat(SpeedParameter, normalizedSpeed, speedDampTime, deltaTime);
            }

            if (animator != null && hasGroundedParameter)
            {
                animator.SetBool(GroundedParameter, state.IsGrounded);
            }

            currentRecoil = Mathf.MoveTowards(currentRecoil, 0f, recoilReturnSpeed * deltaTime);
            if (recoilPivot != null)
            {
                recoilPivot.localRotation = recoilBaseRotation * Quaternion.Euler(-currentRecoil, 0f, 0f);
            }
        }

        /// <summary>立即播放本地预测射击表现；命中和伤害仍等待服务器裁决。</summary>
        public void PlayPredictedShot()
        {
            if (animator != null && hasFireParameter)
            {
                animator.SetTrigger(FireParameter);
            }

            // UnityEngine.Object's null-conditional operator bypasses Unity's overloaded equality.
            // Use the explicit check for unassigned or destroyed Inspector references.
            if (muzzleFlash != null)
            {
                muzzleFlash.Play(true);
            }
            if (shotAudio != null)
            {
                shotAudio.Play();
            }

            currentRecoil += recoilAngle;
            if (predictedProjectilePrefab != null)
            {
                Transform spawn = projectileSpawnPoint != null ? projectileSpawnPoint : transform;
                PredictedProjectileVisual projectile = Instantiate(predictedProjectilePrefab, spawn.position, spawn.rotation);
                projectile.Initialize(spawn.forward, predictedProjectileSpeed, predictedProjectileLifetime);
            }
        }

        private void CacheParameters()
        {
            hasSpeedParameter = false;
            hasGroundedParameter = false;
            hasFireParameter = false;
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return;
            }

            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].nameHash == SpeedParameter && parameters[i].type == AnimatorControllerParameterType.Float)
                {
                    hasSpeedParameter = true;
                }
                else if (parameters[i].nameHash == GroundedParameter && parameters[i].type == AnimatorControllerParameterType.Bool)
                {
                    hasGroundedParameter = true;
                }
                else if (parameters[i].nameHash == FireParameter && parameters[i].type == AnimatorControllerParameterType.Trigger)
                {
                    hasFireParameter = true;
                }
            }
        }
    }
}

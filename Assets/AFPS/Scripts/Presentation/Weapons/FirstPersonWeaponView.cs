using AFPS.Simulation.Characters;
using UnityEngine;

namespace AFPS.Presentation.Weapons
{
    /// <summary>
    /// Drives the local first-person weapon animator from the predicted player state.
    /// This presentation component deliberately has no dependency on Cowsins runtime scripts.
    /// </summary>
    public sealed class FirstPersonWeaponView : MonoBehaviour
    {
        private static readonly int SpeedParameter = Animator.StringToHash("Speed");
        private static readonly int GroundedParameter = Animator.StringToHash("Grounded");

        [SerializeField] private Animator animator;
        [SerializeField, Min(0.01f)] private float fullSpeed = 6f;
        [SerializeField, Min(0f)] private float speedDampTime = 0.08f;

        private bool hasSpeedParameter;
        private bool hasGroundedParameter;

        private void Awake()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>(true);
            }

            CacheParameters();
        }

        public void Render(in PlayerState state, float deltaTime)
        {
            if (animator == null)
            {
                return;
            }

            if (hasSpeedParameter)
            {
                Vector3 planarVelocity = new Vector3(state.Velocity.x, 0f, state.Velocity.z);
                float normalizedSpeed = Mathf.Clamp01(planarVelocity.magnitude / fullSpeed);
                animator.SetFloat(SpeedParameter, normalizedSpeed, speedDampTime, deltaTime);
            }

            if (hasGroundedParameter)
            {
                animator.SetBool(GroundedParameter, state.IsGrounded);
            }
        }

        private void CacheParameters()
        {
            hasSpeedParameter = false;
            hasGroundedParameter = false;
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
            }
        }
    }
}

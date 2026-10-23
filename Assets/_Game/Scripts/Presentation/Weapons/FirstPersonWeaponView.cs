using AFPS.Simulation.Characters;
using UnityEngine;

namespace AFPS.Presentation.Weapons
{
    /// <summary>
    /// Drives the local first-person weapon animator from the predicted player state.
    /// This presentation component depends only on AFPS simulation state.
    /// </summary>
    public sealed partial class FirstPersonWeaponView : MonoBehaviour
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
        // 横轴为真实秒、纵轴为相对冲击幅度；快速抬升后回到零，不影响瞄准输入。
        [SerializeField] private AnimationCurve recoilCurve = new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(0.025f, 1f),
            new Keyframe(0.09f, 0.35f), new Keyframe(0.22f, 0f));

        private bool hasSpeedParameter;
        private bool hasGroundedParameter;
        private bool hasFireParameter;
        private readonly System.Collections.Generic.List<float> recoilAges = new System.Collections.Generic.List<float>();
        private Quaternion recoilBaseRotation;
        private Vector3 recoilBasePosition;

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
            recoilBasePosition = recoilPivot.localPosition;
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

            float elapsed = Mathf.Max(0f, deltaTime);
            float currentRecoil = 0f;
            float duration = recoilCurve != null && recoilCurve.length > 0
                ? Mathf.Max(0.001f, recoilCurve[recoilCurve.length - 1].time) : 0.22f;
            for (int i = recoilAges.Count - 1; i >= 0; i--)
            {
                recoilAges[i] += elapsed;
                if (recoilAges[i] >= duration)
                {
                    recoilAges.RemoveAt(i);
                    continue;
                }
                float weight = recoilCurve != null && recoilCurve.length > 0
                    ? recoilCurve.Evaluate(recoilAges[i]) : 1f - recoilAges[i] / duration;
                currentRecoil += recoilAngle * weight;
            }
            RecoilWeight = Mathf.Clamp01(currentRecoil / Mathf.Max(0.01f, recoilAngle));
            if (recoilPivot != null)
            {
                recoilPivot.localRotation = recoilBaseRotation * Quaternion.Euler(
                    -Mathf.Min(9f, currentRecoil) + (Reloading ? 12f : 0f), 0f, Reloading ? -12f : 0f);
            }
            AdvancePresentation(elapsed, state);
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

            if (recoilAges.Count >= 16)
            {
                recoilAges.RemoveAt(0);
            }
            recoilAges.Add(0f);
            PlayOwnedAnimation("Fire");
            FlashRemaining = 0.04f;
            // 弹丸由 ShotResultView 按射击身份统一管理，避免创建无法关联权威结果的匿名代理。
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

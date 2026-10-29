using AFPS.NetCode.SnapshotInterpolation;
using UnityEngine;

namespace AFPS.Presentation.Characters
{
    /// <summary>自有人形开发模型的只读姿态；不创建碰撞体，也不驱动权威根节点。</summary>
    public sealed class RemoteCharacterPoseView : MonoBehaviour
    {
        // 单位米：与生成器的两段0.43米腿骨、1.8米站立高度保持一致。
        private const float LegLength = 0.43f;
        private const float StandingHipHeight = 0.92f;
        private const float FootHeight = 0.06f;
        private const float StandingHeight = 1.8f;
        private const float StrideLength = 1.6f;

        [SerializeField] private Transform hips;
        [SerializeField] private Transform upperBody;
        [SerializeField] private Transform leftThigh;
        [SerializeField] private Transform rightThigh;
        [SerializeField] private Transform leftKnee;
        [SerializeField] private Transform rightKnee;
        [SerializeField] private Transform leftFoot;
        [SerializeField] private Transform rightFoot;

        private Vector3 previousPosition;
        private double previousTick;
        private bool hasPreviousSample;
        private float stridePhase;

        public bool IsConfigured => hips != null && upperBody != null
            && leftThigh != null && rightThigh != null && leftKnee != null
            && rightKnee != null && leftFoot != null && rightFoot != null;
        public bool IsCrouchPose => IsConfigured && hips.localPosition.y < StandingHipHeight - 0.1f;

        /// <summary>生成器装配本地骨架引用；所有节点位于该表现子树内。</summary>
        public void Configure(
            Transform hipsTransform,
            Transform upperBodyTransform,
            Transform leftThighTransform,
            Transform rightThighTransform,
            Transform leftKneeTransform,
            Transform rightKneeTransform,
            Transform leftFootTransform,
            Transform rightFootTransform)
        {
            hips = hipsTransform;
            upperBody = upperBodyTransform;
            leftThigh = leftThighTransform;
            rightThigh = rightThighTransform;
            leftKnee = leftKneeTransform;
            rightKnee = rightKneeTransform;
            leftFoot = leftFootTransform;
            rightFoot = rightFootTransform;
        }

        public void Render(
            in RemotePlayerRenderState state,
            bool isAlive,
            bool gameplayActive,
            float crouchedHeightRatio)
        {
            if (!IsConfigured)
            {
                return;
            }

            bool stale = state.Status == RemotePlayerSamplingStatus.ExtrapolationLimitReached;
            bool reset = !hasPreviousSample || state.Status == RemotePlayerSamplingStatus.Teleported
                || state.ServerTickTime < previousTick || stale || !isAlive || !gameplayActive;
            Vector3 displacement = state.Position - previousPosition;
            displacement.y = 0f;
            bool walking = isAlive && gameplayActive && !stale && state.IsGrounded
                && new Vector2(state.Velocity.x, state.Velocity.z).sqrMagnitude > 0.0225f;
            if (reset || !walking)
            {
                stridePhase = 0f;
            }
            else if (state.ServerTickTime > previousTick)
            {
                // 由实际渲染位移推进步态，不依赖帧率；重复采样同一Tick不得继续走步。
                // 限制单次步幅，避免传送边界或异常大位移造成姿态瞬间翻转。
                stridePhase = Mathf.Repeat(
                    stridePhase + Mathf.Min(displacement.magnitude, 0.5f) * Mathf.PI * 2f / StrideLength,
                    Mathf.PI * 2f);
            }
            previousPosition = state.Position;
            previousTick = state.ServerTickTime;
            hasPreviousSample = true;

            float heightRatio = state.IsCrouched ? Mathf.Clamp(crouchedHeightRatio, 0.6f, 1f) : 1f;
            float hipHeight = StandingHipHeight - StandingHeight * (1f - heightRatio);
            float bend = Mathf.Acos(Mathf.Clamp01((hipHeight - FootHeight) / (2f * LegLength)))
                * Mathf.Rad2Deg;
            float swing = walking ? Mathf.Sin(stridePhase) * 25f * heightRatio : 0f;
            if (!state.IsGrounded && isAlive && gameplayActive && !stale)
            {
                bend = Mathf.Max(bend, 25f);
            }

            // 下蹲通过降低髋部和屈膝完成，不缩扁头部、武器或整个角色模型。
            hips.localPosition = Vector3.up * hipHeight;
            PoseLeg(leftThigh, leftKnee, leftFoot, bend, swing);
            PoseLeg(rightThigh, rightKnee, rightFoot, bend, -swing);
            float pitch = Mathf.DeltaAngle(0f, state.Rotation.eulerAngles.x);
            upperBody.localRotation = Quaternion.Euler(Mathf.Clamp(pitch, -60f, 60f), 0f, 0f);

            // 死亡只倾倒表现子树；脚底根节点继续严格使用权威位置与Yaw，重生自动复位。
            transform.localPosition = isAlive ? Vector3.zero : Vector3.up * 0.4f;
            transform.localRotation = isAlive ? Quaternion.identity : Quaternion.Euler(0f, 0f, 90f);
        }

        private static void PoseLeg(Transform thigh, Transform knee, Transform foot, float bend, float swing)
        {
            thigh.localRotation = Quaternion.Euler(-bend + swing, 0f, 0f);
            knee.localRotation = Quaternion.Euler(2f * bend, 0f, 0f);
            foot.localRotation = Quaternion.Euler(-bend - swing, 0f, 0f);
        }

        private void OnDisable()
        {
            hasPreviousSample = false;
            stridePhase = 0f;
        }
    }
}

using AFPS.NetCode.SnapshotInterpolation;
using UnityEngine;

namespace AFPS.Presentation.Characters
{
    /// <summary>
    /// 只显示远端玩家快照采样结果，不执行本地输入预测、回滚或校正平滑。
    /// </summary>
    public sealed class RemotePlayerView : MonoBehaviour
    {
        [SerializeField] private Transform presentationTransform;
        [SerializeField] private Renderer[] renderers;
        [SerializeField] private Color aliveColor = new Color(0.15f, 0.65f, 1f);
        [SerializeField] private Color deadColor = new Color(0.35f, 0.08f, 0.08f);
        private Vector3 initialScale;
        private bool? lastAlive;
        private FootstepAudioView footsteps;
        private MaterialPropertyBlock colorProperties;
        private GrappleRopeView grappleRope;
        private RemoteCharacterPoseView characterPose;
        private bool initialized;
        public bool HasCharacterVisual => characterPose != null && characterPose.IsConfigured;
        public bool IsCrouchPresented => HasCharacterVisual
            ? characterPose.IsCrouchPose
            : presentationTransform != null && presentationTransform.localScale.y < initialScale.y * 0.8f;
        public bool HasGrappleRope => grappleRope != null && grappleRope.IsVisible;
        public ulong FootstepCount => footsteps != null ? footsteps.StepCount : 0;

        private void Awake()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (initialized)
            {
                return;
            }
            if (presentationTransform == null)
            {
                presentationTransform = transform;
            }

            initialScale = presentationTransform.localScale;
            characterPose = GetComponentInChildren<RemoteCharacterPoseView>(true);
            if (renderers == null || renderers.Length == 0)
            {
                renderers = GetComponentsInChildren<Renderer>(true);
            }
            initialized = true;
        }

        public void ApplyRenderState(in RemotePlayerRenderState state, bool isAlive = true,
            bool gameplayActive = true, float crouchedHeightRatio = 0.6f, float grappleOriginHeight = 1.6f)
        {
            // 编辑器姿态预览不会自动执行Awake，仍需绑定同一套表现引用；重复采样不重复初始化。
            Initialize();
            if (footsteps == null)
            {
                footsteps = gameObject.AddComponent<FootstepAudioView>();
            }
            float speed = new Vector2(state.Velocity.x, state.Velocity.z).magnitude;
            float bob = isAlive && gameplayActive && state.IsGrounded && speed > 0.15f
                ? Mathf.Sin((float)state.ServerTickTime * 0.7f) * Mathf.Min(0.045f, speed * 0.008f) : 0f;
            bool articulated = HasCharacterVisual;
            // 人形脚底根节点只跟随Yaw；俯仰交给上半身，避免向上瞄准时双脚翘离地面。
            Quaternion rotation = articulated
                ? Quaternion.Euler(0f, state.Rotation.eulerAngles.y, 0f)
                : isAlive ? state.Rotation : state.Rotation * Quaternion.Euler(0f, 0f, 90f);
            Vector3 position = articulated
                ? state.Position
                : state.Position + Vector3.up * (isAlive ? bob : -0.5f);
            presentationTransform.SetPositionAndRotation(position, rotation);
            float heightRatio = state.IsCrouched ? Mathf.Clamp(crouchedHeightRatio, 0.01f, 1f) : 1f;
            presentationTransform.localScale = articulated
                ? initialScale
                : new Vector3(initialScale.x,
                    initialScale.y * heightRatio * (isAlive ? 1f : 0.65f), initialScale.z);
            if (articulated)
            {
                characterPose.Render(state, isAlive, gameplayActive, heightRatio);
            }
            footsteps.Render(state.Position, state.IsGrounded, isAlive && gameplayActive,
                state.Status == RemotePlayerSamplingStatus.Teleported);
            if (state.IsGrappling && isAlive && gameplayActive
                && state.Status != RemotePlayerSamplingStatus.ExtrapolationLimitReached)
            {
                if (grappleRope == null)
                {
                    grappleRope = gameObject.AddComponent<GrappleRopeView>();
                }
                var offset = Quaternion.Euler(0f, state.Rotation.eulerAngles.y, 0f) * new Vector3(0.15f, -0.12f, 0f);
                grappleRope.RenderEndpoints(position + Vector3.up * grappleOriginHeight + offset, state.GrapplePoint);
            }
            else
            {
                HideGrappleRope();
            }

            if (lastAlive == isAlive)
            {
                return;
            }

            lastAlive = isAlive;
            Color color = isAlive ? aliveColor : deadColor;
            if (colorProperties == null)
            {
                colorProperties = new MaterialPropertyBlock();
            }
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    // 只覆写本角色颜色，不复制材质或修改所有角色共享的材质资产。
                    renderers[i].GetPropertyBlock(colorProperties);
                    colorProperties.SetColor("_Color", color);
                    colorProperties.SetColor("_BaseColor", color);
                    renderers[i].SetPropertyBlock(colorProperties);
                }
            }
        }

        public void HideGrappleRope()
        {
            if (grappleRope != null)
            {
                grappleRope.Hide();
            }
        }
    }
}

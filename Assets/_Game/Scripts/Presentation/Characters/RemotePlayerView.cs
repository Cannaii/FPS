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
        public ulong FootstepCount => footsteps != null ? footsteps.StepCount : 0;

        private void Awake()
        {
            if (presentationTransform == null)
            {
                presentationTransform = transform;
            }

            initialScale = presentationTransform.localScale;
            if (renderers == null || renderers.Length == 0)
            {
                renderers = GetComponentsInChildren<Renderer>(true);
            }
        }

        public void ApplyRenderState(in RemotePlayerRenderState state, bool isAlive = true, bool gameplayActive = true)
        {
            if (footsteps == null)
            {
                footsteps = gameObject.AddComponent<FootstepAudioView>();
            }
            float speed = new Vector2(state.Velocity.x, state.Velocity.z).magnitude;
            float bob = isAlive && gameplayActive && state.IsGrounded && speed > 0.15f
                ? Mathf.Sin((float)state.ServerTickTime * 0.7f) * Mathf.Min(0.045f, speed * 0.008f) : 0f;
            Quaternion rotation = isAlive ? state.Rotation : state.Rotation * Quaternion.Euler(0f, 0f, 90f);
            Vector3 position = state.Position + Vector3.up * (isAlive ? bob : -0.5f);
            presentationTransform.SetPositionAndRotation(position, rotation);
            presentationTransform.localScale = isAlive ? initialScale : new Vector3(initialScale.x, initialScale.y * 0.65f, initialScale.z);
            footsteps.Render(state.Position, state.IsGrounded, isAlive && gameplayActive,
                state.Status == RemotePlayerSamplingStatus.Teleported);

            if (lastAlive == isAlive)
            {
                return;
            }

            lastAlive = isAlive;
            Color color = isAlive ? aliveColor : deadColor;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].material.color = color;
                }
            }
        }
    }
}

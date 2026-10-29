using AFPS.Presentation.Weapons;
using AFPS.Simulation.Characters;
using UnityEngine;

namespace AFPS.Presentation.Characters
{
    /// <summary>本地钩索只读表现；端点跟随平滑显示位置，不反写锚点、绳长或模拟状态。</summary>
    public sealed class GrappleRopeView : MonoBehaviour
    {
        private LineRenderer line;
        private Material material;
        public bool IsVisible => line != null && line.enabled;

        public void Render(in PlayerMovementState state, Vector3 visualFeet, in PlayerMovementConfig config, bool gameplayActive = true)
        {
            if (!gameplayActive || !state.IsGrappling || !PlayerGrappleSimulation.TryOrigin(state, config, out var origin))
            {
                Hide();
                return;
            }
            float height = (origin.Y - state.PositionFixed.Y).ToFloat();
            var handOffset = Quaternion.Euler(0f, state.YawFixedDegrees.ToFloat(), 0f) * new Vector3(0.15f, -0.12f, 0f);
            var point = state.GrapplePointFixed;
            RenderEndpoints(visualFeet + Vector3.up * height + handOffset,
                new Vector3(point.X.ToFloat(), point.Y.ToFloat(), point.Z.ToFloat()));
        }

        /// <summary>远端和本地共享的只读渲染出口；端点是表现世界坐标，单位米。</summary>
        public void RenderEndpoints(Vector3 origin, Vector3 anchor)
        {
            if (line == null)
            {
                var target = new GameObject("Grapple Rope");
                target.transform.SetParent(transform, false);
                line = target.AddComponent<LineRenderer>();
                var template = CombatPresentationAssets.Load<Material>("Effects");
                material = template != null ? new Material(template) : new Material(Shader.Find("Sprites/Default"));
                line.sharedMaterial = material;
                line.useWorldSpace = true;
                line.positionCount = 2;
                line.startWidth = 0.018f; // 米，只影响渲染粗细，不是碰撞半径。
                line.endWidth = 0.012f;
                line.startColor = new Color(0.3f, 0.85f, 1f);
                line.endColor = Color.white;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
            }
            line.enabled = true;
            line.SetPosition(0, origin);
            line.SetPosition(1, anchor);
        }

        public void Hide()
        {
            if (line != null)
            {
                line.enabled = false;
            }
        }

        private void OnDisable()
        {
            Hide();
        }

        private void OnDestroy()
        {
            if (material != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(material);
                }
                else
                {
                    DestroyImmediate(material);
                }
            }
        }
    }
}

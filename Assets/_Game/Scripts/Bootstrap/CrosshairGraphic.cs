using UnityEngine;
using UnityEngine.UI;

namespace AFPS.Bootstrap
{
    /// <summary>原生UGUI几何准星，中心固定于RectTransform；不读取或修改模拟。</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CrosshairGraphic : MaskableGraphic
    {
        [SerializeField] private bool useSavedSettings = true;
        [SerializeField] private CrosshairAppearance appearance = CrosshairAppearance.Default;
        public bool UseSavedSettings
        {
            get => useSavedSettings;
            set => useSavedSettings = value;
        }
        public CrosshairAppearance Appearance
        {
            get => appearance;
            set
            {
                appearance = value.Validated();
                SetVerticesDirty();
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            raycastTarget = false;
            if (Application.isPlaying && useSavedSettings)
            {
                Appearance = CrosshairPreferences.Current;
                CrosshairPreferences.Changed += UpdateAppearance;
            }
        }

        protected override void OnDisable()
        {
            CrosshairPreferences.Changed -= UpdateAppearance;
            base.OnDisable();
        }

        private void UpdateAppearance(CrosshairAppearance value)
        {
            if (useSavedSettings)
            {
                Appearance = value;
            }
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var value = appearance.Validated();
            if (value.Opacity <= 0f)
            {
                return;
            }
            Vector2 center = rectTransform.rect.center;
            Color tint = value.Color;
            tint.a = value.Opacity;
            if (value.Outline > 0f)
            {
                Draw(mesh, center, value, new Color(0f, 0f, 0f, value.Opacity), value.Outline);
            }
            Draw(mesh, center, value, tint, 0f);
        }

        private static void Draw(VertexHelper mesh, Vector2 center, CrosshairAppearance value, Color tint, float border)
        {
            float radius = value.Size * 0.5f;
            float half = value.Thickness * 0.5f;
            if (value.Style == CrosshairStyle.Cross || value.Style == CrosshairStyle.CrossAndDot)
            {
                Quad(mesh, center, new Rect(value.Gap - border, -half - border,
                    radius - value.Gap + border * 2f, value.Thickness + border * 2f), tint);
                Quad(mesh, center, new Rect(-radius - border, -half - border,
                    radius - value.Gap + border * 2f, value.Thickness + border * 2f), tint);
                Quad(mesh, center, new Rect(-half - border, value.Gap - border,
                    value.Thickness + border * 2f, radius - value.Gap + border * 2f), tint);
                Quad(mesh, center, new Rect(-half - border, -radius - border,
                    value.Thickness + border * 2f, radius - value.Gap + border * 2f), tint);
            }
            if (value.Style == CrosshairStyle.Dot || value.Style == CrosshairStyle.CrossAndDot)
            {
                float dot = value.Style == CrosshairStyle.Dot ? radius : Mathf.Max(1f, half);
                Ring(mesh, center, dot + border, 0f, tint);
            }
            if (value.Style == CrosshairStyle.Ring)
            {
                Ring(mesh, center, radius + border, Mathf.Max(0f, radius - value.Thickness - border), tint);
            }
        }

        private static void Quad(VertexHelper mesh, Vector2 center, Rect rect, Color tint)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(center + new Vector2(rect.xMin, rect.yMin), tint, Vector2.zero);
            mesh.AddVert(center + new Vector2(rect.xMin, rect.yMax), tint, Vector2.zero);
            mesh.AddVert(center + new Vector2(rect.xMax, rect.yMax), tint, Vector2.zero);
            mesh.AddVert(center + new Vector2(rect.xMax, rect.yMin), tint, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start + 2, start + 3, start);
        }

        private static void Ring(VertexHelper mesh, Vector2 center, float outer, float inner, Color tint)
        {
            const int segments = 48;
            for (int index = 0; index < segments; index++)
            {
                float a = index * Mathf.PI * 2f / segments;
                float b = (index + 1) * Mathf.PI * 2f / segments;
                Vector2 from = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Vector2 to = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
                int start = mesh.currentVertCount;
                mesh.AddVert(center + from * outer, tint, Vector2.zero);
                mesh.AddVert(center + to * outer, tint, Vector2.zero);
                mesh.AddVert(center + to * inner, tint, Vector2.zero);
                mesh.AddVert(center + from * inner, tint, Vector2.zero);
                mesh.AddTriangle(start, start + 1, start + 2);
                mesh.AddTriangle(start + 2, start + 3, start);
            }
        }
    }
}

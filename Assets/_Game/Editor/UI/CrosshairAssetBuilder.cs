using AFPS.Bootstrap;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace AFPS.Editor.UI
{
    /// <summary>生成可编辑准星和独立设置窗口；已有资源不覆盖，不全局保存用户资产。</summary>
    public static class CrosshairAssetBuilder
    {
        public const string CrosshairPath = "Assets/_Game/Art/Resources/UI/Crosshair.prefab";
        public const string WindowPath = "Assets/_Game/Art/Resources/UI/CrosshairSettings.prefab";
        private static TMP_FontAsset font;

        [MenuItem("AFPS/UI/Create Crosshair Prefabs If Missing")]
        public static void Create()
        {
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NetworkLobbyAssetBuilder.FontPath);
            if (font == null)
            {
                throw new System.InvalidOperationException("缺少项目中文SDF字体。");
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(CrosshairPath) == null)
            {
                var root = Rect("Crosshair", null, Vector2.zero, new Vector2(200f, 200f));
                try
                {
                    root.gameObject.AddComponent<CrosshairGraphic>().Appearance = CrosshairAppearance.Default;
                    PrefabUtility.SaveAsPrefabAsset(root.gameObject, CrosshairPath);
                }
                finally
                {
                    Object.DestroyImmediate(root.gameObject);
                }
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(WindowPath) == null)
            {
                CreateWindow();
            }
        }

        private static void CreateWindow()
        {
            var root = new GameObject("Crosshair Settings", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            try
            {
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 1100;
                var scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
                var backdrop = Rect("Backdrop", root.transform, Vector2.zero, Vector2.zero);
                backdrop.anchorMin = Vector2.zero;
                backdrop.anchorMax = Vector2.one;
                backdrop.gameObject.AddComponent<Image>().color = new Color(0.02f, 0.03f, 0.05f, 0.85f);
                var panel = Rect("Panel", backdrop, Vector2.zero, new Vector2(1080f, 860f));
                panel.gameObject.AddComponent<Image>().color = new Color(0.075f, 0.095f, 0.135f, 1f);
                Label(panel, "准星设置", new Vector2(0f, 380f), new Vector2(900f, 52f), 34f);
                Label(panel, "F2 打开或应用并关闭 · 调整时实时预览", new Vector2(0f, 330f),
                    new Vector2(900f, 36f), 20f);
                string[] names = { "大小", "线条粗细", "中心间隙", "不透明度", "红色", "绿色", "蓝色", "描边宽度" };
                float[] minimum = { 8f, 1f, 0f, 0f, 0f, 0f, 0f, 0f };
                float[] maximum = { 96f, 8f, 24f, 1f, 1f, 1f, 1f, 4f };
                var sliders = new Slider[8];
                var values = new TMP_Text[8];
                for (int index = 0; index < names.Length; index++)
                {
                    float y = 260f - index * 60f;
                    var label = Label(panel, names[index], new Vector2(-430f, y), new Vector2(160f, 38f), 21f);
                    label.alignment = TextAlignmentOptions.MidlineLeft;
                    sliders[index] = Slider(panel, names[index], new Vector2(-145f, y), minimum[index], maximum[index]);
                    values[index] = Label(panel, "", new Vector2(110f, y), new Vector2(120f, 38f), 20f);
                }
                Label(panel, "实时预览", new Vector2(350f, 260f), new Vector2(260f, 38f), 23f);
                var previewBackdrop = Rect("Preview Backdrop", panel, new Vector2(350f, 100f), new Vector2(260f, 260f));
                previewBackdrop.gameObject.AddComponent<Image>().color = new Color(0.02f, 0.035f, 0.05f);
                var preview = Rect("Preview Crosshair", previewBackdrop, Vector2.zero, new Vector2(200f, 200f))
                    .gameObject.AddComponent<CrosshairGraphic>();
                preview.UseSavedSettings = false;
                preview.Appearance = CrosshairAppearance.Default;
                var style = Button(panel, "样式：十字（点击切换）", new Vector2(0f, -250f), new Vector2(800f, 48f));
                Label(panel, "仅改变准星外观，不改变瞄准和命中判定。", new Vector2(0f, -310f),
                    new Vector2(900f, 38f), 20f);
                var apply = Button(panel, "应用并关闭", new Vector2(-280f, -375f), new Vector2(250f, 52f));
                var cancel = Button(panel, "取消修改", new Vector2(0f, -375f), new Vector2(250f, 52f));
                var reset = Button(panel, "恢复默认", new Vector2(280f, -375f), new Vector2(250f, 52f));
                var view = root.AddComponent<CrosshairSettingsView>();
                var serialized = new SerializedObject(view);
                serialized.FindProperty("preview").objectReferenceValue = preview;
                serialized.FindProperty("style").objectReferenceValue = style;
                serialized.FindProperty("styleLabel").objectReferenceValue = style.GetComponentInChildren<TMP_Text>();
                serialized.FindProperty("apply").objectReferenceValue = apply;
                serialized.FindProperty("cancel").objectReferenceValue = cancel;
                serialized.FindProperty("reset").objectReferenceValue = reset;
                var sliderFields = serialized.FindProperty("sliders");
                var valueFields = serialized.FindProperty("values");
                sliderFields.arraySize = sliders.Length;
                valueFields.arraySize = values.Length;
                for (int index = 0; index < sliders.Length; index++)
                {
                    sliderFields.GetArrayElementAtIndex(index).objectReferenceValue = sliders[index];
                    valueFields.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, WindowPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var root = new GameObject(name, typeof(RectTransform));
            var rect = root.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.one * 0.5f;
            rect.anchorMax = rect.anchorMin;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static TextMeshProUGUI Label(Transform parent, string text, Vector2 position, Vector2 size, float points)
        {
            var label = Rect(text, parent, position, size).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = text;
            label.fontSize = points;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            return label;
        }

        private static Button Button(Transform parent, string text, Vector2 position, Vector2 size)
        {
            var rect = Rect(text, parent, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.12f, 0.60f, 0.82f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var label = Label(rect, text, Vector2.zero, size, 22f);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.sizeDelta = Vector2.zero;
            return button;
        }

        private static Slider Slider(Transform parent, string name, Vector2 position, float minimum, float maximum)
        {
            var rect = Rect(name + " Slider", parent, position, new Vector2(360f, 24f));
            var track = rect.gameObject.AddComponent<Image>();
            track.color = new Color(0.16f, 0.21f, 0.27f);
            var fill = Rect("Fill", rect, Vector2.zero, Vector2.zero);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.70f, 0.94f);
            var handle = Rect("Handle", rect, Vector2.zero, new Vector2(18f, 36f));
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = Color.white;
            var slider = rect.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.minValue = minimum;
            slider.maxValue = maximum;
            return slider;
        }
    }
}

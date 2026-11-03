using System;
using AFPS.Bootstrap;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace AFPS.Editor.UI
{
    /// <summary>仅首次生成重连覆盖层，保留后续人工编辑，不保存其他资源。</summary>
    public sealed class NetworkRecoveryAssetBuilder
    {
        public const string PrefabPath = "Assets/_Game/Art/Resources/UI/NetworkRecovery.prefab";
        private static TMP_FontAsset font;
        private GameObject canvasRoot;
        private Canvas canvas;
        private TextMeshProUGUI message;
        private Button retry;
        private Button cancel;
        private Button joinAsNew;

        [MenuItem("AFPS/UI/Create Recovery Prefab If Missing")]
        public static void Create()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            {
                Debug.Log("重连Prefab已存在，保留编辑内容：" + PrefabPath);
                return;
            }

            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NetworkLobbyAssetBuilder.FontPath);
            if (font == null)
            {
                throw new InvalidOperationException("请先准备项目中文SDF字体资源。");
            }

            var builder = new NetworkRecoveryAssetBuilder();
            try
            {
                builder.Build();
                var view = builder.canvasRoot.AddComponent<NetworkRecoveryView>();
                var serialized = new SerializedObject(view);
                serialized.FindProperty("canvas").objectReferenceValue = builder.canvas;
                serialized.FindProperty("message").objectReferenceValue = builder.message;
                serialized.FindProperty("retry").objectReferenceValue = builder.retry;
                serialized.FindProperty("cancel").objectReferenceValue = builder.cancel;
                serialized.FindProperty("joinAsNew").objectReferenceValue = builder.joinAsNew;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                if (!view.TryValidate(out string error))
                {
                    throw new InvalidOperationException(error);
                }

                PrefabUtility.SaveAsPrefabAsset(builder.canvasRoot, PrefabPath);
                Debug.Log("已生成可编辑重连界面：" + PrefabPath);
            }
            finally
            {
                if (builder.canvasRoot != null)
                {
                    UnityEngine.Object.DestroyImmediate(builder.canvasRoot);
                }
            }
        }

        private void Build()
        {
            // 尺寸为1920×1080参考像素，宽高缩放匹配权重各占一半。
            var root = new GameObject(
                "Reconnect UI",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasRoot = root;
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1200;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var backdrop = CreateRect(root.transform, "Backdrop", Vector2.zero, Vector2.zero);
            backdrop.anchorMin = Vector2.zero;
            backdrop.anchorMax = Vector2.one;
            backdrop.gameObject.AddComponent<Image>().color = new Color(0.02f, 0.03f, 0.05f, 0.80f);
            var panel = CreateRect(backdrop, "Panel", new Vector2(760, 330), Vector2.zero);
            panel.gameObject.AddComponent<Image>().color = new Color(0.075f, 0.095f, 0.135f, 0.99f);
            CreateLabel(panel, "连接恢复", new Vector2(0, 112), new Vector2(680, 50), 32);
            message = CreateLabel(panel, "连接中断，正在等待重连…原角色仍可能受到伤害。", new Vector2(0, 42), new Vector2(680, 82), 22);
            retry = CreateButton(panel, "重试原会话", new Vector2(-240, -62));
            cancel = CreateButton(panel, "取消重连", new Vector2(0, -62));
            joinAsNew = CreateButton(panel, "作为新玩家加入", new Vector2(240, -62));
            CreateLabel(panel, "重新加入会创建新角色；原角色最多保留约30秒。", new Vector2(0, -123), new Vector2(690, 40), 18);
            retry.name = "Retry Session";
            cancel.name = "Cancel Reconnect";
            joinAsNew.name = "Join As New Player";
        }

        private static RectTransform CreateRect(Transform parent, string name, Vector2 size, Vector2 position)
        {
            var root = new GameObject(name, typeof(RectTransform));
            var rect = root.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = rect.anchorMin;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private static TextMeshProUGUI CreateLabel(
            Transform parent,
            string text,
            Vector2 position,
            Vector2 size,
            int fontSize)
        {
            var label = CreateRect(parent, "Text", size, position).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            return label;
        }

        private static Button CreateButton(Transform parent, string text, Vector2 position)
        {
            var rect = CreateRect(parent, "Button", new Vector2(220, 54), position);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.12f, 0.60f, 0.82f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            CreateLabel(rect, text, Vector2.zero, new Vector2(210, 48), 20);
            return button;
        }

    }
}

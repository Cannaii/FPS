using System;
using AFPS.Bootstrap;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace AFPS.Editor.UI
{
    /// <summary>仅首次生成F3诊断界面，已有资源不覆盖，不保存其他资源。</summary>
    public sealed class NetworkDebugAssetBuilder
    {
        public const string PrefabPath = "Assets/_Game/Art/Resources/UI/NetworkDebug.prefab";
        private static TMP_FontAsset font;
        private GameObject canvasRoot;
        private Canvas canvas;
        private TMP_Text clientText;
        private TMP_Text serverText;
        private TMP_Text detailText;

        [MenuItem("AFPS/UI/Create Network Debug Prefab If Missing")]
        public static void Create()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            {
                Debug.Log("F3诊断Prefab已存在，保留编辑内容：" + PrefabPath);
                return;
            }

            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NetworkLobbyAssetBuilder.FontPath);
            if (font == null)
            {
                throw new InvalidOperationException("请先准备项目中文SDF字体资源。");
            }

            var builder = new NetworkDebugAssetBuilder();
            try
            {
                builder.Build();
                // 编辑时提供排版示例，运行时首次显示前用真实诊断数据覆盖。
                builder.clientText.name = "Client Metrics";
                builder.serverText.name = "Server Metrics";
                builder.detailText.name = "Connection Details";
                builder.clientText.text = "客户端与网络\n模式：运行时读取\n本地实体：0    远端实体：0"
                    + "\n网络时钟：待同步\n往返延迟 / 抖动：—\n插值 当前 / 目标：0.0 / 0.0 Tick"
                    + "\n预测校正：0 次\n时钟 请求 / 响应 / 确认：0 / 0 / 0"
                    + "\n状态上报 / 合并替换：0 / 0\n客户端恢复：未连接\n算法 / 协议：运行时读取";
                builder.serverText.text = "服务器与远端快照\n当前进程未运行服务器"
                    + "\n服务器模拟耗时、发送流量与输入窗口\n请在主机或独立服务器查看。";
                builder.detailText.text = "连接诊断（实体排序，显示前4 / 共0）\n最近状态漂移：无记录";
                var view = builder.canvasRoot.AddComponent<NetworkDebugView>();
                var serialized = new SerializedObject(view);
                serialized.FindProperty("canvas").objectReferenceValue = builder.canvas;
                serialized.FindProperty("clientText").objectReferenceValue = builder.clientText;
                serialized.FindProperty("serverText").objectReferenceValue = builder.serverText;
                serialized.FindProperty("detailText").objectReferenceValue = builder.detailText;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                if (!view.TryValidate(out string error))
                {
                    throw new InvalidOperationException(error);
                }

                PrefabUtility.SaveAsPrefabAsset(builder.canvasRoot, PrefabPath);
                Debug.Log("已生成可编辑F3诊断界面：" + PrefabPath);
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
            // 尺寸为1920×1080参考像素，面板按屏幕比例锚定，宽高缩放匹配权重各半。
            var root = new GameObject("Network Debug UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasRoot = root;
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var panel = new GameObject("Diagnostics Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(root.transform, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.06f, 0.17f);
            rect.anchorMax = new Vector2(0.94f, 0.86f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0.025f, 0.045f, 0.065f, 0.98f);
            panel.GetComponent<Image>().raycastTarget = false;
            Label(rect, "网络诊断    F3 收起    ·    只读，不暂停对局", 27f,
                new Vector2(0f, 1f), Vector2.one, new Vector2(24f, -64f), new Vector2(-24f, -12f));
            clientText = Label(rect, "", 20f, new Vector2(0f, 0.36f), new Vector2(0.5f, 1f),
                new Vector2(24f, 0f), new Vector2(-12f, -72f));
            serverText = Label(rect, "", 20f, new Vector2(0.5f, 0.36f), Vector2.one,
                new Vector2(12f, 0f), new Vector2(-24f, -72f));
            detailText = Label(rect, "", 20f, Vector2.zero, new Vector2(1f, 0.36f),
                new Vector2(24f, 20f), new Vector2(-24f, -4f));
        }

        private static TMP_Text Label(Transform parent, string value, float size, Vector2 minimum,
            Vector2 maximum, Vector2 offsetMinimum, Vector2 offsetMaximum)
        {
            var text = new GameObject("Diagnostic Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            text.transform.SetParent(parent, false);
            text.rectTransform.anchorMin = minimum;
            text.rectTransform.anchorMax = maximum;
            text.rectTransform.offsetMin = offsetMinimum;
            text.rectTransform.offsetMax = offsetMaximum;
            text.font = font;
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.color = new Color(0.87f, 0.94f, 1f);
            text.raycastTarget = false;
            text.richText = false;
            text.enableWordWrapping = true;
            text.text = value;
            return text;
        }


    }
}

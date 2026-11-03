using System;
using AFPS.Bootstrap;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace AFPS.Editor.UI
{
    /// <summary>仅在资源缺失时生成HUD，已有Prefab和材质不覆盖，保留人工编辑。</summary>
    public sealed class GameplayHudAssetBuilder
    {
        public const string PrefabPath = "Assets/_Game/Art/Resources/UI/GameplayHud.prefab";
        public const string MaterialPath = "Assets/_Game/Art/Resources/UI/GameplayHudText.mat";
        private static TMP_FontAsset font;
        private static Material textMaterial;
        private GameObject canvasRoot;
        private Canvas canvas;
        private Image damageFlash;
        private TextMeshProUGUI statusText;
        private TextMeshProUGUI scoreText;
        private TextMeshProUGUI networkText;
        private TextMeshProUGUI healthText;
        private TextMeshProUGUI weaponText;
        private TextMeshProUGUI centerText;
        private TextMeshProUGUI hitMarkerText;
        private TextMeshProUGUI rejectionText;
        private TextMeshProUGUI damageDirectionText;

        [MenuItem("AFPS/UI/Create Gameplay HUD Prefab If Missing")]
        public static void Create()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            {
                Debug.Log("战斗HUD Prefab已存在，保留编辑内容：" + PrefabPath);
                return;
            }

            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NetworkLobbyAssetBuilder.FontPath);
            if (font == null)
            {
                throw new InvalidOperationException("请先准备项目中文SDF字体资源。");
            }

            textMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (textMaterial == null)
            {
                // 描边材质独立持久化，不能把临时材质引用写入Prefab，也不修改共享字体材质。
                textMaterial = new Material(font.material);
                textMaterial.name = "GameplayHudText";
                textMaterial.SetFloat("_OutlineWidth", 0.18f);
                textMaterial.SetColor("_OutlineColor", new Color32(0, 0, 0, 220));
                textMaterial.EnableKeyword("OUTLINE_ON");
                AssetDatabase.CreateAsset(textMaterial, MaterialPath);
            }

            var builder = new GameplayHudAssetBuilder();
            try
            {
                builder.Build();
                // 编辑模式提供示例内容便于排版；运行时在HUD首次可见前用真实状态覆盖。
                builder.statusText.text = "自由死斗  05:00";
                builder.networkText.text = "网络延迟  0 毫秒";
                builder.healthText.text = "生命  100   击杀 0 / 死亡 0\n体力 100%   突进 2/2\n就绪";
                builder.weaponText.text = "突击步枪\n<size=38>30 / 30</size>\n左键开火   R 换弹   Q 切枪";
                builder.scoreText.text = "玩家    击杀 / 死亡\n> 玩家 1   0 / 0";
                var view = builder.canvasRoot.AddComponent<GameplayHudView>();
                var serialized = new SerializedObject(view);
                serialized.FindProperty("canvas").objectReferenceValue = builder.canvas;
                serialized.FindProperty("scorePanel").objectReferenceValue = builder.scoreText.transform.parent.gameObject;
                serialized.FindProperty("damageFlash").objectReferenceValue = builder.damageFlash;
                serialized.FindProperty("statusText").objectReferenceValue = builder.statusText;
                serialized.FindProperty("scoreText").objectReferenceValue = builder.scoreText;
                serialized.FindProperty("networkText").objectReferenceValue = builder.networkText;
                serialized.FindProperty("healthText").objectReferenceValue = builder.healthText;
                serialized.FindProperty("weaponText").objectReferenceValue = builder.weaponText;
                serialized.FindProperty("centerText").objectReferenceValue = builder.centerText;
                serialized.FindProperty("hitMarkerText").objectReferenceValue = builder.hitMarkerText;
                serialized.FindProperty("rejectionText").objectReferenceValue = builder.rejectionText;
                serialized.FindProperty("damageDirectionText").objectReferenceValue = builder.damageDirectionText;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                if (!view.TryValidate(out string error))
                {
                    throw new InvalidOperationException(error);
                }

                PrefabUtility.SaveAsPrefabAsset(builder.canvasRoot, PrefabPath);
                // 不调用全局SaveAssets，避免把用户正在编辑的字体或其他资源一并写入。
                Debug.Log("已生成可编辑战斗HUD：" + PrefabPath);
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
            GameObject root = new GameObject(
                "Gameplay HUD",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));
            canvasRoot = root;
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var flashRoot = new GameObject("Damage Flash", typeof(RectTransform), typeof(Image));
            flashRoot.transform.SetParent(root.transform, false);
            var flashRect = flashRoot.GetComponent<RectTransform>();
            flashRect.anchorMin = Vector2.zero;
            flashRect.anchorMax = Vector2.one;
            flashRect.sizeDelta = Vector2.zero;
            damageFlash = flashRoot.GetComponent<Image>();
            damageFlash.color = new Color(1f, 0f, 0f, 0.12f);
            damageFlash.raycastTarget = false;
            damageFlash.enabled = false;

            // 以1920×1080为参考，面板各占独立区域；底部留出操作提示，不挤入顶部状态长串。
            scaler.matchWidthOrHeight = 0.5f;
            statusText = Panel(
                root.transform,
                "Match",
                new Vector2(0.5f, 1f),
                new Vector2(0f, -24f),
                new Vector2(360f, 90f),
                26f);
            statusText.alignment = TextAlignmentOptions.Center;
            networkText = Panel(
                root.transform,
                "Network",
                new Vector2(0f, 1f),
                new Vector2(28f, -24f),
                new Vector2(330f, 54f),
                20f);
            scoreText = Panel(
                root.transform,
                "Scores",
                Vector2.one,
                new Vector2(-28f, -24f),
                new Vector2(330f, 320f),
                22f);
            healthText = Panel(
                root.transform,
                "Health",
                Vector2.zero,
                new Vector2(28f, 92f),
                new Vector2(430f, 145f),
                24f);
            weaponText = Panel(
                root.transform,
                "Weapon",
                Vector2.right,
                new Vector2(-28f, 92f),
                new Vector2(360f, 165f),
                23f);
            centerText = CreateText(root.transform, "", 38f, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 180f), new Vector2(850f, 120f));
            hitMarkerText = CreateText(root.transform, "", 26f, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(50f, 50f));
            damageDirectionText = CreateText(root.transform, "", 36f, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(50f, 50f));
            centerText.name = "Center Message";
            hitMarkerText.name = "Hit Marker";
            damageDirectionText.name = "Damage Direction";
            damageDirectionText.color = new Color(1f, 0.25f, 0.18f);
            rejectionText = CreateText(root.transform, "", 23f, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 130f), new Vector2(700f, 50f));
            rejectionText.name = "Fire Rejection";
            CreateText(root.transform, "Shift 冲刺   Alt 突进   空格 跳跃/双跳   Ctrl 下蹲/滑铲   E 蹬墙反弹   右键/F 钩索   F3 网络诊断", 20f,
                TextAlignmentOptions.Center, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 24f), new Vector2(1500f, 42f));
        }


        private static TextMeshProUGUI Panel(Transform parent, string name, Vector2 anchor, Vector2 position,
            Vector2 dimensions, float fontSize)
        {
            var target = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = target.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;
            var image = target.GetComponent<Image>();
            image.color = new Color(0.025f, 0.045f, 0.065f, 0.76f);
            image.raycastTarget = false;
            var text = CreateText(rect, "", fontSize, TextAlignmentOptions.TopLeft,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            text.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            text.rectTransform.offsetMin = new Vector2(18f, 12f);
            text.rectTransform.offsetMax = new Vector2(-18f, -12f);
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Truncate;
            return text;
        }

        private static TextMeshProUGUI CreateText(
            Transform parent,
            string value,
            float size,
            TextAlignmentOptions alignment,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 position,
            Vector2 dimensions)
        {
            GameObject target = new GameObject("Text", typeof(RectTransform));
            target.transform.SetParent(parent, false);
            RectTransform rect = target.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = anchorMin;
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;
            TextMeshProUGUI text = target.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSharedMaterial = textMaterial;
            text.text = value;
            text.fontSize = size;
            text.fontStyle = FontStyles.Bold;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.enableWordWrapping = false;


            return text;
        }

    }
}

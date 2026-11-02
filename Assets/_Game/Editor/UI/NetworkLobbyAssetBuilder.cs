using System;
using AFPS.Bootstrap;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace AFPS.Editor.UI
{
    /// <summary>仅首次生成大厅资源；已有 Prefab 和字体不覆盖，保留后续人工编辑。</summary>
    public sealed class NetworkLobbyAssetBuilder
    {
        public const string PrefabPath = "Assets/_Game/Art/Resources/UI/NetworkLobby.prefab";
        public const string FontPath = "Assets/_Game/Art/Resources/UI/NotoSansSC SDF.asset";
        private static readonly Color BackdropColor = new Color(0.025f, 0.035f, 0.055f, 0.96f);
        private static readonly Color PanelColor = new Color(0.075f, 0.095f, 0.135f, 0.98f);
        private static readonly Color FieldColor = new Color(0.12f, 0.15f, 0.20f, 1f);
        private static readonly Color AccentColor = new Color(0.12f, 0.70f, 0.94f, 1f);


        private static TMP_FontAsset font;
        private GameObject canvasRoot;
        private TMP_InputField addressInput;
        private TMP_InputField portInput;
        private TextMeshProUGUI statusText;
        private TextMeshProUGUI roomCodeText;
        private TextMeshProUGUI roomPlayersText;
        private Button copyRoomCodeButton;
        private Button hostButton;
        private Button joinButton;
        private Button practiceButton;
        private Button enterButton;
        private Button previousSpawnButton;
        private Button nextSpawnButton;
        private TextMeshProUGUI spawnText;
        private RectTransform mainPanel;
        private RectTransform discoveryPanel;
        private TextMeshProUGUI discoveryStatus;
        private Button refreshRoomsButton;
        private Button previousRoomsButton;
        private Button nextRoomsButton;
        private readonly Button[] roomButtons = new Button[4];

        [MenuItem("AFPS/UI/Create Lobby Prefab If Missing")]
        public static void Create()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            {
                Debug.Log("大厅 Prefab 已存在，保留编辑内容：" + PrefabPath);
                return;
            }

            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null)
            {
                // 64像素采样、9像素留白、2048×2048动态图集；允许按需增加图集覆盖中文。
                // 字体、材质及图集持久化，确保 Prefab 编辑模式和 Player 引用同一资源。
                font = TMP_FontAsset.CreateFontAsset(ChineseUiFont.Source, 64, 9,
                    GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
                font.name = "NotoSansSC SDF";
                AssetDatabase.CreateAsset(font, FontPath);
                AssetDatabase.AddObjectToAsset(font.material, font);
                foreach (Texture2D atlas in font.atlasTextures)
                {
                    AssetDatabase.AddObjectToAsset(atlas, font);
                }
            }

            var builder = new NetworkLobbyAssetBuilder();
            try
            {
                builder.BuildMenu();
                var view = builder.canvasRoot.AddComponent<NetworkLobbyView>();
                var serialized = new SerializedObject(view);
                serialized.FindProperty("addressInput").objectReferenceValue = builder.addressInput;
                serialized.FindProperty("portInput").objectReferenceValue = builder.portInput;
                serialized.FindProperty("statusText").objectReferenceValue = builder.statusText;
                serialized.FindProperty("roomCodeText").objectReferenceValue = builder.roomCodeText;
                serialized.FindProperty("roomPlayersText").objectReferenceValue = builder.roomPlayersText;
                serialized.FindProperty("copyRoomCodeButton").objectReferenceValue = builder.copyRoomCodeButton;
                serialized.FindProperty("hostButton").objectReferenceValue = builder.hostButton;
                serialized.FindProperty("joinButton").objectReferenceValue = builder.joinButton;
                serialized.FindProperty("practiceButton").objectReferenceValue = builder.practiceButton;
                serialized.FindProperty("enterButton").objectReferenceValue = builder.enterButton;
                serialized.FindProperty("previousSpawnButton").objectReferenceValue = builder.previousSpawnButton;
                serialized.FindProperty("nextSpawnButton").objectReferenceValue = builder.nextSpawnButton;
                serialized.FindProperty("spawnText").objectReferenceValue = builder.spawnText;
                serialized.FindProperty("mainPanel").objectReferenceValue = builder.mainPanel;
                serialized.FindProperty("discoveryPanel").objectReferenceValue = builder.discoveryPanel;
                serialized.FindProperty("discoveryStatus").objectReferenceValue = builder.discoveryStatus;
                serialized.FindProperty("refreshRoomsButton").objectReferenceValue = builder.refreshRoomsButton;
                serialized.FindProperty("previousRoomsButton").objectReferenceValue = builder.previousRoomsButton;
                serialized.FindProperty("nextRoomsButton").objectReferenceValue = builder.nextRoomsButton;
                var rows = serialized.FindProperty("roomButtons");
                rows.arraySize = builder.roomButtons.Length;
                for (int index = 0; index < builder.roomButtons.Length; index++)
                {
                    rows.GetArrayElementAtIndex(index).objectReferenceValue = builder.roomButtons[index];
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
                if (!view.TryValidate(out string error))
                {
                    throw new InvalidOperationException(error);
                }

                PrefabUtility.SaveAsPrefabAsset(builder.canvasRoot, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log("已生成可编辑大厅：" + PrefabPath);
            }
            finally
            {
                if (builder.canvasRoot != null)
                {
                    UnityEngine.Object.DestroyImmediate(builder.canvasRoot);
                }
            }
        }

        private void BuildMenu()
        {
            canvasRoot = new GameObject(
                "Network Connection UI",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            Canvas canvas = canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            CanvasScaler scaler = canvasRoot.GetComponent<CanvasScaler>();
            // 布局单位为1920×1080参考分辨率下的像素，宽高匹配权重各占一半。
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform backdrop = CreateRect(
                "Backdrop",
                canvasRoot.transform,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            backdrop.gameObject.AddComponent<Image>().color = BackdropColor;
            RectTransform panel = CreateRect(
                "Panel",
                backdrop,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(760f, 900f),
                Vector2.zero);
            panel.gameObject.AddComponent<Image>().color = PanelColor;

            CreateText(
                panel,
                "AFPS 局域网",
                38f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                new Vector2(0f, 370f),
                new Vector2(620f, 54f),
                Color.white);
            CreateText(
                panel,
                "创建房间，或使用房间码 / IPv4 地址加入。",
                20f,
                FontStyles.Normal,
                TextAlignmentOptions.Center,
                new Vector2(0f, 320f),
                new Vector2(620f, 36f),
                new Color(0.72f, 0.78f, 0.86f));
            CreateText(
                panel,
                "服务器地址 / 房间码",
                16f,
                FontStyles.Bold,
                TextAlignmentOptions.Left,
                new Vector2(0f, 270f),
                new Vector2(520f, 30f),
                new Color(0.72f, 0.78f, 0.86f));
            addressInput = CreateInputField(
                panel,
                "Server Address",
                "127.0.0.1",
                "IPv4 地址或 AF1 开头的房间码",
                new Vector2(0f, 230f),
                new Vector2(520f, 52f));
            addressInput.characterLimit = 64;
            CreateText(
                panel,
                "端口（使用房间码时自动读取）",
                16f,
                FontStyles.Bold,
                TextAlignmentOptions.Left,
                new Vector2(0f, 180f),
                new Vector2(520f, 30f),
                new Color(0.72f, 0.78f, 0.86f));
            portInput = CreateInputField(panel, "Port", "7777", "7777", new Vector2(0f, 140f), new Vector2(520f, 52f));
            portInput.contentType = TMP_InputField.ContentType.IntegerNumber;

            hostButton = CreateButton(panel, "Host Button", "创建房间", new Vector2(-135f, 60f), new Vector2(250f, 58f));
            joinButton = CreateButton(panel, "Join Button", "加入房间", new Vector2(135f, 60f), new Vector2(250f, 58f));
            practiceButton = CreateButton(panel, "Practice Button", "单人练习", new Vector2(0f, -10f),
                new Vector2(520f, 58f));
            roomCodeText = CreateText(panel, "房间码：创建房间后显示", 22f, FontStyles.Bold,
                TextAlignmentOptions.Center, new Vector2(0f, -78f), new Vector2(640f, 34f), Color.white);
            copyRoomCodeButton = CreateButton(panel, "Copy Room Code Button", "复制房间码",
                new Vector2(0f, -128f), new Vector2(520f, 44f));
            copyRoomCodeButton.interactable = false;
            roomPlayersText = CreateText(panel, "房间人数：尚未连接", 20f, FontStyles.Normal,
                TextAlignmentOptions.Center, new Vector2(0f, -183f), new Vector2(640f, 34f), Color.white);
            previousSpawnButton = CreateButton(panel, "Previous Spawn Button", "上一个",
                new Vector2(-205f, -230f), new Vector2(110f, 36f));
            nextSpawnButton = CreateButton(panel, "Next Spawn Button", "下一个",
                new Vector2(205f, -230f), new Vector2(110f, 36f));
            spawnText = CreateText(panel, "", 17f, FontStyles.Normal, TextAlignmentOptions.Center,
                new Vector2(0f, -230f), new Vector2(300f, 36f), Color.white);
            previousSpawnButton.gameObject.SetActive(false);
            nextSpawnButton.gameObject.SetActive(false);
            spawnText.gameObject.SetActive(false);
            statusText = CreateText(
                panel,
                "本机地址：尚未连接",
                18f,
                FontStyles.Normal,
                TextAlignmentOptions.Center,
                new Vector2(0f, -282f),
                new Vector2(640f, 54f),
                new Color(0.66f, 0.75f, 0.84f));
            statusText.enableWordWrapping = true;
            enterButton = CreateButton(panel, "Enter Button", "进入游戏", new Vector2(0f, -340f), new Vector2(520f, 58f));
            enterButton.gameObject.SetActive(false);
            CreateText(panel, "房间码包含地址与端口，同一局域网内使用。", 17f, FontStyles.Normal,
                TextAlignmentOptions.Center, new Vector2(0f, -400f), new Vector2(640f, 30f), new Color(0.72f, 0.78f, 0.86f));
            mainPanel = panel;
            BuildDiscoveryMenu(backdrop);
        }

        private void BuildDiscoveryMenu(Transform backdrop)
        {
            mainPanel.anchoredPosition = new Vector2(-330f, 0f);
            discoveryPanel = CreateRect("LAN Rooms", backdrop, new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(620f, 900f), new Vector2(410f, 0f));
            discoveryPanel.gameObject.AddComponent<Image>().color = PanelColor;
            CreateText(discoveryPanel, "局域网房间", 32f, FontStyles.Bold, TextAlignmentOptions.Center,
                new Vector2(0f, 370f), new Vector2(540f, 54f), Color.white);
            CreateText(discoveryPanel, "自动搜索同一局域网内的房间", 19f, FontStyles.Normal,
                TextAlignmentOptions.Center, new Vector2(0f, 320f), new Vector2(540f, 36f), Color.white);
            refreshRoomsButton = CreateButton(discoveryPanel, "Refresh Rooms Button", "刷新房间",
                new Vector2(0f, 260f), new Vector2(540f, 50f));
            discoveryStatus = CreateText(discoveryPanel, "正在搜索…", 18f, FontStyles.Normal,
                TextAlignmentOptions.Center, new Vector2(0f, 205f), new Vector2(540f, 42f), Color.white);
            for (int index = 0; index < roomButtons.Length; index++)
            {
                roomButtons[index] = CreateButton(discoveryPanel, "Room Row " + index, "",
                    new Vector2(0f, 120f - 145f * index), new Vector2(540f, 120f));
                roomButtons[index].gameObject.SetActive(false);
            }
            previousRoomsButton = CreateButton(discoveryPanel, "Previous Rooms Button", "上一页",
                new Vector2(-140f, -410f), new Vector2(260f, 36f));
            nextRoomsButton = CreateButton(discoveryPanel, "Next Rooms Button", "下一页",
                new Vector2(140f, -410f), new Vector2(260f, 36f));
        }

        private static RectTransform CreateRect(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 size,
            Vector2 position)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private static TextMeshProUGUI CreateText(
            Transform parent,
            string text,
            float fontSize,
            FontStyles style,
            TextAlignmentOptions alignment,
            Vector2 position,
            Vector2 size,
            Color color)
        {
            RectTransform rect = CreateRect(
                "Text",
                parent,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                size,
                position);
            TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.alignment = alignment;
            label.color = color;
            label.raycastTarget = false;
            return label;
        }

        private static TMP_InputField CreateInputField(
            Transform parent,
            string name,
            string value,
            string placeholderText,
            Vector2 position,
            Vector2 size)
        {
            RectTransform root = CreateRect(
                name,
                parent,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                size,
                position);
            Image background = root.gameObject.AddComponent<Image>();
            background.color = FieldColor;
            TMP_InputField input = root.gameObject.AddComponent<TMP_InputField>();
            RectTransform viewport = CreateRect(
                "Text Area",
                root,
                Vector2.zero,
                Vector2.one,
                new Vector2(-32f, -12f),
                Vector2.zero);
            viewport.gameObject.AddComponent<RectMask2D>();

            TextMeshProUGUI text = CreateText(
                viewport,
                value,
                22f,
                FontStyles.Normal,
                TextAlignmentOptions.MidlineLeft,
                Vector2.zero,
                Vector2.zero,
                Color.white);
            Stretch(text.rectTransform);
            TextMeshProUGUI placeholder = CreateText(
                viewport,
                placeholderText,
                22f,
                FontStyles.Italic,
                TextAlignmentOptions.MidlineLeft,
                Vector2.zero,
                Vector2.zero,
                new Color(0.5f, 0.55f, 0.62f));
            Stretch(placeholder.rectTransform);

            input.textViewport = viewport;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.text = value;
            input.pointSize = 22f;
            input.caretColor = Color.white;
            input.selectionColor = new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.35f);
            return input;
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 position, Vector2 size)
        {
            RectTransform root = CreateRect(
                name,
                parent,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                size,
                position);
            Image image = root.gameObject.AddComponent<Image>();
            image.color = AccentColor;
            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            TextMeshProUGUI text = CreateText(
                root,
                label,
                19f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                Vector2.zero,
                Vector2.zero,
                Color.white);
            Stretch(text.rectTransform);
            return button;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

    }
}

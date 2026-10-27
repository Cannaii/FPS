using System.Net;
using System.Net.Sockets;
using AFPS.Input;
using AFPS.NetCode.Runtime;
using AFPS.NetCode.Sessions;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace AFPS.Bootstrap
{
    /// <summary>Runtime-created LAN host/join menu for normal player launches.</summary>
    public sealed class NetworkConnectionMenu : MonoBehaviour
    {
        private static readonly Color BackdropColor = new Color(0.025f, 0.035f, 0.055f, 0.96f);
        private static readonly Color PanelColor = new Color(0.075f, 0.095f, 0.135f, 0.98f);
        private static readonly Color FieldColor = new Color(0.12f, 0.15f, 0.20f, 1f);
        private static readonly Color AccentColor = new Color(0.12f, 0.70f, 0.94f, 1f);

        private UnityNetworkBootstrap bootstrap;
        private LocalPlayerInputCollector inputCollector;
        private GameObject canvasRoot;
        private TMP_InputField addressInput;
        private TMP_InputField portInput;
        private TextMeshProUGUI statusText;
        private Button hostButton;
        private Button joinButton;
        private Button practiceButton;
        private Button enterButton;
        private bool initialized;
        private string localAddress;

        public void Initialize(UnityNetworkBootstrap owner)
        {
            if (initialized)
            {
                return;
            }

            bootstrap = owner;
            initialized = true;
            localAddress = GetLocalIpv4Address();
            inputCollector = FindObjectOfType<LocalPlayerInputCollector>(true);
            if (inputCollector != null)
            {
                inputCollector.enabled = false;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            EnsureEventSystem();
            BuildMenu();
        }

        private void Update()
        {
            if (bootstrap == null || bootstrap.Runtime == null)
            {
                return;
            }

            UnityNetworkMovementController movement = bootstrap.GetComponent<UnityNetworkMovementController>();
            if (movement == null || movement.SessionManager == null)
            {
                return;
            }

            bool ready = movement.CanEnterGame;
            enterButton.gameObject.SetActive(ready);
            if (ready)
            {
                if (bootstrap.ActiveMode == NetworkLaunchMode.Practice)
                {
                    EnterGame();
                    return;
                }
                string message = bootstrap.ActiveMode == NetworkLaunchMode.Host
                    ? $"房间地址：{localAddress}:{portInput.text}，地图验证通过。"
                    : "地图验证通过，可以进入游戏。";
                SetStatus(message, false);
            }
            else if (!string.IsNullOrEmpty(movement.ConnectionStatusMessage))
            {
                ConnectionAdmissionStatus status = movement.SessionManager.ClientAdmissionStatus;
                bool failed = status != ConnectionAdmissionStatus.AwaitingHandshake
                    && status != ConnectionAdmissionStatus.Admitted;
                SetStatus(movement.ConnectionStatusMessage, failed);
            }
        }

        private void StartNetwork(NetworkLaunchMode mode)
        {
            if (!NetworkConnectionInput.TryCreateOptions(mode, addressInput.text, portInput.text, bootstrap.DefaultMaxConnections, out NetworkLaunchOptions options, out string error))
            {
                SetStatus(error, true);
                return;
            }

            SetButtonsInteractable(false);
            SetStatus(mode == NetworkLaunchMode.Practice ? "正在启动单人练习…"
                : mode == NetworkLaunchMode.Host ? "正在创建房间…" : "正在连接服务器…", false);
            if (!bootstrap.TryStartNetwork(options, out error))
            {
                SetStatus(error, true);
                SetButtonsInteractable(true);
                return;
            }
        }

        private void EnterGame()
        {
            UnityNetworkMovementController movement = bootstrap.GetComponent<UnityNetworkMovementController>();
            if (movement == null || !movement.CanEnterGame)
            {
                return;
            }

            if (inputCollector != null)
            {
                inputCollector.enabled = true;
            }

            if (canvasRoot != null)
            {
                Destroy(canvasRoot);
            }

            Destroy(this);
        }

        private void BuildMenu()
        {
            canvasRoot = new GameObject("Network Connection UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            CanvasScaler scaler = canvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform backdrop = CreateRect("Backdrop", canvasRoot.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            backdrop.gameObject.AddComponent<Image>().color = BackdropColor;
            RectTransform panel = CreateRect("Panel", backdrop, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(660f, 700f), Vector2.zero);
            panel.gameObject.AddComponent<Image>().color = PanelColor;

            CreateText(panel, "AFPS 局域网", 38f, FontStyles.Bold, TextAlignmentOptions.Center, new Vector2(0f, 224f), new Vector2(560f, 54f), Color.white);
            CreateText(panel, "创建房间，或输入服务器 IPv4 地址加入。", 20f, FontStyles.Normal, TextAlignmentOptions.Center, new Vector2(0f, 178f), new Vector2(560f, 36f), new Color(0.72f, 0.78f, 0.86f));
            CreateText(panel, "服务器地址", 16f, FontStyles.Bold, TextAlignmentOptions.Left, new Vector2(-190f, 116f), new Vector2(180f, 30f), new Color(0.72f, 0.78f, 0.86f));
            addressInput = CreateInputField(panel, "Server Address", bootstrap.DefaultServerAddress, "192.168.1.20", new Vector2(0f, 77f), new Vector2(520f, 52f));
            CreateText(panel, "端口", 16f, FontStyles.Bold, TextAlignmentOptions.Left, new Vector2(-220f, 27f), new Vector2(120f, 30f), new Color(0.72f, 0.78f, 0.86f));
            portInput = CreateInputField(panel, "Port", bootstrap.DefaultPort.ToString(), "7777", new Vector2(0f, -12f), new Vector2(520f, 52f));
            portInput.contentType = TMP_InputField.ContentType.IntegerNumber;

            hostButton = CreateButton(panel, "Host Button", "创建房间", new Vector2(-135f, -91f), new Vector2(250f, 58f), () => StartNetwork(NetworkLaunchMode.Host));
            joinButton = CreateButton(panel, "Join Button", "加入房间", new Vector2(135f, -91f), new Vector2(250f, 58f), () => StartNetwork(NetworkLaunchMode.Client));
            practiceButton = CreateButton(panel, "Practice Button", "单人练习", new Vector2(0f, -162f),
                new Vector2(520f, 58f), () => StartNetwork(NetworkLaunchMode.Practice));
            statusText = CreateText(panel, $"本机地址：{localAddress}", 18f, FontStyles.Normal, TextAlignmentOptions.Center, new Vector2(0f, -229f), new Vector2(560f, 54f), new Color(0.66f, 0.75f, 0.84f));
            statusText.enableWordWrapping = true;
            enterButton = CreateButton(panel, "Enter Button", "进入游戏", new Vector2(0f, -294f), new Vector2(520f, 58f), EnterGame);
            enterButton.gameObject.SetActive(false);
        }

        private void SetButtonsInteractable(bool interactable)
        {
            hostButton.interactable = interactable;
            joinButton.interactable = interactable;
            practiceButton.interactable = interactable;
            addressInput.interactable = interactable;
            portInput.interactable = interactable;
        }

        private void SetStatus(string message, bool isError)
        {
            statusText.text = message;
            statusText.color = isError ? new Color(1f, 0.38f, 0.38f) : new Color(0.42f, 0.88f, 0.68f);
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            DontDestroyOnLoad(eventSystem);
        }

        private static string GetLocalIpv4Address()
        {
            try
            {
                IPAddress[] addresses = Dns.GetHostEntry(Dns.GetHostName()).AddressList;
                for (int i = 0; i < addresses.Length; i++)
                {
                    if (addresses[i].AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(addresses[i]))
                    {
                        return addresses[i].ToString();
                    }
                }
            }
            catch (SocketException)
            {
            }

            return "127.0.0.1";
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Vector2 position)
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

        private static TextMeshProUGUI CreateText(Transform parent, string text, float fontSize, FontStyles style, TextAlignmentOptions alignment, Vector2 position, Vector2 size, Color color)
        {
            RectTransform rect = CreateRect("Text", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), size, position);
            TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = ChineseUiFont.Asset;
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.alignment = alignment;
            label.color = color;
            label.raycastTarget = false;
            return label;
        }

        private static TMP_InputField CreateInputField(Transform parent, string name, string value, string placeholderText, Vector2 position, Vector2 size)
        {
            RectTransform root = CreateRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), size, position);
            Image background = root.gameObject.AddComponent<Image>();
            background.color = FieldColor;
            TMP_InputField input = root.gameObject.AddComponent<TMP_InputField>();
            RectTransform viewport = CreateRect("Text Area", root, Vector2.zero, Vector2.one, new Vector2(-32f, -12f), Vector2.zero);
            viewport.gameObject.AddComponent<RectMask2D>();

            TextMeshProUGUI text = CreateText(viewport, value, 22f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, Vector2.zero, Vector2.zero, Color.white);
            Stretch(text.rectTransform);
            TextMeshProUGUI placeholder = CreateText(viewport, placeholderText, 22f, FontStyles.Italic, TextAlignmentOptions.MidlineLeft, Vector2.zero, Vector2.zero, new Color(0.5f, 0.55f, 0.62f));
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

        private static Button CreateButton(Transform parent, string name, string label, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            RectTransform root = CreateRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), size, position);
            Image image = root.gameObject.AddComponent<Image>();
            image.color = AccentColor;
            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);
            TextMeshProUGUI text = CreateText(root, label, 19f, FontStyles.Bold, TextAlignmentOptions.Center, Vector2.zero, Vector2.zero, Color.white);
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

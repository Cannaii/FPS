using System;
using System.Net;
using System.Net.Sockets;
using AFPS.Input;
using AFPS.NetCode.Runtime;
using AFPS.NetCode.Transport;
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
        private Button enterButton;
        private bool initialized;

        public void Initialize(UnityNetworkBootstrap owner)
        {
            if (initialized)
            {
                return;
            }

            bootstrap = owner;
            initialized = true;
            bootstrap.TransportEventReceived += HandleTransportEvent;
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

        private void OnDestroy()
        {
            if (bootstrap != null)
            {
                bootstrap.TransportEventReceived -= HandleTransportEvent;
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
            SetStatus(mode == NetworkLaunchMode.Host ? "Starting host..." : "Connecting...", false);
            if (!bootstrap.TryStartNetwork(options, out error))
            {
                SetStatus(error, true);
                SetButtonsInteractable(true);
                return;
            }

            if (mode == NetworkLaunchMode.Host)
            {
                SetStatus($"Room ready at {GetLocalIpv4Address()}:{options.Port}", false);
                enterButton.gameObject.SetActive(true);
            }
        }

        private void HandleTransportEvent(NetworkTransportSide side, GameTransportEvent transportEvent, ArraySegment<byte> payload)
        {
            if (side != NetworkTransportSide.Client)
            {
                return;
            }

            if (transportEvent.Type == TransportEventType.Connected)
            {
                SetStatus("Connected. Ready to enter.", false);
                enterButton.gameObject.SetActive(true);
            }
            else if (transportEvent.Type == TransportEventType.Disconnected)
            {
                SetStatus("Connection lost. Restart the game to try again.", true);
                enterButton.gameObject.SetActive(false);
            }
        }

        private void EnterGame()
        {
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
            RectTransform panel = CreateRect("Panel", backdrop, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(660f, 570f), Vector2.zero);
            panel.gameObject.AddComponent<Image>().color = PanelColor;

            CreateText(panel, "AFPS LAN", 38f, FontStyles.Bold, TextAlignmentOptions.Center, new Vector2(0f, 224f), new Vector2(560f, 54f), Color.white);
            CreateText(panel, "Host a game or join a direct IPv4 address.", 20f, FontStyles.Normal, TextAlignmentOptions.Center, new Vector2(0f, 178f), new Vector2(560f, 36f), new Color(0.72f, 0.78f, 0.86f));
            CreateText(panel, "SERVER IP", 16f, FontStyles.Bold, TextAlignmentOptions.Left, new Vector2(-190f, 116f), new Vector2(180f, 30f), new Color(0.72f, 0.78f, 0.86f));
            addressInput = CreateInputField(panel, "Server Address", bootstrap.DefaultServerAddress, "192.168.1.20", new Vector2(0f, 77f), new Vector2(520f, 52f));
            CreateText(panel, "PORT", 16f, FontStyles.Bold, TextAlignmentOptions.Left, new Vector2(-220f, 27f), new Vector2(120f, 30f), new Color(0.72f, 0.78f, 0.86f));
            portInput = CreateInputField(panel, "Port", bootstrap.DefaultPort.ToString(), "7777", new Vector2(0f, -12f), new Vector2(520f, 52f));
            portInput.contentType = TMP_InputField.ContentType.IntegerNumber;

            hostButton = CreateButton(panel, "Host Button", "HOST GAME", new Vector2(-135f, -91f), new Vector2(250f, 58f), () => StartNetwork(NetworkLaunchMode.Host));
            joinButton = CreateButton(panel, "Join Button", "JOIN GAME", new Vector2(135f, -91f), new Vector2(250f, 58f), () => StartNetwork(NetworkLaunchMode.Client));
            statusText = CreateText(panel, $"Local address: {GetLocalIpv4Address()}", 18f, FontStyles.Normal, TextAlignmentOptions.Center, new Vector2(0f, -159f), new Vector2(560f, 54f), new Color(0.66f, 0.75f, 0.84f));
            statusText.enableWordWrapping = true;
            enterButton = CreateButton(panel, "Enter Button", "ENTER GAME", new Vector2(0f, -224f), new Vector2(520f, 58f), EnterGame);
            enterButton.gameObject.SetActive(false);
        }

        private void SetButtonsInteractable(bool interactable)
        {
            hostButton.interactable = interactable;
            joinButton.interactable = interactable;
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

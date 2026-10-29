using System.Net;
using System.Net.Sockets;
using AFPS.Input;
using AFPS.NetCode.Runtime;
using AFPS.NetCode.Match;
using AFPS.NetCode.Sessions;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace AFPS.Bootstrap
{
    /// <summary>中文联网大厅：发现/创建/加入房间、人数、出生点、准备/倒计时与进入对局。</summary>
    // 保留旧组件类型的迁移信息；文件与.meta成对移动，场景/Prefab继续沿用原GUID。
    [UnityEngine.Scripting.APIUpdating.MovedFrom(
        true, sourceNamespace: "AFPS.Bootstrap", sourceAssembly: "_Game.Bootstrap",
        sourceClassName: "NetworkConnectionMenu")]
    public sealed partial class NetworkLobbyMenu : MonoBehaviour
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
        private bool initialized;
        private string localAddress;
        private double copiedUntil;

        public string CurrentRoomCode { get; private set; }

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
            if (bootstrap.Runtime == null)
            {
                StartBrowsing();
            }
            else
            {
                SetButtonsInteractable(false);
                portInput.text = bootstrap.ActiveOptions.Port.ToString();
                addressInput.text = bootstrap.ActiveOptions.ServerAddress;
                discoveryPanel.gameObject.SetActive(false);
                mainPanel.anchoredPosition = Vector2.zero;
                if (bootstrap.ActiveMode == NetworkLaunchMode.Host
                    && RoomCode.TryEncode(localAddress, bootstrap.ActiveOptions.Port, out string code, out _))
                {
                    CurrentRoomCode = code;
                    roomCodeText.text = "房间码：" + code;
                    copyRoomCodeButton.interactable = true;
                }
            }
        }

        private void Update()
        {
            if (bootstrap == null)
            {
                return;
            }
            if (bootstrap.Runtime == null)
            {
                UpdateBrowsing();
                return;
            }

            UnityNetworkGameplayController movement = bootstrap.GetComponent<UnityNetworkGameplayController>();
            if (movement == null || movement.SessionCoordinator == null)
            {
                return;
            }

            bool ready = movement.CanEnterGame;
            UpdateSpawnSelection(movement.SessionCoordinator, ready);
            enterButton.gameObject.SetActive(ready);
            var match = movement.SessionCoordinator.ClientDeathmatchState;
            roomPlayersText.text = match != null
                ? $"在线玩家：{match.OnlinePlayerCount}    暂离玩家：{match.PlayerCount - match.OnlinePlayerCount}"
                : "正在同步房间人数…";
            copyRoomCodeButton.GetComponentInChildren<TextMeshProUGUI>().text =
                Time.realtimeSinceStartupAsDouble < copiedUntil ? "房间码已复制" : "复制房间码";
            if (ready)
            {
                if (bootstrap.ActiveMode == NetworkLaunchMode.Practice)
                {
                    EnterGame();
                    return;
                }
                var manager = movement.SessionCoordinator;
                bool hasPlayer = manager.TryGetLocalLobbyPlayer(out var player);
                var phase = manager.ClientLobby?.Phase;
                enterButton.GetComponentInChildren<TextMeshProUGUI>().text = phase == LobbyPhase.Playing ? "进入游戏"
                    : phase == LobbyPhase.Finished ? "查看结果" : manager.LobbyReadyPending ? "等待服务器确认…"
                    : hasPlayer && player.Ready ? "取消准备" : "准备";
                enterButton.interactable = phase == LobbyPhase.Playing || phase == LobbyPhase.Finished
                    || (hasPlayer && player.Connected && !player.Recovering && !manager.ClientRecoveryPending
                        && !manager.LobbyReadyPending);
                string message = phase == LobbyPhase.Playing ? "对局已开始，可以进入游戏。"
                    : phase == LobbyPhase.Finished ? "本局已结束，可以查看结果。"
                    : phase == LobbyPhase.Countdown ? $"全部玩家已准备，{manager.LobbyCountdownSeconds} 秒后开局。"
                    : hasPlayer ? player.Ready ? "已准备，等待其他在线玩家。" : "地图验证通过，请点击准备。"
                    : "正在同步大厅状态…";
                if (manager.SpawnSelectionDeclined && phase == LobbyPhase.Waiting)
                {
                    message = "该出生点已被占用，请重新选择。";
                }
                SetStatus(message, false);
            }
            else if (!string.IsNullOrEmpty(movement.ConnectionStatusMessage))
            {
                ConnectionAdmissionStatus status = movement.SessionCoordinator.ClientAdmissionStatus;
                bool failed = status != ConnectionAdmissionStatus.AwaitingHandshake
                    && status != ConnectionAdmissionStatus.Admitted;
                SetStatus(movement.ConnectionStatusMessage, failed);
            }
        }

        private void UpdateSpawnSelection(AFPS.NetCode.Sessions.NetworkSessionCoordinator manager, bool admitted)
        {
            bool visible = admitted && bootstrap.ActiveMode != NetworkLaunchMode.Practice;
            previousSpawnButton.gameObject.SetActive(visible);
            nextSpawnButton.gameObject.SetActive(visible);
            spawnText.gameObject.SetActive(visible);
            bool editable = visible && manager.TryGetLocalLobbyPlayer(out var player)
                && !player.Ready && !player.Recovering && !manager.LobbyReadyPending
                && !manager.ClientRecoveryPending && manager.ClientLobby.Phase == LobbyPhase.Waiting;
            previousSpawnButton.interactable = editable;
            nextSpawnButton.interactable = editable;
            if (manager.TryGetLocalLobbyPlayer(out var local)
                && manager.TryGetSpawnPosition(local.SpawnSlot, out var position))
            {
                spawnText.text = $"出生点 {local.SpawnSlot}（{position.X.ToFloat():0.#}, "
                    + $"{position.Y.ToFloat():0.#}, {position.Z.ToFloat():0.#} 米）";
            }
            else
            {
                spawnText.text = "正在同步出生点…";
            }
        }

        private void SelectAdjacentSpawn(int direction)
        {
            var manager = bootstrap.GetComponent<UnityNetworkGameplayController>()?.SessionCoordinator;
            if (manager == null || !manager.TryGetLocalLobbyPlayer(out var player))
            {
                return;
            }
            for (int offset = 1; offset < AFPS.NetCode.Match.LobbyStartGate.MaximumPlayers; offset++)
            {
                byte slot = (byte)((player.SpawnSlot - 1 + direction * offset + 64) % 64 + 1);
                if (manager.IsClientSpawnSlotAvailable(slot))
                {
                    manager.TrySelectSpawnSlot(slot);
                    return;
                }
            }
            SetStatus("没有空闲出生点。", false);
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
            if (mode == NetworkLaunchMode.Host
                && RoomCode.TryEncode(localAddress, options.Port, out string code, out _))
            {
                CurrentRoomCode = code;
                roomCodeText.text = "房间码：" + code;
                copyRoomCodeButton.interactable = true;
            }
            else if (mode == NetworkLaunchMode.Client)
            {
                roomCodeText.text = $"目标服务器：{options.ServerAddress}:{options.Port}";
            }
            StopBrowsing();
            discoveryPanel.gameObject.SetActive(false);
            mainPanel.anchoredPosition = Vector2.zero;
        }

        private void CopyRoomCode()
        {
            if (string.IsNullOrEmpty(CurrentRoomCode))
            {
                return;
            }
            GUIUtility.systemCopyBuffer = CurrentRoomCode;
            copiedUntil = Time.realtimeSinceStartupAsDouble + 2;
        }

        private void EnterGame()
        {
            UnityNetworkGameplayController movement = bootstrap.GetComponent<UnityNetworkGameplayController>();
            if (movement == null || !movement.CanEnterGame)
            {
                return;
            }
            var manager = movement.SessionCoordinator;
            if (!manager.IsLocalPractice && !manager.CanEnterMatch && manager.ClientLobby?.Phase != LobbyPhase.Finished)
            {
                if (manager.TryGetLocalLobbyPlayer(out var player))
                {
                    manager.TrySetLobbyReady(!player.Ready);
                }
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
            RectTransform panel = CreateRect("Panel", backdrop, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(760f, 900f), Vector2.zero);
            panel.gameObject.AddComponent<Image>().color = PanelColor;

            CreateText(panel, "AFPS 局域网", 38f, FontStyles.Bold, TextAlignmentOptions.Center, new Vector2(0f, 370f), new Vector2(620f, 54f), Color.white);
            CreateText(panel, "创建房间，或使用房间码 / IPv4 地址加入。", 20f, FontStyles.Normal, TextAlignmentOptions.Center, new Vector2(0f, 320f), new Vector2(620f, 36f), new Color(0.72f, 0.78f, 0.86f));
            CreateText(panel, "服务器地址 / 房间码", 16f, FontStyles.Bold, TextAlignmentOptions.Left, new Vector2(0f, 270f), new Vector2(520f, 30f), new Color(0.72f, 0.78f, 0.86f));
            addressInput = CreateInputField(panel, "Server Address", bootstrap.DefaultServerAddress, "IPv4 地址或 AF1 开头的房间码", new Vector2(0f, 230f), new Vector2(520f, 52f));
            addressInput.characterLimit = 64;
            CreateText(panel, "端口（使用房间码时自动读取）", 16f, FontStyles.Bold, TextAlignmentOptions.Left, new Vector2(0f, 180f), new Vector2(520f, 30f), new Color(0.72f, 0.78f, 0.86f));
            portInput = CreateInputField(panel, "Port", bootstrap.DefaultPort.ToString(), "7777", new Vector2(0f, 140f), new Vector2(520f, 52f));
            portInput.contentType = TMP_InputField.ContentType.IntegerNumber;

            hostButton = CreateButton(panel, "Host Button", "创建房间", new Vector2(-135f, 60f), new Vector2(250f, 58f), () => StartNetwork(NetworkLaunchMode.Host));
            joinButton = CreateButton(panel, "Join Button", "加入房间", new Vector2(135f, 60f), new Vector2(250f, 58f), () => StartNetwork(NetworkLaunchMode.Client));
            practiceButton = CreateButton(panel, "Practice Button", "单人练习", new Vector2(0f, -10f),
                new Vector2(520f, 58f), () => StartNetwork(NetworkLaunchMode.Practice));
            roomCodeText = CreateText(panel, "房间码：创建房间后显示", 22f, FontStyles.Bold,
                TextAlignmentOptions.Center, new Vector2(0f, -78f), new Vector2(640f, 34f), Color.white);
            copyRoomCodeButton = CreateButton(panel, "Copy Room Code Button", "复制房间码",
                new Vector2(0f, -128f), new Vector2(520f, 44f), CopyRoomCode);
            copyRoomCodeButton.interactable = false;
            roomPlayersText = CreateText(panel, "房间人数：尚未连接", 20f, FontStyles.Normal,
                TextAlignmentOptions.Center, new Vector2(0f, -183f), new Vector2(640f, 34f), Color.white);
            previousSpawnButton = CreateButton(panel, "Previous Spawn Button", "上一个",
                new Vector2(-205f, -230f), new Vector2(110f, 36f), () => SelectAdjacentSpawn(-1));
            nextSpawnButton = CreateButton(panel, "Next Spawn Button", "下一个",
                new Vector2(205f, -230f), new Vector2(110f, 36f), () => SelectAdjacentSpawn(1));
            spawnText = CreateText(panel, "", 17f, FontStyles.Normal, TextAlignmentOptions.Center,
                new Vector2(0f, -230f), new Vector2(300f, 36f), Color.white);
            previousSpawnButton.gameObject.SetActive(false);
            nextSpawnButton.gameObject.SetActive(false);
            spawnText.gameObject.SetActive(false);
            statusText = CreateText(panel, $"本机地址：{localAddress}", 18f, FontStyles.Normal, TextAlignmentOptions.Center, new Vector2(0f, -282f), new Vector2(640f, 54f), new Color(0.66f, 0.75f, 0.84f));
            statusText.enableWordWrapping = true;
            enterButton = CreateButton(panel, "Enter Button", "进入游戏", new Vector2(0f, -340f), new Vector2(520f, 58f), EnterGame);
            enterButton.gameObject.SetActive(false);
            CreateText(panel, "房间码包含地址与端口，同一局域网内使用。", 17f, FontStyles.Normal,
                TextAlignmentOptions.Center, new Vector2(0f, -400f), new Vector2(640f, 30f), new Color(0.72f, 0.78f, 0.86f));
            mainPanel = panel;
            BuildDiscoveryMenu(backdrop);
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

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
            NetworkLobbyView prefab = Resources.Load<NetworkLobbyView>(NetworkLobbyView.ResourcePath);
            if (prefab == null || !prefab.TryValidate(out _))
            {
                throw new System.InvalidOperationException(
                    "大厅资源缺失或引用不完整，请检查 Resources/UI/NetworkLobby.prefab。");
            }

            NetworkLobbyView view = Instantiate(prefab);
            canvasRoot = view.gameObject;
            addressInput = view.AddressInput;
            portInput = view.PortInput;
            statusText = view.StatusText;
            roomCodeText = view.RoomCodeText;
            roomPlayersText = view.RoomPlayersText;
            copyRoomCodeButton = view.CopyRoomCodeButton;
            hostButton = view.HostButton;
            joinButton = view.JoinButton;
            practiceButton = view.PracticeButton;
            enterButton = view.EnterButton;
            previousSpawnButton = view.PreviousSpawnButton;
            nextSpawnButton = view.NextSpawnButton;
            spawnText = view.SpawnText;
            mainPanel = view.MainPanel;
            discoveryPanel = view.DiscoveryPanel;
            discoveryStatus = view.DiscoveryStatus;
            refreshRoomsButton = view.RefreshRoomsButton;
            previousRoomsButton = view.PreviousRoomsButton;
            nextRoomsButton = view.NextRoomsButton;
            for (int index = 0; index < roomButtons.Length; index++)
            {
                int row = index;
                roomButtons[index] = view.RoomButtons[index];
                roomButtons[index].onClick.AddListener(() => JoinListedRoom(row));
            }

            // 只初始化会话数据与事件；位置、字号、颜色和输入控件配置由 Prefab 保存。
            addressInput.SetTextWithoutNotify(bootstrap.DefaultServerAddress);
            portInput.SetTextWithoutNotify(bootstrap.DefaultPort.ToString());
            statusText.text = $"本机地址：{localAddress}";
            hostButton.onClick.AddListener(() => StartNetwork(NetworkLaunchMode.Host));
            joinButton.onClick.AddListener(() => StartNetwork(NetworkLaunchMode.Client));
            practiceButton.onClick.AddListener(() => StartNetwork(NetworkLaunchMode.Practice));
            copyRoomCodeButton.onClick.AddListener(CopyRoomCode);
            previousSpawnButton.onClick.AddListener(() => SelectAdjacentSpawn(-1));
            nextSpawnButton.onClick.AddListener(() => SelectAdjacentSpawn(1));
            enterButton.onClick.AddListener(EnterGame);
            refreshRoomsButton.onClick.AddListener(RefreshRooms);
            previousRoomsButton.onClick.AddListener(() => ChangeRoomPage(-1));
            nextRoomsButton.onClick.AddListener(() => ChangeRoomPage(1));
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

    }
}

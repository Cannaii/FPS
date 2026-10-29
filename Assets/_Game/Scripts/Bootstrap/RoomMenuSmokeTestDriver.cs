using System;
using System.IO;
using System.Linq;
using AFPS.Input;
using AFPS.NetCode.Runtime;
using AFPS.NetCode.Match;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AFPS.Bootstrap
{
    /// <summary>仅显式启动：通过真实菜单创建房间、复制房间码及加入，验证权威人数后进入游戏。</summary>
    public sealed class RoomMenuSmokeTestDriver : MonoBehaviour
    {
        public static bool IsRequested => Array.Exists(Environment.GetCommandLineArgs(),
            argument => argument.Equals("-afpsRoomMenuSmoke", StringComparison.OrdinalIgnoreCase));

        private UnityNetworkBootstrap bootstrap;
        private UnityNetworkMovementController movement;
        private LocalPlayerInputCollector input;
        private double started;
        private string role;
        private string codePath;
        private ushort port;
        private bool clicked;
        private bool codePublished;
        private bool invalidRejected;
        private bool discoveryJoin;
        private bool discovered;
        private bool capturedCountdown;
        private bool sawCountdown;
        private bool cancelMode;
        private bool canceled;
        private bool cancelConfirmed;
        private bool restartedCountdown;
        private double reReadyAt;
        private bool spawnRequested;
        private bool spawnConfirmed;
        private byte originalSpawn;

        public void Configure(UnityNetworkBootstrap owner)
        {
            bootstrap = owner;
            movement = owner.GetComponent<UnityNetworkMovementController>();
            input = FindObjectOfType<LocalPlayerInputCollector>(true);
            started = Time.realtimeSinceStartupAsDouble;
            string[] arguments = Environment.GetCommandLineArgs();
            discoveryJoin = Array.Exists(arguments, argument => argument == "-afpsRoomDiscoveryJoin");
            cancelMode = Array.Exists(arguments, argument => argument == "-afpsLobbyCancelTest");
            for (int index = 0; index + 1 < arguments.Length; index++)
            {
                if (arguments[index] == "-afpsRoomMenuRole")
                {
                    role = arguments[index + 1];
                }
                else if (arguments[index] == "-afpsRoomMenuCodeFile")
                {
                    codePath = arguments[index + 1];
                }
                else if (arguments[index] == "-afpsRoomMenuPort")
                {
                    ushort.TryParse(arguments[index + 1], out port);
                }
            }
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
        }

        private void Update()
        {
            if ((role != "Host" && role != "Client") || port == 0 || string.IsNullOrEmpty(codePath))
            {
                Finish(false, "invalidArguments=True");
                return;
            }
            // 秒，单调实时时间；测试不延长协议准入超时，也不自动重建失败的会话。
            if (Time.realtimeSinceStartupAsDouble - started > 25)
            {
                Finish(false, $"timeout=True clicked={clicked}");
                return;
            }
            var menu = bootstrap.GetComponent<NetworkConnectionMenu>();
            if (menu == null)
            {
                return;
            }
            if (!clicked)
            {
                var fields = FindObjectsOfType<TMP_InputField>();
                var portField = fields.FirstOrDefault(field => field.name == "Port");
                var addressField = fields.FirstOrDefault(field => field.name == "Server Address");
                if (portField == null || addressField == null)
                {
                    return;
                }
                if (role == "Host")
                {
                    portField.text = port.ToString();
                    Button("Host Button").onClick.Invoke();
                }
                else
                {
                    if (discoveryJoin)
                    {
                        var row = FindObjectsOfType<Button>().FirstOrDefault(button => button.interactable
                            && button.name.StartsWith("Room Row ", StringComparison.Ordinal)
                            && button.GetComponentInChildren<TextMeshProUGUI>().text.Contains(":" + port + "\n"));
                        if (row == null)
                        {
                            return;
                        }
                        if (Array.Exists(Environment.GetCommandLineArgs(), value => value == "-afpsRoomMenuCapture"))
                        {
                            PracticeSmokeTestDriver.CaptureCameras(
                                Path.Combine(Path.GetDirectoryName(codePath), "rooms-client.png"), true);
                        }
                        // 此分支不读取共享文件、不填写目标地址，必须真正点击UDP发现得到的房间。
                        row.onClick.Invoke();
                        discovered = bootstrap.Runtime != null;
                    }
                    else
                    {
                        if (!File.Exists(codePath))
                        {
                            return;
                        }
                        addressField.text = "AF1-R2M0-250Y-C4PRC";
                        Button("Join Button").onClick.Invoke();
                        invalidRejected = bootstrap.Runtime == null;
                        addressField.text = File.ReadAllText(codePath);
                        // 故意填写另一个有效端口，证明菜单确实使用房间码内的端口。
                        portField.text = "1";
                        Button("Join Button").onClick.Invoke();
                    }
                }
                clicked = true;
            }
            if (role == "Host" && !codePublished && !string.IsNullOrEmpty(menu.CurrentRoomCode))
            {
                string previousClipboard = GUIUtility.systemCopyBuffer;
                bool copied;
                try
                {
                    Button("Copy Room Code Button").onClick.Invoke();
                    copied = GUIUtility.systemCopyBuffer == menu.CurrentRoomCode;
                }
                finally
                {
                    GUIUtility.systemCopyBuffer = previousClipboard;
                }
                if (!copied || !RoomCode.TryDecode(menu.CurrentRoomCode, out _, out ushort decoded, out _)
                    || decoded != port)
                {
                    Finish(false, "copyOrPortMismatch=True");
                    return;
                }
                // 先写临时文件再原子改名，客户端不能读到房间码写入一半的中间态。
                File.WriteAllText(codePath + ".tmp", menu.CurrentRoomCode);
                File.Move(codePath + ".tmp", codePath);
                codePublished = true;
            }
            var match = movement.SessionManager?.ClientDeathmatchState;
            if (!movement.CanEnterGame || match == null || match.OnlinePlayerCount != 2)
            {
                return;
            }
            var enter = Button("Enter Button");
            var manager = movement.SessionManager;
            if (!spawnConfirmed && manager.TryGetLocalLobbyPlayer(out var spawnPlayer))
            {
                if (spawnRequested && !manager.LobbyReadyPending)
                {
                    spawnConfirmed = !manager.SpawnSelectionDeclined && spawnPlayer.SpawnSlot != originalSpawn;
                    spawnRequested = false;
                }
                if (!spawnConfirmed)
                {
                    var nextSpawn = Button("Next Spawn Button");
                    if (!spawnRequested && nextSpawn != null && nextSpawn.interactable)
                    {
                        originalSpawn = spawnPlayer.SpawnSlot;
                        nextSpawn.onClick.Invoke();
                        spawnRequested = manager.LobbyReadyPending;
                    }
                    return;
                }
            }
            // 菜单可能比测试驱动晚一帧刷新，等实际文字更新后再截图和取消准备。
            bool countdownVisible = FindObjectsOfType<TextMeshProUGUI>()
                .Any(text => text.text.Contains("秒后开局"))
                && enter != null
                && enter.GetComponentInChildren<TextMeshProUGUI>().text == "取消准备";
            if (manager.ClientLobby?.Phase == LobbyPhase.Countdown)
            {
                sawCountdown = true;
                restartedCountdown |= cancelConfirmed;
                if (!capturedCountdown && countdownVisible
                    && Array.Exists(Environment.GetCommandLineArgs(), value => value == "-afpsRoomMenuCapture"))
                {
                    PracticeSmokeTestDriver.CaptureCameras(
                        Path.Combine(Path.GetDirectoryName(codePath), "countdown-" + role.ToLowerInvariant() + ".png"), true);
                    capturedCountdown = true;
                }
            }
            if (cancelMode && role == "Host" && manager.TryGetLocalLobbyPlayer(out var current))
            {
                if (!canceled && manager.ClientLobby.Phase == LobbyPhase.Countdown
                    && countdownVisible && current.Ready && !manager.LobbyReadyPending
                    && enter != null && enter.interactable)
                {
                    enter.onClick.Invoke();
                    canceled = true;
                    return;
                }
                if (canceled && !cancelConfirmed && manager.ClientLobby.Phase == LobbyPhase.Waiting
                    && !current.Ready && !manager.LobbyReadyPending)
                {
                    cancelConfirmed = true;
                    reReadyAt = Time.realtimeSinceStartupAsDouble + 0.5;
                }
                if (canceled && (!cancelConfirmed || Time.realtimeSinceStartupAsDouble < reReadyAt))
                {
                    return;
                }
            }
            if (!manager.CanEnterMatch)
            {
                if (input != null && input.enabled)
                {
                    Finish(false, "inputEnabledBeforePlaying=True");
                    return;
                }
                if (manager.TryGetLocalLobbyPlayer(out var local) && !local.Ready && !manager.LobbyReadyPending
                    && enter != null && enter.interactable)
                {
                    enter.onClick.Invoke();
                }
                return;
            }
            bool chineseCount = FindObjectsOfType<TextMeshProUGUI>()
                .Any(text => text.text.Contains("在线玩家：2") && text.text.Contains("暂离玩家：0"));
            if (enter == null || !chineseCount)
            {
                return;
            }
            var combatHud = FindObjectsOfType<Canvas>(true).FirstOrDefault(canvas => canvas.name == "Deathmatch HUD");
            if (combatHud == null || combatHud.enabled)
            {
                return;
            }
            bool capture = Array.Exists(Environment.GetCommandLineArgs(),
                argument => argument == "-afpsRoomMenuCapture");
            if (capture)
            {
                PracticeSmokeTestDriver.CaptureCameras(
                    Path.Combine(Path.GetDirectoryName(codePath), "menu-" + role.ToLowerInvariant() + ".png"), true);
            }
            enter.onClick.Invoke();
            bool passed = input != null && input.enabled && match.PlayerCount == 2
                && sawCountdown && spawnConfirmed
                && (role == "Host" ? codePublished : discoveryJoin ? discovered : invalidRejected)
                && (!cancelMode || role != "Host" || (cancelConfirmed && restartedCountdown));
            Finish(passed, $"role={role} codeJoin=True online={match.OnlinePlayerCount}"
                + $" total={match.PlayerCount} entered={input != null && input.enabled} invalidRejected={invalidRejected}"
                + $" discovered={discovered} countdown={sawCountdown} canceled={cancelConfirmed}"
                + $" spawnSelected={spawnConfirmed}"
                + $" restarted={restartedCountdown} hudHidden=True");
        }

        private static Button Button(string name)
        {
            return FindObjectsOfType<Button>().FirstOrDefault(button => button.name == name);
        }

        private void Finish(bool passed, string details)
        {
            enabled = false;
            Debug.Log($"[AFPS_ROOMMENU_RESULT] {(passed ? "PASS" : "FAIL")} {details}");
            SmokeTestExit.Quit(this, passed ? 0 : 2);
        }
    }
}

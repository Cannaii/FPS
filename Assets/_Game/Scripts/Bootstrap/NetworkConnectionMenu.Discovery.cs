using System.Collections.Generic;
using AFPS.NetCode.Runtime;
using AFPS.Simulation.World;
using AFPS.Transport.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AFPS.Bootstrap
{
    public sealed partial class NetworkConnectionMenu
    {
        private const int RoomsPerPage = 4;
        private readonly List<LanRoomEntry> listedRooms = new List<LanRoomEntry>();
        private readonly LanRoomEntry[] rowEntries = new LanRoomEntry[RoomsPerPage];
        private readonly Button[] roomButtons = new Button[RoomsPerPage];
        private RectTransform mainPanel;
        private RectTransform discoveryPanel;
        private TextMeshProUGUI discoveryStatus;
        private Button refreshRoomsButton;
        private Button previousRoomsButton;
        private Button nextRoomsButton;
        private LanRoomDiscovery roomBrowser;
        private SimulationWorldIdentity? browserIdentity;
        private int roomPage;
        private double nextListUpdate;

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
                new Vector2(0f, 260f), new Vector2(540f, 50f), () =>
                {
                    roomBrowser?.Refresh();
                    if (roomBrowser != null && !roomBrowser.IsRunning)
                    {
                        roomBrowser.TryStartBrowser(out _);
                    }
                    roomPage = 0;
                    nextListUpdate = 0;
                });
            discoveryStatus = CreateText(discoveryPanel, "正在搜索…", 18f, FontStyles.Normal,
                TextAlignmentOptions.Center, new Vector2(0f, 205f), new Vector2(540f, 42f), Color.white);
            for (int index = 0; index < roomButtons.Length; index++)
            {
                int row = index;
                roomButtons[index] = CreateButton(discoveryPanel, "Room Row " + index, "",
                    new Vector2(0f, 120f - 145f * index), new Vector2(540f, 120f), () => JoinListedRoom(row));
                roomButtons[index].gameObject.SetActive(false);
            }
            previousRoomsButton = CreateButton(discoveryPanel, "Previous Rooms Button", "上一页",
                new Vector2(-140f, -410f), new Vector2(260f, 36f), () =>
                {
                    roomPage--;
                    nextListUpdate = 0;
                });
            nextRoomsButton = CreateButton(discoveryPanel, "Next Rooms Button", "下一页",
                new Vector2(140f, -410f), new Vector2(260f, 36f), () =>
                {
                    roomPage++;
                    nextListUpdate = 0;
                });
        }

        private void StartBrowsing()
        {
            var movement = bootstrap.GetComponent<UnityNetworkMovementController>();
            if (movement != null && movement.TryPrepareSimulation(out _))
            {
                browserIdentity = movement.PreparedWorldIdentity;
            }
            roomBrowser = new LanRoomDiscovery();
            if (!roomBrowser.TryStartBrowser(out string error))
            {
                discoveryStatus.text = error;
            }
        }

        private void UpdateBrowsing()
        {
            if (roomBrowser == null)
            {
                return;
            }
            double now = Time.realtimeSinceStartupAsDouble;
            roomBrowser.PumpBrowser(now);
            if (now < nextListUpdate)
            {
                return;
            }
            nextListUpdate = now + 0.25; // 秒，列表UI最多4Hz刷新；发现收包仍每帧有界处理。
            roomBrowser.Rooms.CopyTo(listedRooms);
            roomPage = Mathf.Clamp(roomPage, 0, Mathf.Max(0, (listedRooms.Count - 1) / RoomsPerPage));
            for (int index = 0; index < roomButtons.Length; index++)
            {
                int roomIndex = roomPage * RoomsPerPage + index;
                bool visible = roomIndex < listedRooms.Count;
                roomButtons[index].gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }
                var room = listedRooms[roomIndex];
                rowEntries[index] = room;
                var advertisement = room.Advertisement;
                bool compatible = browserIdentity.HasValue && advertisement.IsCompatible(browserIdentity.Value);
                bool full = advertisement.OnlinePlayers >= advertisement.Capacity;
                string state = !compatible ? "地图 / 配置 / 协议不匹配"
                    : full ? "房间已满" : "点击加入";
                string phase = advertisement.Phase == AFPS.NetCode.Match.DeathmatchPhase.WaitingForPlayers
                    ? "等待玩家" : advertisement.Phase == AFPS.NetCode.Match.DeathmatchPhase.Playing
                        ? "对局中" : "本局结束";
                roomButtons[index].GetComponentInChildren<TextMeshProUGUI>().text =
                    $"{room.Address}:{advertisement.Port}\n在线 {advertisement.OnlinePlayers} / {advertisement.Capacity} · {phase}\n{state}";
                roomButtons[index].interactable = compatible && !full;
            }
            previousRoomsButton.interactable = roomPage > 0;
            nextRoomsButton.interactable = (roomPage + 1) * RoomsPerPage < listedRooms.Count;
            discoveryStatus.text = listedRooms.Count > 0
                ? $"发现 {listedRooms.Count} 个房间 · 第 {roomPage + 1} 页"
                : !string.IsNullOrEmpty(roomBrowser.LastError) ? "搜索不可用，可使用房间码或地址加入。"
                : "尚未发现房间，可刷新或使用房间码加入。";
        }

        private void JoinListedRoom(int row)
        {
            if (roomBrowser == null || bootstrap.Runtime != null || !browserIdentity.HasValue)
            {
                return;
            }
            var selected = rowEntries[row];
            roomBrowser.Rooms.Advance(Time.realtimeSinceStartupAsDouble);
            roomBrowser.Rooms.CopyTo(listedRooms);
            // 列表可能过期或重新排序；核对按钮展示的端点及实例，不能按旧行号加入另一间房。
            foreach (var room in listedRooms)
            {
                if (room.Address == selected.Address && room.Advertisement.Port == selected.Advertisement.Port
                    && room.Advertisement.InstanceId == selected.Advertisement.InstanceId
                    && room.Advertisement.IsCompatible(browserIdentity.Value)
                    && room.Advertisement.OnlinePlayers < room.Advertisement.Capacity
                    && RoomCode.TryEncode(room.Address, room.Advertisement.Port, out string code, out _))
                {
                    addressInput.text = code;
                    portInput.text = room.Advertisement.Port.ToString();
                    StartNetwork(NetworkLaunchMode.Client);
                    return;
                }
            }
            SetStatus("房间信息已变化，请刷新后重新选择。", true);
        }

        private void StopBrowsing()
        {
            roomBrowser?.Dispose();
            roomBrowser = null;
            listedRooms.Clear();
        }

        private void OnApplicationQuit() => StopBrowsing();

        private void OnDestroy()
        {
            StopBrowsing();
            if (canvasRoot != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(canvasRoot);
                }
                else
                {
                    DestroyImmediate(canvasRoot);
                }
            }
        }
    }
}

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AFPS.Bootstrap
{
    /// <summary>大厅的可编辑资源与控件引用；不持有网络状态或运行时布局生成逻辑。</summary>
    public sealed class NetworkLobbyView : MonoBehaviour
    {
        public const string ResourcePath = "UI/NetworkLobby";

        [SerializeField] private TMP_InputField addressInput;
        [SerializeField] private TMP_InputField portInput;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI roomCodeText;
        [SerializeField] private TextMeshProUGUI roomPlayersText;
        [SerializeField] private Button copyRoomCodeButton;
        [SerializeField] private Button hostButton;
        [SerializeField] private Button joinButton;
        [SerializeField] private Button practiceButton;
        [SerializeField] private Button enterButton;
        [SerializeField] private Button previousSpawnButton;
        [SerializeField] private Button nextSpawnButton;
        [SerializeField] private TextMeshProUGUI spawnText;
        [SerializeField] private RectTransform mainPanel;
        [SerializeField] private RectTransform discoveryPanel;
        [SerializeField] private TextMeshProUGUI discoveryStatus;
        [SerializeField] private Button refreshRoomsButton;
        [SerializeField] private Button previousRoomsButton;
        [SerializeField] private Button nextRoomsButton;
        [SerializeField] private Button[] roomButtons;

        public TMP_InputField AddressInput => addressInput;
        public TMP_InputField PortInput => portInput;
        public TextMeshProUGUI StatusText => statusText;
        public TextMeshProUGUI RoomCodeText => roomCodeText;
        public TextMeshProUGUI RoomPlayersText => roomPlayersText;
        public Button CopyRoomCodeButton => copyRoomCodeButton;
        public Button HostButton => hostButton;
        public Button JoinButton => joinButton;
        public Button PracticeButton => practiceButton;
        public Button EnterButton => enterButton;
        public Button PreviousSpawnButton => previousSpawnButton;
        public Button NextSpawnButton => nextSpawnButton;
        public TextMeshProUGUI SpawnText => spawnText;
        public RectTransform MainPanel => mainPanel;
        public RectTransform DiscoveryPanel => discoveryPanel;
        public TextMeshProUGUI DiscoveryStatus => discoveryStatus;
        public Button RefreshRoomsButton => refreshRoomsButton;
        public Button PreviousRoomsButton => previousRoomsButton;
        public Button NextRoomsButton => nextRoomsButton;
        public Button[] RoomButtons => roomButtons;

        public bool TryValidate(out string error)
        {
            if (addressInput == null)
            {
                error = "大厅 Prefab 缺少引用：addressInput";
                return false;
            }

            if (portInput == null)
            {
                error = "大厅 Prefab 缺少引用：portInput";
                return false;
            }

            if (statusText == null)
            {
                error = "大厅 Prefab 缺少引用：statusText";
                return false;
            }

            if (roomCodeText == null)
            {
                error = "大厅 Prefab 缺少引用：roomCodeText";
                return false;
            }

            if (roomPlayersText == null)
            {
                error = "大厅 Prefab 缺少引用：roomPlayersText";
                return false;
            }

            if (copyRoomCodeButton == null)
            {
                error = "大厅 Prefab 缺少引用：copyRoomCodeButton";
                return false;
            }

            if (hostButton == null)
            {
                error = "大厅 Prefab 缺少引用：hostButton";
                return false;
            }

            if (joinButton == null)
            {
                error = "大厅 Prefab 缺少引用：joinButton";
                return false;
            }

            if (practiceButton == null)
            {
                error = "大厅 Prefab 缺少引用：practiceButton";
                return false;
            }

            if (enterButton == null)
            {
                error = "大厅 Prefab 缺少引用：enterButton";
                return false;
            }

            if (previousSpawnButton == null)
            {
                error = "大厅 Prefab 缺少引用：previousSpawnButton";
                return false;
            }

            if (nextSpawnButton == null)
            {
                error = "大厅 Prefab 缺少引用：nextSpawnButton";
                return false;
            }

            if (spawnText == null)
            {
                error = "大厅 Prefab 缺少引用：spawnText";
                return false;
            }

            if (mainPanel == null)
            {
                error = "大厅 Prefab 缺少引用：mainPanel";
                return false;
            }

            if (discoveryPanel == null)
            {
                error = "大厅 Prefab 缺少引用：discoveryPanel";
                return false;
            }

            if (discoveryStatus == null)
            {
                error = "大厅 Prefab 缺少引用：discoveryStatus";
                return false;
            }

            if (refreshRoomsButton == null)
            {
                error = "大厅 Prefab 缺少引用：refreshRoomsButton";
                return false;
            }

            if (previousRoomsButton == null)
            {
                error = "大厅 Prefab 缺少引用：previousRoomsButton";
                return false;
            }

            if (nextRoomsButton == null)
            {
                error = "大厅 Prefab 缺少引用：nextRoomsButton";
                return false;
            }

            // 发现列表与现有分页协议保持四行；美术可编辑每一行，不能静默丢失绑定。
            if (roomButtons == null || roomButtons.Length != 4)
            {
                error = "大厅 Prefab 必须绑定四个房间行按钮。";
                return false;
            }

            for (int index = 0; index < roomButtons.Length; index++)
            {
                if (roomButtons[index] == null)
                {
                    error = "大厅 Prefab 缺少房间行：" + index;
                    return false;
                }
            }

            error = null;
            return true;
        }
    }
}

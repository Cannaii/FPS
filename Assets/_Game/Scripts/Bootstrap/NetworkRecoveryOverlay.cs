using AFPS.Input;
using AFPS.NetCode.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace AFPS.Bootstrap
{
    /// <summary>独立的重连覆盖层，避免与战斗HUD或已销毁的入局菜单混用。</summary>
    public sealed class NetworkRecoveryOverlay : MonoBehaviour
    {
        private UnityNetworkBootstrap bootstrap;
        private Canvas canvas;
        private TextMeshProUGUI message;
        private Button retry;
        private Button cancel;
        private LocalPlayerInputCollector input;
        private bool showing;
        private bool inputWasEnabled;
        private bool cursorWasVisible;
        private CursorLockMode cursorLock;

        public void Configure(UnityNetworkBootstrap owner)
        {
            bootstrap = owner;
            input = FindObjectOfType<LocalPlayerInputCollector>(true);
            if (EventSystem.current == null)
            {
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            }
            var root = new GameObject("Reconnect UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1200;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var backdrop = CreateRect(root.transform, "Backdrop", Vector2.zero, Vector2.zero);
            backdrop.anchorMin = Vector2.zero;
            backdrop.anchorMax = Vector2.one;
            backdrop.gameObject.AddComponent<Image>().color = new Color(0.02f, 0.03f, 0.05f, 0.80f);
            var panel = CreateRect(backdrop, "Panel", new Vector2(760, 330), Vector2.zero);
            panel.gameObject.AddComponent<Image>().color = new Color(0.075f, 0.095f, 0.135f, 0.99f);
            CreateLabel(panel, "连接恢复", new Vector2(0, 112), new Vector2(680, 50), 32);
            message = CreateLabel(panel, string.Empty, new Vector2(0, 42), new Vector2(680, 82), 22);
            retry = CreateButton(panel, "重试原会话", new Vector2(-240, -62),
                () => bootstrap.Reconnect.Retry(Time.realtimeSinceStartupAsDouble));
            cancel = CreateButton(panel, "取消重连", new Vector2(0, -62),
                () => bootstrap.Reconnect.Cancel());
            CreateButton(panel, "作为新玩家加入", new Vector2(240, -62),
                () => bootstrap.Reconnect.JoinAsNewPlayer(Time.realtimeSinceStartupAsDouble));
            CreateLabel(panel, "重新加入会创建新角色；原角色最多保留约30秒。", new Vector2(0, -123), new Vector2(690, 40), 18);
            canvas.enabled = false;
        }

        private void Update()
        {
            if (bootstrap?.Reconnect == null || canvas == null)
            {
                return;
            }
            var reconnect = bootstrap.Reconnect;
            bool visible = reconnect.Status != ClientReconnectStatus.Idle;
            if (visible != showing)
            {
                showing = visible;
                canvas.enabled = visible;
                if (visible)
                {
                    inputWasEnabled = input != null && input.enabled;
                    cursorWasVisible = Cursor.visible;
                    cursorLock = Cursor.lockState;
                    if (input != null)
                    {
                        input.enabled = false;
                    }
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                else
                {
                    if (input != null && inputWasEnabled)
                    {
                        input.enabled = true;
                    }
                    Cursor.lockState = cursorLock;
                    Cursor.visible = cursorWasVisible;
                }
            }
            message.text = reconnect.Message;
            retry.interactable = !reconnect.IsActive;
            cancel.interactable = reconnect.IsActive;
        }

        private static RectTransform CreateRect(Transform parent, string name, Vector2 size, Vector2 position)
        {
            var root = new GameObject(name, typeof(RectTransform));
            var rect = root.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = rect.anchorMin;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private static TextMeshProUGUI CreateLabel(Transform parent, string text, Vector2 position, Vector2 size, int fontSize)
        {
            var label = CreateRect(parent, "Text", size, position).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = ChineseUiFont.Asset;
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            return label;
        }

        private static Button CreateButton(Transform parent, string text, Vector2 position, UnityEngine.Events.UnityAction clicked)
        {
            var rect = CreateRect(parent, "Button", new Vector2(220, 54), position);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.12f, 0.60f, 0.82f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(clicked);
            CreateLabel(rect, text, Vector2.zero, new Vector2(210, 48), 20);
            return button;
        }
    }
}

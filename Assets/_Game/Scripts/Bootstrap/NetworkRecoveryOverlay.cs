using AFPS.Input;
using AFPS.NetCode.Runtime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace AFPS.Bootstrap
{
    /// <summary>重连覆盖层协调会话操作与输入焦点，布局由Prefab持有。</summary>
    public sealed class NetworkRecoveryOverlay : MonoBehaviour
    {
        private UnityNetworkBootstrap bootstrap;
        private NetworkRecoveryView view;
        private LocalPlayerInputCollector input;
        private bool showing;
        private bool inputWasEnabled;
        private bool cursorWasVisible;
        private CursorLockMode cursorLock;

        public void Configure(UnityNetworkBootstrap owner)
        {
            if (bootstrap != owner)
            {
                SetVisible(false);
            }

            bootstrap = owner;
            input = FindObjectOfType<LocalPlayerInputCollector>(true);
            if (view != null)
            {
                return;
            }

            var prefab = Resources.Load<NetworkRecoveryView>(NetworkRecoveryView.ResourcePath);
            if (prefab == null)
            {
                throw new System.InvalidOperationException("缺少 Resources/UI/NetworkRecovery.prefab。");
            }

            if (!prefab.TryValidate(out string error))
            {
                throw new System.InvalidOperationException(error);
            }

            if (EventSystem.current == null)
            {
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            }

            view = Instantiate(prefab, transform, false);
            view.name = "Reconnect UI";
            view.Retry.onClick.AddListener(RetrySession);
            view.Cancel.onClick.AddListener(CancelReconnect);
            view.JoinAsNew.onClick.AddListener(JoinAsNewPlayer);
            view.Canvas.enabled = false;
        }

        private void Update()
        {
            if (bootstrap?.Reconnect == null || view == null)
            {
                SetVisible(false);
                return;
            }

            var reconnect = bootstrap.Reconnect;
            RenderState(reconnect.Status, reconnect.IsActive, reconnect.Message);
        }

        private void RenderState(ClientReconnectStatus status, bool active, string message)
        {
            SetVisible(status != ClientReconnectStatus.Idle);
            view.Message.text = message;
            view.Retry.interactable = !active;
            view.Cancel.interactable = active;
        }

        private void SetVisible(bool visible)
        {
            if (view != null)
            {
                view.Canvas.enabled = visible;
            }

            if (showing == visible)
            {
                return;
            }

            showing = visible;
            if (visible)
            {
                // 只在进入覆盖层时借用焦点，持续重连不能反复覆盖原输入/鼠标快照。
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

        private void RetrySession()
        {
            bootstrap?.Reconnect?.Retry(Time.realtimeSinceStartupAsDouble);
        }

        private void CancelReconnect()
        {
            bootstrap?.Reconnect?.Cancel();
        }

        private void JoinAsNewPlayer()
        {
            bootstrap?.Reconnect?.JoinAsNewPlayer(Time.realtimeSinceStartupAsDouble);
        }

        private void OnDisable()
        {
            SetVisible(false);
        }

        private void OnDestroy()
        {
            // 销毁时也归还借用的输入焦点，避免会话退出后仍被旧覆盖层锁住。
            SetVisible(false);
            if (view == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(view.gameObject);
            }
            else
            {
                DestroyImmediate(view.gameObject);
            }
        }
    }
}

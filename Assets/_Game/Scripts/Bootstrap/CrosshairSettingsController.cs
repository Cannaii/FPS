using AFPS.Input;
using AFPS.NetCode.Runtime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace AFPS.Bootstrap
{
    /// <summary>F2独立设置窗口；先于玩家输入采集更新，打开窗口时不能把拖滑条当作射击。</summary>
    [DefaultExecutionOrder(-900)]
    public sealed class CrosshairSettingsController : MonoBehaviour
    {
        private CrosshairSettingsView view;
        private LocalPlayerInputCollector input;
        private bool inputWasEnabled;
        private bool cursorWasVisible;
        private CursorLockMode cursorLock;
        private CrosshairAppearance original;
        private CrosshairAppearance draft;
        public bool IsVisible => view != null;

        private void Update()
        {
            var network = GetComponent<UnityNetworkBootstrap>();
            if (network != null && network.Reconnect != null && network.Reconnect.Status != ClientReconnectStatus.Idle)
            {
                // 此更新早于恢复覆盖层，先归还焦点，让恢复覆盖层记录正确的输入状态。
                Close(false);
                return;
            }
            if (Keyboard.current != null && Keyboard.current.f2Key.wasPressedThisFrame)
            {
                if (IsVisible)
                {
                    Close(true);
                }
                else
                {
                    Open();
                }
            }
        }

        public void Open()
        {
            if (view != null)
            {
                return;
            }
            var prefab = Resources.Load<CrosshairSettingsView>(CrosshairSettingsView.ResourcePath);
            if (prefab == null || !prefab.IsConfigured)
            {
                throw new System.InvalidOperationException("缺少完整的UI/CrosshairSettings.prefab。");
            }
            if (EventSystem.current == null)
            {
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            }
            original = CrosshairPreferences.Current;
            draft = original;
            input = FindObjectOfType<LocalPlayerInputCollector>(true);
            inputWasEnabled = input != null && input.enabled;
            cursorWasVisible = Cursor.visible;
            cursorLock = Cursor.lockState;
            if (input != null)
            {
                input.DiscardPendingActions();
                input.enabled = false;
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            view = Instantiate(prefab, transform, false);
            for (int index = 0; index < view.Sliders.Length; index++)
            {
                view.Sliders[index].onValueChanged.AddListener(_ => ReadControls());
            }
            view.Style.onClick.AddListener(() =>
            {
                draft.Style = (CrosshairStyle)(((int)draft.Style + 1) % 4);
                FillControls();
            });
            view.Apply.onClick.AddListener(() => Close(true));
            view.Cancel.onClick.AddListener(() => Close(false));
            view.Reset.onClick.AddListener(() =>
            {
                draft = CrosshairAppearance.Default;
                FillControls();
            });
            FillControls();
        }

        private void FillControls()
        {
            float[] numbers = { draft.Size, draft.Thickness, draft.Gap, draft.Opacity,
                draft.Color.r, draft.Color.g, draft.Color.b, draft.Outline };
            for (int index = 0; index < numbers.Length; index++)
            {
                view.Sliders[index].SetValueWithoutNotify(numbers[index]);
            }
            RefreshPreview();
        }

        private void ReadControls()
        {
            draft.Size = view.Sliders[0].value;
            draft.Thickness = view.Sliders[1].value;
            draft.Gap = view.Sliders[2].value;
            draft.Opacity = view.Sliders[3].value;
            draft.Color = new Color(view.Sliders[4].value, view.Sliders[5].value, view.Sliders[6].value, 1f);
            draft.Outline = view.Sliders[7].value;
            draft = draft.Validated();
            FillControls();
        }

        private void RefreshPreview()
        {
            string[] styles = { "十字", "圆点", "圆环", "十字 + 圆点" };
            view.StyleLabel.text = "样式：" + styles[(int)draft.Style] + "（点击切换）";
            for (int index = 0; index < view.Sliders.Length; index++)
            {
                view.Values[index].text = index >= 3 && index <= 6
                    ? (view.Sliders[index].value * 100f).ToString("0") + "%"
                    : view.Sliders[index].value.ToString("0.#") + " px";
            }
            view.Preview.Appearance = draft;
            CrosshairPreferences.Apply(draft, false);
        }

        public void Close(bool save)
        {
            if (view == null)
            {
                return;
            }
            CrosshairPreferences.Apply(save ? draft : original, save);
            // 先停用射线拦截，再销毁；F2关闭后同一帧不能遗留透明覆盖层。
            view.gameObject.SetActive(false);
            if (Application.isPlaying)
            {
                Destroy(view.gameObject);
            }
            else
            {
                DestroyImmediate(view.gameObject);
            }
            view = null;
            if (input != null && inputWasEnabled)
            {
                input.DiscardPendingActions();
                input.enabled = true;
            }
            Cursor.lockState = cursorLock;
            Cursor.visible = cursorWasVisible;
        }

        private void OnDisable()
        {
            Close(false);
        }

        private void OnDestroy()
        {
            Close(false);
        }
    }
}

using UnityEngine;
using UnityEngine.UI;

namespace cowsins.Settings.UI
{
    // UI Controller for toggle settings
    public class ToggleSettingUI : SettingUIController
    {
        [SerializeField] private Toggle toggle;
        private ISetting<bool> setting;

        public void Bind(ISetting<bool> setting)
        {
            this.setting = setting;
            Initialize();
        }

        public override void Initialize()
        {
            if (setting == null || toggle == null) return;

            toggle.isOn = setting.Value;
            toggle.onValueChanged.AddListener(OnToggleChanged);
            setting.OnValueChanged += OnSettingChanged;
        }

        public override void Cleanup()
        {
            if (toggle != null)
                toggle.onValueChanged.RemoveListener(OnToggleChanged);
            if (setting != null)
                setting.OnValueChanged -= OnSettingChanged;
        }

        private void OnToggleChanged(bool value)
        {
            setting.Value = value;
        }

        private void OnSettingChanged(bool value)
        {
            if (toggle.isOn != value)
                toggle.isOn = value;
        }

        private void OnDestroy() => Cleanup();
    }
}
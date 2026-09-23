using UnityEngine;
using TMPro;

namespace cowsins.Settings.UI
{
    // UI Controller for dropdown settings
    public class DropdownSettingUI : SettingUIController
    {
        [SerializeField] private TMP_Dropdown dropdown;
        private ISetting<int> setting;

        public void Bind(ISetting<int> setting)
        {
            this.setting = setting;
            Initialize();
        }

        public override void Initialize()
        {
            if (setting == null || dropdown == null) return;

            dropdown.value = setting.Value;
            dropdown.onValueChanged.AddListener(OnDropdownChanged);
            setting.OnValueChanged += OnSettingChanged;
        }

        public override void Cleanup()
        {
            if (dropdown != null)
                dropdown.onValueChanged.RemoveListener(OnDropdownChanged);
            if (setting != null)
                setting.OnValueChanged -= OnSettingChanged;
        }

        private void OnDropdownChanged(int value)
        {
            setting.Value = value;
        }

        private void OnSettingChanged(int value)
        {
            if (dropdown.value != value)
                dropdown.value = value;
        }

        private void OnDestroy() => Cleanup();
    }
}
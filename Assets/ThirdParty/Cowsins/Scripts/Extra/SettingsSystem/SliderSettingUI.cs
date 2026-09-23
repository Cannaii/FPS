using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace cowsins.Settings.UI
{
    public class SliderSettingUI : SettingUIController
    {
        [SerializeField] private Slider slider;
        [SerializeField] private TextMeshProUGUI valueText;
        [SerializeField] private string format = "F1";

        private ISetting<float> setting;

        public void Bind(ISetting<float> setting)
        {
            this.setting = setting;
            Initialize();
        }

        public override void Initialize()
        {
            if (setting == null || slider == null) return;

            slider.value = setting.Value;
            UpdateValueText(setting.Value);

            slider.onValueChanged.AddListener(OnSliderChanged);
            setting.OnValueChanged += OnSettingChanged;
        }

        public override void Cleanup()
        {
            if (slider != null)
                slider.onValueChanged.RemoveListener(OnSliderChanged);
            if (setting != null)
                setting.OnValueChanged -= OnSettingChanged;
        }

        private void OnSliderChanged(float value)
        {
            setting.Value = value;
            UpdateValueText(value);
        }

        private void OnSettingChanged(float value)
        {
            if (!Mathf.Approximately(slider.value, value))
                slider.value = value;
            UpdateValueText(value);
        }

        private void UpdateValueText(float value)
        {
            if (valueText != null)
                valueText.text = value.ToString(format);
        }

        private void OnDestroy() => Cleanup();
    }
}
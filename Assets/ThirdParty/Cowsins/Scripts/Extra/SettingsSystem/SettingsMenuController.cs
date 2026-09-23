using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

namespace cowsins.Settings.UI
{
    // Main settings menu controller
    public class SettingsMenuController : MonoBehaviour
    {
        [Header("Graphics")]
        [SerializeField] private ToggleSettingUI fullscreenToggle;
        [SerializeField] private TMP_Dropdown resolutionDropdown;
        [SerializeField] private DropdownSettingUI frameRateDropdown;
        [SerializeField] private ToggleSettingUI vsyncToggle;
        [SerializeField] private DropdownSettingUI qualityDropdown;

        [Header("Audio")]
        [SerializeField] private SliderSettingUI masterVolumeSlider;

        [Header("Controls")]
        [SerializeField] private SliderSettingUI mouseSensXSlider;
        [SerializeField] private SliderSettingUI mouseSensYSlider;
        [SerializeField] private SliderSettingUI controllerSensXSlider;
        [SerializeField] private SliderSettingUI controllerSensYSlider;
        [SerializeField] private ToggleSettingUI invertYToggle;
        [SerializeField] private ToggleSettingUI invertYControllerToggle;

        [Header("Buttons")]
        [SerializeField] private Button resetButton;

        private GameSettingsManager settingsManager;

        private void Start()
        {
            settingsManager = GameSettingsManager.Instance;
            if (settingsManager == null)
            {
                Debug.LogError("GameSettingsManager instance not found!");
                return;
            }

            InitializeUI();
            BindSettings();
            SetupButtons();
        }

        private void InitializeUI()
        {
            PopulateResolutionDropdown();
        }

        private void BindSettings()
        {
            // Graphics
            fullscreenToggle?.Bind(settingsManager.Graphics.Fullscreen);
            frameRateDropdown?.Bind(settingsManager.Graphics.FrameRate);
            vsyncToggle?.Bind(settingsManager.Graphics.VSync);
            qualityDropdown?.Bind(settingsManager.Graphics.Quality);

            // Resolution is a special case
            if (resolutionDropdown != null)
            {
                resolutionDropdown.value = settingsManager.Graphics.Resolution.Value;
                resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
            }

            // Audio
            masterVolumeSlider?.Bind(settingsManager.Audio.MasterVolume);

            // Controls
            mouseSensXSlider?.Bind(settingsManager.Controls.MouseSensitivityX);
            mouseSensYSlider?.Bind(settingsManager.Controls.MouseSensitivityY);
            controllerSensXSlider?.Bind(settingsManager.Controls.ControllerSensitivityX);
            controllerSensYSlider?.Bind(settingsManager.Controls.ControllerSensitivityY);
            invertYToggle?.Bind(settingsManager.Controls.InvertY);
            invertYControllerToggle?.Bind(settingsManager.Controls.InvertYController);
        }

        private void PopulateResolutionDropdown()
        {
            if (resolutionDropdown == null) return;

            resolutionDropdown.ClearOptions();
            Resolution[] resolutions = settingsManager.Graphics.Resolution.GetAvailableResolutions();

            List<string> options = new List<string>();
            foreach (Resolution res in resolutions)
            {
                // This avoids getting an annoying "Depcrated Warning" when using Unity 6 or above. newer Unity versions use refreshRateRatio not just refreshRate
#if UNITY_6000_0_OR_NEWER
                float refreshRate = (float)res.refreshRateRatio.numerator / res.refreshRateRatio.denominator;
                options.Add($"{res.width} x {res.height} @ {refreshRate:F2}Hz");
#else
                options.Add($"{res.width} x {res.height} @ {Mathf.Round(res.refreshRate)}Hz");
#endif
            }

            resolutionDropdown.AddOptions(options);
            resolutionDropdown.value = settingsManager.Graphics.Resolution.Value;
            resolutionDropdown.RefreshShownValue();
        }

        private void OnResolutionChanged(int value)
        {
            settingsManager.Graphics.Resolution.Value = value;
        }

        private void SetupButtons()
        {
            if (resetButton != null)
                resetButton.onClick.AddListener(OnResetClicked);
        }

        private void OnResetClicked()
        {
            settingsManager.ResetAllSettings();
            InputManager.ResetAllBindings();
        }

        private void OnDestroy()
        {
            if (resolutionDropdown != null)
                resolutionDropdown.onValueChanged.RemoveListener(OnResolutionChanged);

            if (resetButton != null)
                resetButton.onClick.RemoveListener(OnResetClicked);
        }
    }
}
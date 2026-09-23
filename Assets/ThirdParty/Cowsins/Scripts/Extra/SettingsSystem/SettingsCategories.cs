using UnityEngine;
using System.Collections.Generic;

namespace cowsins.Settings
{
    // Base class for organizing settings into categories
    public abstract class SettingsCategory
    {
        public abstract string CategoryName { get; }
        protected List<ISetting> settings = new List<ISetting>();

        public IReadOnlyList<ISetting> Settings => settings.AsReadOnly();

        public void SaveAll()
        {
            foreach (var setting in settings)
                setting.Save();
            PlayerPrefs.Save();
        }

        public void LoadAll()
        {
            foreach (var setting in settings)
                setting.Load();
        }

        public void ResetAll()
        {
            foreach (var setting in settings)
                setting.Reset();
        }

        public void ApplyAll()
        {
            foreach (var setting in settings)
                setting.Apply();
        }

        protected void RegisterSetting(ISetting setting)
        {
            settings.Add(setting);
        }
    }

    // CONCRETE CATEGORIES
    public class GraphicsSettings : SettingsCategory
    {
        public override string CategoryName => "Graphics";

        public BoolSetting Fullscreen { get; private set; }
        public ResolutionSetting Resolution { get; private set; }
        public FrameRateSetting FrameRate { get; private set; }
        public VSyncSetting VSync { get; private set; }
        public GraphicsQualitySetting Quality { get; private set; }

        public GraphicsSettings()
        {
            Fullscreen = new BoolSetting("fullScreen", true);
            Resolution = new ResolutionSetting("resolution", Screen.resolutions.Length - 1, Fullscreen);
            FrameRate = new FrameRateSetting("maxFrameRate", 0);
            VSync = new VSyncSetting("vsync", false);
            Quality = new GraphicsQualitySetting("graphicsQuality", 2);

            RegisterSetting(Fullscreen);
            RegisterSetting(Resolution);
            RegisterSetting(FrameRate);
            RegisterSetting(VSync);
            RegisterSetting(Quality);
        }
    }

    public class AudioSettings : SettingsCategory
    {
        public override string CategoryName => "Audio";

        public AudioVolumeSetting MasterVolume { get; private set; }

        public AudioSettings(UnityEngine.Audio.AudioMixer masterMixer)
        {
            MasterVolume = new AudioVolumeSetting("masterVolume", 1f, masterMixer, "Volume");
            RegisterSetting(MasterVolume);
        }
    }

    public class ControlSettings : SettingsCategory
    {
        public override string CategoryName => "Controls";

        public FloatSetting MouseSensitivityX { get; private set; }
        public FloatSetting MouseSensitivityY { get; private set; }
        public FloatSetting ControllerSensitivityX { get; private set; }
        public FloatSetting ControllerSensitivityY { get; private set; }
        public BoolSetting InvertY { get; private set; }
        public BoolSetting InvertYController { get; private set; }


        public ControlSettings()
        {
            MouseSensitivityX = new FloatSetting("playerSensX", 4f);
            MouseSensitivityY = new FloatSetting("playerSensY", 4f);
            ControllerSensitivityX = new FloatSetting("playerControllerSensX", 35f);
            ControllerSensitivityY = new FloatSetting("playerControllerSensY", 35f);
            InvertY = new BoolSetting("invertY", false);
            InvertYController = new BoolSetting("invertYController", false);

            RegisterSetting(MouseSensitivityX);
            RegisterSetting(MouseSensitivityY);
            RegisterSetting(ControllerSensitivityX);
            RegisterSetting(ControllerSensitivityY);
            RegisterSetting(InvertY);
            RegisterSetting(InvertYController);
        }
    }
}
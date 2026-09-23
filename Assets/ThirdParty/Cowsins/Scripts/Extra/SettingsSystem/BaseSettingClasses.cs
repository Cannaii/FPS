using UnityEngine;
using System;
using System.Collections.Generic;

namespace cowsins.Settings
{
    /// <summary>
    /// Abstract base class for all settings with common functionalities
    /// </summary>
    public abstract class Setting<T> : ISetting<T>
    {
        public string Key { get; private set; }
        public T DefaultValue { get; private set; }
        public event Action<T> OnValueChanged;

        private T _value;
        public T Value
        {
            get => _value;
            set
            {
                if (!EqualityComparer<T>.Default.Equals(_value, value))
                {
                    _value = value;
                    OnValueChanged?.Invoke(_value);
                    Apply();
                    Save();
                    GameSettingsManager.Instance?.InvokeSettingsApplied();
                }
            }
        }

        protected Setting(string key, T defaultValue)
        {
            Key = key;
            DefaultValue = defaultValue;
            _value = defaultValue;
        }

        public abstract void Save();
        public abstract void Load();
        public virtual void Apply() { }

        public virtual void Reset()
        {
            Value = DefaultValue;
        }
    }

    // These Specific Settings Types all inherit from Setting. They are all based on PlayerPrefs as well.
    public class IntSetting : Setting<int>
    {
        public IntSetting(string key, int defaultValue) : base(key, defaultValue) { }

        public override void Save() => PlayerPrefs.SetInt(Key, Value);
        public override void Load() => Value = PlayerPrefs.GetInt(Key, DefaultValue);
    }

    public class FloatSetting : Setting<float>
    {
        public FloatSetting(string key, float defaultValue) : base(key, defaultValue) { }

        public override void Save() => PlayerPrefs.SetFloat(Key, Value);
        public override void Load() => Value = PlayerPrefs.GetFloat(Key, DefaultValue);
    }

    public class BoolSetting : Setting<bool>
    {
        public BoolSetting(string key, bool defaultValue) : base(key, defaultValue) { }

        public override void Save() => PlayerPrefs.SetInt(Key, Value ? 1 : 0);
        public override void Load() => Value = PlayerPrefs.GetInt(Key, DefaultValue ? 1 : 0) == 1;
    }

    public class StringSetting : Setting<string>
    {
        public StringSetting(string key, string defaultValue) : base(key, defaultValue) { }

        public override void Save() => PlayerPrefs.SetString(Key, Value);
        public override void Load() => Value = PlayerPrefs.GetString(Key, DefaultValue);
    }

    // OTHER SETTINGS ( SPECIFIC IMPLEMENTATIONS LIKE RESOLUTIONS, FRAMERATE, ETC )
    public class ResolutionSetting : IntSetting
    {
        private Resolution[] availableResolutions;
        private BoolSetting fullscreenSetting;

        public ResolutionSetting(string key, int defaultValue, BoolSetting fullscreenSetting)
            : base(key, defaultValue)
        {
            this.fullscreenSetting = fullscreenSetting;
            availableResolutions = Screen.resolutions;

            // Validates default value only if within the length of the Available resolutions Array
            if (defaultValue < 0 || defaultValue >= availableResolutions.Length)
                Value = availableResolutions.Length - 1;
        }

        public Resolution[] GetAvailableResolutions() => availableResolutions;

        public override void Save()
        {
            base.Save();
            if (Value >= 0 && Value < availableResolutions.Length)
            {
                Resolution res = availableResolutions[Value];
                PlayerPrefs.SetInt(Key + "_width", res.width);
                PlayerPrefs.SetInt(Key + "_height", res.height);
#if UNITY_6000_0_OR_NEWER
                PlayerPrefs.SetInt(Key + "_refreshNumerator", (int)res.refreshRateRatio.numerator);
                PlayerPrefs.SetInt(Key + "_refreshDenominator", (int)res.refreshRateRatio.denominator);
#else
                PlayerPrefs.SetInt(Key + "_refreshRate", Mathf.RoundToInt((float)res.refreshRate));
#endif
            }
        }

        public override void Load()
        {
            int width = PlayerPrefs.GetInt(Key + "_width", -1);
            int height = PlayerPrefs.GetInt(Key + "_height", -1);

            if (width != -1 && height != -1 && availableResolutions.Length > 0)
            {
                for (int i = 0; i < availableResolutions.Length; i++)
                {
                    if (availableResolutions[i].width == width && availableResolutions[i].height == height)
                    {
                        Value = i;
                        return;
                    }
                }
            }

            base.Load();

            if (availableResolutions.Length > 0)
                Value = Mathf.Clamp(Value, 0, availableResolutions.Length - 1);
            else
                Value = -1;
        }

        public override void Apply()
        {
            if (Value >= 0 && Value < availableResolutions.Length)
            {
                Resolution res = availableResolutions[Value];
                Screen.SetResolution(res.width, res.height, fullscreenSetting.Value);
            }
        }
    
    }

    public class FrameRateSetting : IntSetting
    {
        private static readonly int[] FrameRateOptions = { 60, 120, 230, 300 };

        public FrameRateSetting(string key, int defaultValue) : base(key, defaultValue) { }

        public override void Apply()
        {
            if (Value >= 0 && Value < FrameRateOptions.Length)
            {
                Application.targetFrameRate = FrameRateOptions[Value];
            }
        }

        public override void Reset()
        {
            Value = FrameRateOptions.Length - 1;
        }

        public int GetCurrentFrameRate() =>
            Value >= 0 && Value < FrameRateOptions.Length ? FrameRateOptions[Value] : 60;
    }

    public class VSyncSetting : BoolSetting
    {
        public VSyncSetting(string key, bool defaultValue) : base(key, defaultValue) { }

        public override void Apply()
        {
            QualitySettings.vSyncCount = Value ? 1 : 0;
        }
    }

    public class GraphicsQualitySetting : IntSetting
    {
        public GraphicsQualitySetting(string key, int defaultValue) : base(key, defaultValue) { }

        public override void Apply()
        {
            QualitySettings.SetQualityLevel(Value);
        }
    }

    public class AudioVolumeSetting : FloatSetting
    {
        private UnityEngine.Audio.AudioMixer mixer;
        private string mixerParameter;

        public AudioVolumeSetting(string key, float defaultValue,
            UnityEngine.Audio.AudioMixer mixer, string mixerParameter)
            : base(key, defaultValue)
        {
            this.mixer = mixer;
            this.mixerParameter = mixerParameter;
        }

        public override void Apply()
        {
            if (mixer != null)
            {
                float dbValue = Value > 0.001f ? Mathf.Log10(Value) * 20 : -144f;
                mixer.SetFloat(mixerParameter, dbValue);
            }
        }
    }
}
using System;
using UnityEngine;

namespace AFPS.Bootstrap
{
    public enum CrosshairStyle
    {
        Cross,
        Dot,
        Ring,
        CrossAndDot
    }

    /// <summary>纯显示参数；尺寸为1920×1080 Canvas参考像素，不影响瞄准或命中。</summary>
    [Serializable]
    public struct CrosshairAppearance
    {
        public CrosshairStyle Style;
        public float Size;
        public float Thickness;
        public float Gap;
        public float Opacity;
        public Color Color;
        public float Outline;

        public static CrosshairAppearance Default => new CrosshairAppearance
        {
            Style = CrosshairStyle.Cross,
            Size = 24f,
            Thickness = 2f,
            Gap = 4f,
            Opacity = 1f,
            Color = new Color(0.2f, 1f, 0.3f, 1f),
            Outline = 1f
        };

        public CrosshairAppearance Validated()
        {
            var value = this;
            var defaults = Default;
            if (!Enum.IsDefined(typeof(CrosshairStyle), value.Style))
            {
                value.Style = defaults.Style;
            }

            value.Size = Bounded(value.Size, 8f, 96f, defaults.Size);
            value.Thickness = Bounded(value.Thickness, 1f, 8f, defaults.Thickness);
            value.Gap = Bounded(value.Gap, 0f, Mathf.Min(24f, value.Size * 0.5f - 1f), defaults.Gap);
            value.Opacity = Bounded(value.Opacity, 0f, 1f, defaults.Opacity);
            value.Outline = Bounded(value.Outline, 0f, 4f, defaults.Outline);
            value.Color = new Color(
                Bounded(value.Color.r, 0f, 1f, defaults.Color.r),
                Bounded(value.Color.g, 0f, 1f, defaults.Color.g),
                Bounded(value.Color.b, 0f, 1f, defaults.Color.b), 1f);
            return value;
        }

        private static float Bounded(float value, float minimum, float maximum, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? Mathf.Clamp(fallback, minimum, maximum) : Mathf.Clamp(value, minimum, maximum);
        }
    }

    public static class CrosshairPreferences
    {
        private const string Key = "AFPS.Crosshair.v1";
        private static bool loaded;
        private static CrosshairAppearance current;
        public static event Action<CrosshairAppearance> Changed;

        [Serializable]
        private sealed class Saved
        {
            public int Version;
            public CrosshairAppearance Appearance;
        }

        public static CrosshairAppearance Current
        {
            get
            {
                if (!loaded)
                {
                    current = TryDecode(PlayerPrefs.GetString(Key, ""), out var saved)
                        ? saved : CrosshairAppearance.Default;
                    loaded = true;
                }
                return current;
            }
        }

        public static string Encode(CrosshairAppearance value)
        {
            return JsonUtility.ToJson(new Saved { Version = 1, Appearance = value.Validated() });
        }

        public static bool TryDecode(string json, out CrosshairAppearance value)
        {
            value = CrosshairAppearance.Default;
            if (string.IsNullOrEmpty(json))
            {
                return false;
            }

            try
            {
                var saved = JsonUtility.FromJson<Saved>(json);
                if (saved == null || saved.Version != 1)
                {
                    return false;
                }
                value = saved.Appearance.Validated();
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        public static void Apply(CrosshairAppearance value, bool persist)
        {
            current = value.Validated();
            loaded = true;
            if (persist)
            {
                PlayerPrefs.SetString(Key, Encode(current));
                PlayerPrefs.Save();
            }
            Changed?.Invoke(current);
        }
    }
}

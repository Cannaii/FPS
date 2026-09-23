using System;

namespace cowsins.Settings
{
    // Base interface for all settings
    public interface ISetting
    {
        string Key { get; }
        void Save();
        void Load();
        void Reset();
        void Apply();
    }

    // Generic setting interface
    public interface ISetting<T> : ISetting
    {
        T Value { get; set; }
        T DefaultValue { get; }
        event Action<T> OnValueChanged;
    }
}
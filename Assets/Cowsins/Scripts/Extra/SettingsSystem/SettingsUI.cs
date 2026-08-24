using UnityEngine;

namespace cowsins.Settings.UI
{
    // Base class for UI controllers that bind to settings
    public abstract class SettingUIController : MonoBehaviour
    {
        public abstract void Initialize();
        public abstract void Cleanup();
    }
}
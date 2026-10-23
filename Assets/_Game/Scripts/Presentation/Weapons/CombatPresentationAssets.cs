using UnityEngine;

namespace AFPS.Presentation.Weapons
{
    /// <summary>资源仅来自 _Game/Art 的自有副本，不加载第三方控制器或脚本。</summary>
    public static class CombatPresentationAssets
    {
        public static T Load<T>(string name) where T : Object => Resources.Load<T>("Combat/" + name);

        public static AudioClip FireClip(bool launcher) => Load<AudioClip>(launcher ? "LauncherFire" : "RifleFire");
        public static AudioClip ReloadClip(bool launcher) => Load<AudioClip>(launcher ? "LauncherReload" : "RifleReload");
    }
}

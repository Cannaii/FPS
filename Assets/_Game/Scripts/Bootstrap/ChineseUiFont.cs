using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace AFPS.Bootstrap
{
    /// <summary>项目自带的中文字体；动态生成实际用到的字形，不依赖操作系统或第三方工程。</summary>
    public static class ChineseUiFont
    {
        private static TMP_FontAsset asset;
        public static Font Source => Resources.Load<Font>("UI/NotoSansSC");

        public static TMP_FontAsset Asset
        {
            get
            {
                if (asset == null)
                {
                    Font source = Source;
                    if (source == null)
                    {
                        Debug.LogError("缺少项目中文字体：UI/NotoSansSC。");
                        return TMP_Settings.defaultFontAsset;
                    }
                    asset = TMP_FontAsset.CreateFontAsset(source, 64, 9, GlyphRenderMode.SDFAA,
                        2048, 2048, AtlasPopulationMode.Dynamic, true);
                    asset.name = "AFPS 中文界面字体";
                    asset.hideFlags = HideFlags.DontSave;
                }
                return asset;
            }
        }
    }
}

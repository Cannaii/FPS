using TMPro;
using UnityEngine;

namespace AFPS.Bootstrap
{
    /// <summary>F3诊断布局引用，不读取网络状态，不捕获输入。</summary>
    public sealed class NetworkDebugView : MonoBehaviour
    {
        public const string ResourcePath = "UI/NetworkDebug";

        [SerializeField] private Canvas canvas;
        [SerializeField] private TMP_Text clientText;
        [SerializeField] private TMP_Text serverText;
        [SerializeField] private TMP_Text detailText;

        public Canvas Canvas => canvas;
        public TMP_Text ClientText => clientText;
        public TMP_Text ServerText => serverText;
        public TMP_Text DetailText => detailText;

        public bool TryValidate(out string error)
        {
            if (canvas == null)
            {
                error = "F3诊断Prefab缺少引用：canvas";
                return false;
            }

            if (clientText == null)
            {
                error = "F3诊断Prefab缺少引用：clientText";
                return false;
            }

            if (serverText == null)
            {
                error = "F3诊断Prefab缺少引用：serverText";
                return false;
            }

            if (detailText == null)
            {
                error = "F3诊断Prefab缺少引用：detailText";
                return false;
            }

            error = null;
            return true;
        }
    }
}

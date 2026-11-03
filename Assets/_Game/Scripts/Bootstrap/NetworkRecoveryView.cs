using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AFPS.Bootstrap
{
    /// <summary>重连界面的可编辑控件引用，不持有传输或会话状态。</summary>
    public sealed class NetworkRecoveryView : MonoBehaviour
    {
        public const string ResourcePath = "UI/NetworkRecovery";

        [SerializeField] private Canvas canvas;
        [SerializeField] private TextMeshProUGUI message;
        [SerializeField] private Button retry;
        [SerializeField] private Button cancel;
        [SerializeField] private Button joinAsNew;

        public Canvas Canvas => canvas;
        public TextMeshProUGUI Message => message;
        public Button Retry => retry;
        public Button Cancel => cancel;
        public Button JoinAsNew => joinAsNew;

        public bool TryValidate(out string error)
        {
            if (canvas == null)
            {
                error = "重连Prefab缺少引用：canvas";
                return false;
            }

            if (message == null)
            {
                error = "重连Prefab缺少引用：message";
                return false;
            }

            if (retry == null)
            {
                error = "重连Prefab缺少引用：retry";
                return false;
            }

            if (cancel == null)
            {
                error = "重连Prefab缺少引用：cancel";
                return false;
            }

            if (joinAsNew == null)
            {
                error = "重连Prefab缺少引用：joinAsNew";
                return false;
            }

            error = null;
            return true;
        }
    }
}

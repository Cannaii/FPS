using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AFPS.Bootstrap
{
    public sealed class CrosshairSettingsView : MonoBehaviour
    {
        public const string ResourcePath = "UI/CrosshairSettings";
        [SerializeField] private CrosshairGraphic preview;
        [SerializeField] private Slider[] sliders;
        [SerializeField] private TMP_Text[] values;
        [SerializeField] private Button style;
        [SerializeField] private TMP_Text styleLabel;
        [SerializeField] private Button apply;
        [SerializeField] private Button cancel;
        [SerializeField] private Button reset;
        public CrosshairGraphic Preview => preview;
        public Slider[] Sliders => sliders;
        public TMP_Text[] Values => values;
        public Button Style => style;
        public TMP_Text StyleLabel => styleLabel;
        public Button Apply => apply;
        public Button Cancel => cancel;
        public Button Reset => reset;

        public bool IsConfigured
        {
            get
            {
                if (preview == null || sliders == null || sliders.Length != 8
                    || values == null || values.Length != 8 || style == null || styleLabel == null
                    || apply == null || cancel == null || reset == null)
                {
                    return false;
                }
                for (int index = 0; index < sliders.Length; index++)
                {
                    if (sliders[index] == null || values[index] == null)
                    {
                        return false;
                    }
                }
                return true;
            }
        }
    }
}

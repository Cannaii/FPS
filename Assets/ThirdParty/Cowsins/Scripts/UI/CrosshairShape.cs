#if UNITY_EDITOR
using UnityEditor.Presets;
#endif
using UnityEngine;

namespace cowsins
{
    [System.Serializable]
    public class CrosshairParts
    {
        public bool topPart = true, downPart = true, leftPart = true, rightPart = true, center = false,
            topLeftBracket = false, topRightBracket = false, bottomLeftBracket = false, bottomRightBracket = false;
    }

    public class CrosshairShape : MonoBehaviour
    {
        [SerializeField] private CrosshairParts defaultCrosshairParts;

        private CrosshairParts currentCrosshairParts;

        public CrosshairParts DefaultCrosshairParts => defaultCrosshairParts;
        public CrosshairParts CurrentCrosshairParts => currentCrosshairParts;

        public string presetName;

        private void Awake() => SetCrosshair(defaultCrosshairParts);
        public void SetCrosshair(CrosshairParts parts)
        {
            if (parts == null)
            {
                ResetCrosshairToDefault();
                return;
            }
                
            currentCrosshairParts = parts;
        }
        public void ResetCrosshairToDefault() => currentCrosshairParts = defaultCrosshairParts;
    }
}
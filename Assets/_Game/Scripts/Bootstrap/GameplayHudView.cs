using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AFPS.Bootstrap
{
    /// <summary>战斗HUD的可编辑布局引用与纯表现参数，不持有或修改模拟状态。</summary>
    public sealed class GameplayHudView : MonoBehaviour
    {
        public const string ResourcePath = "UI/GameplayHud";

        [SerializeField] private Canvas canvas;
        [SerializeField] private GameObject scorePanel;
        [SerializeField] private Image damageFlash;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI networkText;
        [SerializeField] private TextMeshProUGUI healthText;
        [SerializeField] private TextMeshProUGUI weaponText;
        [SerializeField] private TextMeshProUGUI centerText;
        [SerializeField] private TextMeshProUGUI hitMarkerText;
        [SerializeField] private TextMeshProUGUI rejectionText;
        [SerializeField] private TextMeshProUGUI damageDirectionText;
        [Tooltip("命中与击杀确认颜色，仅影响HUD表现。")]
        [SerializeField] private Color hitColor = Color.white;
        [SerializeField] private Color killColor = new Color(1f, 0.25f, 0.2f);
        [Tooltip("受击箭头相对编辑位置的半径，单位为Canvas参考像素。")]
        [SerializeField, Min(0f)] private float damageDirectionRadius = 110f;
        [Tooltip("弹药数字字号，单位为TMP富文本的参考像素。")]
        [SerializeField, Min(1)] private int ammoFontSize = 38;

        public Canvas Canvas => canvas;
        public GameObject ScorePanel => scorePanel;
        public Image DamageFlash => damageFlash;
        public TextMeshProUGUI StatusText => statusText;
        public TextMeshProUGUI ScoreText => scoreText;
        public TextMeshProUGUI NetworkText => networkText;
        public TextMeshProUGUI HealthText => healthText;
        public TextMeshProUGUI WeaponText => weaponText;
        public TextMeshProUGUI CenterText => centerText;
        public TextMeshProUGUI HitMarkerText => hitMarkerText;
        public TextMeshProUGUI RejectionText => rejectionText;
        public TextMeshProUGUI DamageDirectionText => damageDirectionText;
        public Color HitColor => hitColor;
        public Color KillColor => killColor;
        public float DamageDirectionRadius => damageDirectionRadius;
        public int AmmoFontSize => ammoFontSize;

        public bool TryValidate(out string error)
        {
            if (canvas == null)
            {
                error = "战斗HUD Prefab缺少引用：canvas";
                return false;
            }

            if (scorePanel == null)
            {
                error = "战斗HUD Prefab缺少引用：scorePanel";
                return false;
            }

            if (damageFlash == null)
            {
                error = "战斗HUD Prefab缺少引用：damageFlash";
                return false;
            }

            if (statusText == null)
            {
                error = "战斗HUD Prefab缺少引用：statusText";
                return false;
            }

            if (scoreText == null)
            {
                error = "战斗HUD Prefab缺少引用：scoreText";
                return false;
            }

            if (networkText == null)
            {
                error = "战斗HUD Prefab缺少引用：networkText";
                return false;
            }

            if (healthText == null)
            {
                error = "战斗HUD Prefab缺少引用：healthText";
                return false;
            }

            if (weaponText == null)
            {
                error = "战斗HUD Prefab缺少引用：weaponText";
                return false;
            }

            if (centerText == null)
            {
                error = "战斗HUD Prefab缺少引用：centerText";
                return false;
            }

            if (hitMarkerText == null)
            {
                error = "战斗HUD Prefab缺少引用：hitMarkerText";
                return false;
            }

            if (rejectionText == null)
            {
                error = "战斗HUD Prefab缺少引用：rejectionText";
                return false;
            }

            if (damageDirectionText == null)
            {
                error = "战斗HUD Prefab缺少引用：damageDirectionText";
                return false;
            }

            error = null;
            return true;
        }
    }
}

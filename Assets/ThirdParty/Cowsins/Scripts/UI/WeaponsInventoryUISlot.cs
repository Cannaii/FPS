/// <summary>
/// This script belongs to cowsins™ as a part of the cowsins´ FPS Engine. All rights reserved. 
/// </summary>
using UnityEngine.UI;
using UnityEngine;
using TMPro;

namespace cowsins
{
    /// <summary>
    /// Each slot you see on your UI, which is generated depending on your inventory size, requires this component
    /// </summary>
    public class WeaponsInventoryUISlot : MonoBehaviour
    {
        [SerializeField] private Image image;

        [SerializeField] private Sprite nullWeaponIndicator;
        [SerializeField] private TextMeshProUGUI numKeyText;

        private Weapon_SO weapon;

        private Vector3 initScale;

        private CanvasGroup canvasGroup; 

        private void OnEnable()
        {
            initScale = transform.localScale;
            canvasGroup = GetComponent<CanvasGroup>(); 
        }

        public void GetImage() => image.sprite = (weapon == null) ? nullWeaponIndicator : weapon.icon;

        public void Select()
        {
            transform.localScale = initScale * 1.2f;
            canvasGroup.alpha = 1;
        }

        public void Deselect()
        {
            transform.localScale = initScale;
            canvasGroup.alpha = .2f;
        }

        public void SetWeapon(Weapon_SO newWeapon)
        {
            weapon = newWeapon ?? null;
            GetImage();
        }

        public void Initialize(int index)
        {
            numKeyText.text = (index + 1).ToString();
        }
    }
}

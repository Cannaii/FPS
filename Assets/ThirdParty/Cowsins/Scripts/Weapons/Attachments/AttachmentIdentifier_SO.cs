using UnityEngine;
#if INVENTORY_PRO_ADD_ON
using cowsins.Inventory;
#endif
namespace cowsins
{
    [CreateAssetMenu(fileName = "NewAttachmentIdentifier", menuName = "COWSINS/New Attachment Identifier", order = 2)]
    public class AttachmentIdentifier_SO : Item_SO
    {
        #region PROPERTIES
        public AttachmentType attachmentType;
        [Range(-1f, 1f), Tooltip("Percentage added to the base reload speed")] public float reloadSpeedIncrease;
        [Range(-1f, 1f), Tooltip("Percentage added to the base aim speed")] public float aimSpeedIncrease;
        [Range(-.2f, .2f), Tooltip("Spread amount to reduce the base spread")] public float spreadDecrease = 0;
        [Range(-1f, 1f), Tooltip("Percentage added to the base fire rate")] public float fireRateDecrease;
        [Range(-.99f, 5f), Tooltip("Percentage added to the base damage")] public float damageIncrease;
        [Tooltip("Weight to add to the weapon. If it weighs less, set a negative value.")] public float weightAdded;
        [Range(-1f, 1f)] public float cameraShakeMultiplier;
        [Range(-1f, 1f), Tooltip("Percentage added to the base penetration")] public float penetrationIncrease;
        #endregion

#if INVENTORY_PRO_ADD_ON
        public override void Use(InventoryProManager inventoryProManager, InventorySlot slot)
        {
            if (inventoryProManager._WeaponController.Id == null) 
            {
                ToastManager.Instance?.ShowToast(ToastManager.Instance.AttachmentNotCompatibleMsg);
                return;
            }

            (bool success, Attachment attachment, int attachmentIdentifier) = CowsinsUtilities.CompatibleAttachment(inventoryProManager._WeaponController.Weapon.weaponObject, this);
            if (!success)
            {
                ToastManager.Instance?.ShowToast("This attachment is not compatible");
                return;
            }
            inventoryProManager._WeaponController.attachmentsSystem.AssignAttachmentToWeapon(attachment, inventoryProManager._WeaponController.CurrentWeaponIndex);
            inventoryProManager._GridGenerator.Operations.ClearSlotArea(slot);
        }
#endif
    }
}
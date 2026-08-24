using UnityEngine;
#if INVENTORY_PRO_ADD_ON
using cowsins.Inventory;
#endif
namespace cowsins
{
    public partial class AttachmentPickeable : Pickeable
    {
        [Tooltip("Attachment to be picked up. Notice that attachment identifiers can be shared among attachments in different weapons."), SaveField]
        public AttachmentIdentifier_SO attachmentIdentifier;

        private int attachmentID;

        private Attachment atc;
        public Attachment Atc => atc;

        private void Start()
        {
            // If the pickeable hasnt been dropped, dont keep going
            if (dropped) return;
            GetVisuals();
        }
        public override void Interact(Transform player)
        {
            if (attachmentIdentifier == null)
            {
                CowsinsUtilities.LogError("<b><color=yellow>AttachmentIdentifier_SO</color></b> " +
                "not found! Skipping Interaction.", this);
                return;
            }

            PlayerDependencies playerDependencies = player.GetComponent<PlayerDependencies>();
            // Reference to WeaponController
            IWeaponReferenceProvider weaponReference = playerDependencies.WeaponReference;
            IInteractEventsProvider interactEventsProvider = playerDependencies.InteractEvents;

            // If the weapon is null or this is not a compatible attachment for the current unholstered weapon, return
            if (weaponReference.Weapon == null || !CheckCompatibleAttachment(weaponReference))
            {
#if INVENTORY_PRO_ADD_ON
                if (InventoryProManager.instance)
                {
                    (bool success, int remainingAmount) = InventoryProManager.instance._GridGenerator.Operations.AddItemToInventory(attachmentIdentifier, 1);
                    if(success)
                    {
                        alreadyInteracted = true;
                        ToastManager.Instance?.ShowToast($"{attachmentIdentifier._name} {ToastManager.Instance.CollectedMsg}");
                        StoreData();
                        Destroy(this.gameObject);
                    }
                    else
                        ToastManager.Instance?.ShowToast(ToastManager.Instance.InventoryIsFullMsg);
                }
#endif
                alreadyInteracted = false;
                return;
            }

            // If compatible, assign a new attachment && Unholster the weapon
            interactEventsProvider.Events.OnAttachmentPickedUp?.Invoke(atc);

            base.Interact(player);

            Destroy(this.gameObject);
        }

        // Get visuals of the attachment when dropping
        public override void Drop(PlayerDependencies playerDependencies, PlayerOrientation orientation)
        {
            base.Drop(playerDependencies, orientation);
            GetVisuals();
        }
        public void GetVisuals()
        {
            // Get whatever we need to display
            if (attachmentIdentifier == null)
            {
                Debug.LogError("Attachment Identifier not set-up! Please assign a proper attachment identifier to your existing attachments, otherwise the system won�t work properly.");
                return;
            }
            interactText = attachmentIdentifier._name;
            image.sprite = attachmentIdentifier.icon;
            if (attachmentIdentifier.pickUpGraphics == null) return;
            Destroy(graphics.GetChild(0).gameObject);
            Instantiate(attachmentIdentifier.pickUpGraphics, transform.position, Quaternion.identity, graphics);
        }
        public override bool IsForbiddenInteraction(IWeaponReferenceProvider weaponController)
        {
            return AddonManager.instance.isInventoryAddonAvailable
                ? false
                : weaponController.Weapon != null && !CheckCompatibleAttachment(weaponController) || weaponController.Weapon == null;
        }
        public bool CheckCompatibleAttachment(IWeaponReferenceProvider weaponController)
        {
            (bool success, Attachment attachment, int atcId) = CowsinsUtilities.CompatibleAttachment(weaponController.Id, attachmentIdentifier);
            if (success)
            {
                atc = attachment;
                attachmentID = atcId;
            }
            return success;
        }

#if SAVE_LOAD_ADD_ON
        // Destroy if picked up.
        // Interacted State is called after loading.
        public override void LoadedState()
        {
            if (this.alreadyInteracted) Destroy(this.gameObject);
            else GetVisuals();
        }
#endif
    }
}
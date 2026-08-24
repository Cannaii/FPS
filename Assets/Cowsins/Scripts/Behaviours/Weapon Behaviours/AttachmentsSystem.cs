using UnityEngine;
using System.Collections.Generic;

namespace cowsins
{
    public class AttachmentsSystem
    {
        private WeaponContext context;
        private InputManager inputManager;
        private IWeaponReferenceProvider weaponReference;

        private Weapon_SO weapon => weaponReference.Weapon;
        private WeaponIdentification id => weaponReference.Id;
        private WeaponIdentification[] inventory => weaponReference.Inventory;

        private WeaponControllerSettings settings;

        public AttachmentsSystem(WeaponContext context, WeaponControllerSettings settings)
        {
            this.context = context;
            this.settings = settings;

            this.inputManager = context.InputManager;
            this.weaponReference = context.Dependencies.WeaponReference;

            // Each time we click on the attachment UI, we should perform the assignment.
            UIEvents.onAttachmentUIElementClickedNewAttachment += AssignNewAttachment;
            context.Dependencies.WeaponEvents.Events.OnAssignAttachmentsToWeapon.AddListener(AssignAttachmentsToWeapon);

            context.Dependencies.InteractEvents.Events.OnAttachmentPickedUp.AddListener(AssignNewAttachment);
        }

        public void AssignNewAttachment(AttachmentIdentifier_SO attachmentIdentifier_SO) => AssignNewAttachmentToWeapon(attachmentIdentifier_SO, weaponReference.CurrentWeaponIndex);

        public void AssignNewAttachmentToWeapon(AttachmentIdentifier_SO attachmentIdentifier_SO, int inventoryIndex)
        {
            (bool compatible, Attachment newAttachment, int atcIndex) = CowsinsUtilities.CompatibleAttachment(inventory[inventoryIndex], attachmentIdentifier_SO);
            if (!compatible) return;

            AssignAttachmentToWeapon(newAttachment, inventoryIndex);
        }

        /// <summary>
        /// Equips the passed attachment.
        /// </summary>
        /// <param name="attachment">Attachment to equip.</param>
        /// <param name="attachmentID">Order ID of the attachment in the WeaponIdentification's compatible attachment array.</param>
        public void AssignNewAttachment(Attachment attachment) => AssignAttachmentToWeapon(attachment, weaponReference.CurrentWeaponIndex);

        /// <summary>
        /// Equips the passed attachment. 
        /// </summary>
        /// <param name="attachment">Attachment to equip</param>
        /// <param name="attachmentID">Order ID of the attachment to equip in the WeaponIdentification compatible attachment array.</param>
        public void AssignAttachmentToWeapon(Attachment attachment, int inventoryIndex)
        {
            if (attachment == null) return;

            if(attachment.attachmentIdentifier == null)
            {
                Debug.LogError($"<color=red>[COWSINS]</color> Attachment Identifier not configured in {attachment.name}");
                return;
            }

            WeaponIdentification curWeapon = inventory[inventoryIndex];

            (bool compatible, Attachment newAttachment, int atcIndex) = CowsinsUtilities.CompatibleAttachment(curWeapon, attachment.attachmentIdentifier);
            if (!compatible) return;

            AttachmentType type = attachment.attachmentIdentifier.attachmentType;
            AttachmentStateManager stateManager = curWeapon.AttachmentState;

            // Handle current attachment replacement using attachment state for the specific weapon
            Attachment currentAttachment = stateManager.GetCurrent(type);
            bool isCurrentDefault = stateManager.IsCurrentDefault(type);

            if (currentAttachment == newAttachment) return;

            if (currentAttachment != null)
            {
                currentAttachment.gameObject.SetActive(false);

                // Drop non default attachments
                if (!isCurrentDefault)
                {
                    context.Transform.GetComponent<InteractManager>().DropAttachment(currentAttachment, false);
                }
            }

            // Equip new attachment through state manager
            newAttachment.gameObject.SetActive(true);
            newAttachment.Attach(curWeapon);

            settings.userEvents.OnAttachAttachment?.Invoke();
        }

        public void ToggleFlashLight()
        {
            Attachment flashlight = inventory[weaponReference.CurrentWeaponIndex]?.AttachmentState.GetCurrent(AttachmentType.Flashlight);

            if (flashlight == null) return;

            flashlight.AttachmentAction();
        }

        private void AssignAttachmentsToWeapon(WeaponIdentification weaponPicked, int inventoryIndex, List<AttachmentIdentifier_SO> attachmentsToAssign)
        {
            List<AttachmentIdentifier_SO> attachments = attachmentsToAssign == null || attachmentsToAssign.Count <= 0 ? weaponPicked.AttachmentState.GetDefaultIdentifiers() : attachmentsToAssign;
            foreach (AttachmentIdentifier_SO attachmentIdentifier in attachments)
            {
                AssignNewAttachmentToWeapon(attachmentIdentifier, inventoryIndex);
            }
        }
    }

}
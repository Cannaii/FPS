using UnityEngine;

namespace cowsins
{
    public abstract class Attachment : MonoBehaviour
    {
        [Title("Basic")]
        [Tooltip("Identifier of the attachment. You can have the same attachment within different weapons as long as they share this attachment identifier" +
                "scriptable object.")]
        public AttachmentIdentifier_SO attachmentIdentifier;

        public virtual void Attach(WeaponIdentification Id)
        {
            this.gameObject.SetActive(true);

            if (attachmentIdentifier == null)
            {
                Debug.LogError($"<color=red>[COWSINS]</color> Attachment Identifier not configured in {this.name}");
                return;
            }

            ModifyStats(Id, 1);
            Id.AttachmentState.SetCurrent(attachmentIdentifier.attachmentType, this);
        }

        public virtual void Dettach(WeaponIdentification Id)
        {
            this.gameObject.SetActive(false);

            if (attachmentIdentifier == null)
            {
                Debug.LogError($"<color=red>[COWSINS]</color> Attachment Identifier not configured in {this.name}");
                return;
            }

            ModifyStats(Id, -1);
            Id.AttachmentState.RemoveCurrent(attachmentIdentifier.attachmentType);
        }

        public virtual void AttachmentAction() { }

        private void ModifyStats(WeaponIdentification Id, int direction)
        {
            Weapon_SO weapon = Id.weapon;
            Id.damage += direction * (weapon.damagePerBullet * attachmentIdentifier.damageIncrease);
            Id.fireRate -= direction * (weapon.fireRate * attachmentIdentifier.fireRateDecrease);
            Id.baseSpread -= direction * (weapon.spreadAmount * attachmentIdentifier.spreadDecrease);
            Id.baseAimSpread -= direction * (weapon.aimSpreadAmount * attachmentIdentifier.spreadDecrease);
            Id.aimSpeed += direction * (weapon.aimingSpeed * attachmentIdentifier.aimSpeedIncrease);
            Id.reloadTime += direction * (weapon.reloadTime * attachmentIdentifier.reloadSpeedIncrease);
            Id.emptyReloadTime += direction * (weapon.emptyReloadTime * attachmentIdentifier.reloadSpeedIncrease);
            Id.weightMultiplier += direction * (weapon.weightMultiplier * attachmentIdentifier.weightAdded);
            Id.camShakeAmount += direction * (weapon.camShakeAmount * attachmentIdentifier.cameraShakeMultiplier);
            Id.penetrationAmount += direction * (weapon.penetrationAmount * attachmentIdentifier.penetrationIncrease);
        }
    }
}
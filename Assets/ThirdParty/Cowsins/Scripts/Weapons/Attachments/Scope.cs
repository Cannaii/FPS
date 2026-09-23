using UnityEngine;
namespace cowsins
{
    public class Scope : Attachment
    {
        [Title("Scope")]
        [Tooltip("Vector3 added to the position of th weapon when aiming with this scoped equipped.")] public Vector3 aimingOffset;
        [Tooltip("Rotation of the weapon when aiming with this scope equipped.")] public Vector3 aimingRotation;

        public override void Attach(WeaponIdentification Id)
        {
            Id.aimingOffset = aimingOffset;
            Id.aimingRotation = aimingRotation;
            base.Attach(Id);
        }

        public override void Dettach(WeaponIdentification Id)
        {
            Id.aimingOffset = Vector3.zero;
            Id.aimingRotation = Id.weapon.aimingRotation;
            base.Dettach(Id);
        }
    }
}
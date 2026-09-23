using UnityEngine;
namespace cowsins
{
    public class Magazine : Attachment
    {
        [Title("Magazine")]
        [Tooltip("Capacity to add to the default magazine. If this magazine supports less bullets, set a negative value.")] public int magazineCapacityAdded;

        public override void Attach(WeaponIdentification Id)
        {
            Id.magazineSize += magazineCapacityAdded;
            ClampBulletsToMagazineCapacity(Id);

            base.Attach(Id);
        }

        public override void Dettach(WeaponIdentification Id)
        {
            Id.magazineSize = Id.weapon.magazineSize;
            ClampBulletsToMagazineCapacity(Id);

            base.Dettach(Id);
        }

        private void ClampBulletsToMagazineCapacity(WeaponIdentification Id)
        {
            if (Id.bulletsLeftInMagazine > Id.magazineSize) Id.bulletsLeftInMagazine = Id.magazineSize;
        }
    }
}
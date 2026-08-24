using System.Collections.Generic;
using UnityEngine;

namespace cowsins
{
    // Implemented by WeaponController and required by PlayerDependencies
    public interface IWeaponReferenceProvider
    {
        public Camera MainCamera { get; }
        Weapon_SO Weapon { get; set; }
        WeaponIdentification Id { get; set; }
        public int CurrentWeaponIndex {  get; set; }
        public WeaponIdentification[] Inventory { get; set; }
    }
    public interface IWeaponBehaviourProvider
    {
        bool IsAiming { get; set; }
        bool IsReloading { get; set; }
        bool IsShooting { get; set; }
        bool AlternateAiming { get; }
        bool IsMeleeAvailable { get; set;  }
        float AimingCamShakeMultiplier { get; }
        float CrouchingCamShakeMultiplier { get; set; }

        bool AddDuplicateWeaponAmmo(int amount);
        bool TryToAddWeapons(Weapon_SO weapon, int currentBullets, int totalBullets, List<AttachmentIdentifier_SO> attachments);
        (Weapon_SO, int, int) SwapWeapons(Weapon_SO weapon, int currentBullets, int totalBullets, List<AttachmentIdentifier_SO> attachments);

    }
    public interface IWeaponRecoilProvider
    {
        float RecoilPitchOffset { get; }
        float RecoilYawOffset { get; }
    }
    public interface IWeaponEventsProvider
    {
        WeaponControllerEvents Events { get; }
    }
}
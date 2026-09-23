using UnityEngine;
#if INVENTORY_PRO_ADD_ON
using cowsins.Inventory;
#endif
namespace cowsins
{
    public partial class BulletsPickeable : Pickeable
    {
        [Tooltip("How many bullets you will get"), SerializeField, SaveField] private int amountOfBullets;

        [SerializeField] private BulletTypeIdentifier_SO bulletTypeIdentifier;

        public int AmountOfBullets => amountOfBullets;

        public override void Awake()
        {
            base.Awake();

            if (bulletTypeIdentifier == null)
            {
                CowsinsUtilities.LogError("<b><color=yellow>Bullet_SO</color></b> " +
                "not found!", this);
                return;
            }

            image.sprite = bulletTypeIdentifier.icon;
            Destroy(graphics.transform.GetChild(0).gameObject);
            Instantiate(bulletTypeIdentifier.pickUpGraphics, graphics);
        }
        public override void Interact(Transform player)
        {

            if (bulletTypeIdentifier == null)
            {
                CowsinsUtilities.LogError("<b><color=yellow>Bullet_SO</color></b> " +
                "not found! Skipping Interaction.", this);
                return;
            }

#if INVENTORY_PRO_ADD_ON
            if (InventoryProManager.instance)
            {
                (bool success, int remainingAmount) = InventoryProManager.instance._GridGenerator.Operations.AddItemToInventory(bulletTypeIdentifier, amountOfBullets);
                if (success)
                {
                    alreadyInteracted = true;
                    interactableEvents.OnInteract?.Invoke();
                    StoreData();
                    ToastManager.Instance?.ShowToast($"x{amountOfBullets - remainingAmount} {ToastManager.Instance.CollectedMsg}");
                    amountOfBullets = remainingAmount;
                    if(amountOfBullets <= 0) Destroy(this.gameObject);
                }
                else
                    ToastManager.Instance?.ShowToast(ToastManager.Instance.InventoryIsFullMsg);
                return;
            }
#else
            if (player.GetComponent<IWeaponReferenceProvider>().Weapon == null) return;
#endif
            alreadyInteracted = true; 
            base.Interact(player);

            PlayerDependencies playerDependencies = player.GetComponent<PlayerDependencies>();
            playerDependencies.WeaponReference.Id.totalBullets += amountOfBullets;
            playerDependencies.WeaponEvents.Events.OnAmmoChanged?.Invoke(false); 
            Destroy(this.gameObject);
        }
        public void SetBullets(BulletTypeIdentifier_SO bulletsSO, int amountOfBullets)
        {
            this.amountOfBullets = amountOfBullets;
            this.bulletTypeIdentifier = bulletsSO;
        }

        public override bool IsForbiddenInteraction(IWeaponReferenceProvider weaponController)
        {
            Weapon_SO weapon = weaponController.Weapon;

            return AddonManager.instance.isInventoryAddonAvailable
                ? false
                : weapon != null && !weapon.limitedMagazines || weapon != null && weapon.limitedMagazines && !CowsinsUtilities.MatchesBulletType(bulletTypeIdentifier, weapon.bulletTypeIdentifier) || weaponController.Weapon == null;
        }


#if SAVE_LOAD_ADD_ON
        // Destroy if picked up.
        // Interacted State is called after loading.
        public override void LoadedState()
        {
            if (this.alreadyInteracted) Destroy(this.gameObject);
        }
#endif
    }
}
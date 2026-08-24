using UnityEngine;

namespace cowsins
{
    public class AddonManager : MonoBehaviour
    {
        [HideInInspector] public bool isInventoryAddonAvailable;
        [HideInInspector] public bool isSaveLoadAddonAvailable;

        public static AddonManager instance;

        private void Awake()
        {
            instance = this;

#if INVENTORY_PRO_ADD_ON
            isInventoryAddonAvailable = true;
#else
            isInventoryAddonAvailable = false;
#endif
#if SAVE_LOAD_ADD_ON
            isSaveLoadAddonAvailable = true;
#else
            isSaveLoadAddonAvailable = false;
#endif
        }
    }

}
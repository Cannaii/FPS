using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
#if INVENTORY_PRO_ADD_ON
using cowsins.Inventory;
#endif
#if SAVE_LOAD_ADD_ON
using cowsins.SaveLoad;
#endif

namespace cowsins
{
    public partial class WeaponPickeable : Pickeable
    {
        [Tooltip("Which weapon are we grabbing"), SaveField] public Weapon_SO weapon;

        [SaveField] private int currentBullets, totalBullets;
        public Dictionary<AttachmentType, AttachmentIdentifier_SO> currentAttachments = new Dictionary<AttachmentType, AttachmentIdentifier_SO>();

        public override void Awake()
        {
            base.Awake();
            foreach (AttachmentType type in System.Enum.GetValues(typeof(AttachmentType)))
            {
                if (!currentAttachments.ContainsKey(type))
                    currentAttachments[type] = null;
            }
            if (dropped) return;
            Initialize();
        }

        public override void Interact(Transform player)
        {
            if (weapon == null)
            {
                CowsinsUtilities.LogError("<b><color=yellow>Weapon_SO</color></b> " +
                "not found! Skipping Interaction.", this);
                return;
            }
            base.Interact(player);

            PlayerDependencies playerDependencies = player.GetComponent<PlayerDependencies>();
            IWeaponBehaviourProvider weaponController = player.GetComponent<IWeaponBehaviourProvider>();
            IInteractManagerProvider interactManager = playerDependencies.InteractManager;

            if (interactManager.DuplicateWeaponAddsBullets && weaponController.AddDuplicateWeaponAmmo(10))
            {
                DestroyAndSave();
                return;
            }

            if (weaponController.TryToAddWeapons(weapon, currentBullets, totalBullets, currentAttachments.Values.ToList()))
            {
                DestroyAndSave();
                return;
            }

#if INVENTORY_PRO_ADD_ON
             if (InventoryProManager.instance && InventoryProManager.instance.StoreWeaponsIfHotbarFull)
            {
                bool success = InventoryProManager.instance._GridGenerator.Operations.AddWeaponToInventory(weapon, currentBullets, totalBullets);
                if (success)
                {
                    ToastManager.Instance?.ShowToast($"{weapon._name} {ToastManager.Instance.CollectedMsg}");
                    DestroyAndSave();
                }
                else
                    ToastManager.Instance?.ShowToast(ToastManager.Instance.InventoryIsFullMsg);
                return;
            }
#endif
            (Weapon_SO swappedWeapon, int swappedCurrentBullets, int swappedTotalBullets) = weaponController.SwapWeapons(weapon, currentBullets, totalBullets, currentAttachments.Values.ToList());
            this.weapon = swappedWeapon;
            this.currentBullets = swappedCurrentBullets;
            this.totalBullets = swappedTotalBullets;

            DestroyGraphics();
            GetVisuals();

            alreadyInteracted = false;
#if SAVE_LOAD_ADD_ON
            StoreData();
#endif
        }

        private void DestroyAndSave()
        {
#if SAVE_LOAD_ADD_ON
            alreadyInteracted = true;
            StoreData();
#endif
            Destroy(this.gameObject);
        }

        public override void Drop(PlayerDependencies playerDependencies, PlayerOrientation orientation)
        {
            base.Drop(playerDependencies, orientation);

            IWeaponReferenceProvider wRef = playerDependencies.WeaponReference;
            WeaponIdentification currentId = wRef.Id != null ? wRef.Id : wRef.Inventory[wRef.CurrentWeaponIndex];

            if (currentId != null)
            {
                currentBullets = currentId.bulletsLeftInMagazine;
                totalBullets = currentId.totalBullets;
                weapon = currentId.weapon;
            }
            GetVisuals();
        }

        public void DropOverrideParameters(Weapon_SO weapon, int currentBullets, int totalBullets, Dictionary<AttachmentType, AttachmentIdentifier_SO> tempAttachments)
        {
            this.pickeable = true;
            this.weapon = weapon;
            this.currentBullets = currentBullets;
            this.totalBullets = totalBullets;
            foreach (AttachmentType type in System.Enum.GetValues(typeof(AttachmentType)))
            {
                currentAttachments[type] = tempAttachments[type];
            }
            GetVisuals();
        }

        /// <summary>
        /// Stores the attachments on the WeaponPickeable so they can be accessed later in case the weapon is picked up.
        /// </summary>
        public void SetPickeableAttachments(WeaponIdentification wId)
        {
            foreach (AttachmentType type in System.Enum.GetValues(typeof(AttachmentType)))
            {
                currentAttachments[type] = wId.AttachmentState.GetCurrent(type)?.attachmentIdentifier;
            }
        }

        #region INITIALIZATION
        private void Initialize()
        {
            if (weapon == null) return;
            GetVisuals();

            var weaponId = weapon.weaponObject;

            // Handle Attachments
            SetDefaultAttachments(weaponId);
            int magCapacityAdded = 0;
            if (weaponId != null && weaponId.AttachmentState != null && weaponId.AttachmentState.GetDefault(AttachmentType.Magazine) is Magazine magazine)
                magCapacityAdded = magazine.magazineCapacityAdded;

            currentBullets = weapon.magazineSize + magCapacityAdded;
            totalBullets = weapon.totalMagazines * currentBullets;
        }

        public void GetVisuals()
        {
            // Get whatever we need to display
            interactText = weapon._name;
            image.sprite = weapon.icon;
            // Manage graphics
            Destroy(graphics.transform.GetChild(0).gameObject);
            Instantiate(weapon.pickUpGraphics, graphics);
        }

        public AttachmentIdentifier_SO GetAttachmentByType(AttachmentType type)
        {
            if (currentAttachments.TryGetValue(type, out AttachmentIdentifier_SO attachmentId))
            {
                return attachmentId;
            }
            return null;
        }

        // Applied the default attachments to the weapon
        private void SetDefaultAttachments(WeaponIdentification weaponId)
        {
            if (weaponId == null || weaponId.AttachmentState == null)
            {
                return;
            }

            foreach (AttachmentType type in System.Enum.GetValues(typeof(AttachmentType)))
            {
                Attachment defaultAttachment = weaponId.AttachmentState.GetDefault(type);
                currentAttachments[type] = defaultAttachment?.attachmentIdentifier;
            }
        }


#if SAVE_LOAD_ADD_ON
        public override CustomSaveData SaveFields()
        {
            // Save base fields
            CustomSaveData saveData = base.SaveFields();
            
            // Save attachments
            if (currentAttachments.ContainsKey(AttachmentType.Barrel)) saveData.savedFields["barrel"] = currentAttachments[AttachmentType.Barrel]?.name;
            if (currentAttachments.ContainsKey(AttachmentType.Scope)) saveData.savedFields["scope"] = currentAttachments[AttachmentType.Scope]?.name;
            if (currentAttachments.ContainsKey(AttachmentType.Stock)) saveData.savedFields["stock"] = currentAttachments[AttachmentType.Stock]?.name;
            if (currentAttachments.ContainsKey(AttachmentType.Grip)) saveData.savedFields["grip"] = currentAttachments[AttachmentType.Grip]?.name;
            if (currentAttachments.ContainsKey(AttachmentType.Magazine)) saveData.savedFields["magazine"] = currentAttachments[AttachmentType.Magazine]?.name;
            if (currentAttachments.ContainsKey(AttachmentType.Flashlight)) saveData.savedFields["flashlight"] = currentAttachments[AttachmentType.Flashlight]?.name;
            if (currentAttachments.ContainsKey(AttachmentType.Laser)) saveData.savedFields["laser"] = currentAttachments[AttachmentType.Laser]?.name;
            
            return saveData;
        }

        public override void LoadFields(object data)
        {
            // Load base fields first
            base.LoadFields(data);
            
            if (data is CustomSaveData saveData)
            {
                // Restore attachments
                if (saveData.savedFields.TryGetValue("barrel", out object barrelName) && barrelName != null)
                    currentAttachments[AttachmentType.Barrel] = ItemRegistry.GetItemByName(barrelName.ToString()) as AttachmentIdentifier_SO;
                
                if (saveData.savedFields.TryGetValue("scope", out object scopeName) && scopeName != null)
                    currentAttachments[AttachmentType.Scope] = ItemRegistry.GetItemByName(scopeName.ToString()) as AttachmentIdentifier_SO;
                
                if (saveData.savedFields.TryGetValue("stock", out object stockName) && stockName != null)
                    currentAttachments[AttachmentType.Stock] = ItemRegistry.GetItemByName(stockName.ToString()) as AttachmentIdentifier_SO;
                
                if (saveData.savedFields.TryGetValue("grip", out object gripName) && gripName != null)
                    currentAttachments[AttachmentType.Grip] = ItemRegistry.GetItemByName(gripName.ToString()) as AttachmentIdentifier_SO;
                
                if (saveData.savedFields.TryGetValue("magazine", out object magazineName) && magazineName != null)
                    currentAttachments[AttachmentType.Magazine] = ItemRegistry.GetItemByName(magazineName.ToString()) as AttachmentIdentifier_SO;
                
                if (saveData.savedFields.TryGetValue("flashlight", out object flashlightName) && flashlightName != null)
                    currentAttachments[AttachmentType.Flashlight] = ItemRegistry.GetItemByName(flashlightName.ToString()) as AttachmentIdentifier_SO;
                
                if (saveData.savedFields.TryGetValue("laser", out object laserName) && laserName != null)
                    currentAttachments[AttachmentType.Laser] = ItemRegistry.GetItemByName(laserName.ToString()) as AttachmentIdentifier_SO;
            }
        }

        // If the Interactable was interacted, destroy on load, if not, load its visuals.
        public override void LoadedState()
        {
            if (this.alreadyInteracted) Destroy(this.gameObject);
            else GetVisuals();
        }
#endif
        #endregion
    }

#if UNITY_EDITOR

    [System.Serializable]
    [CustomEditor(typeof(WeaponPickeable))]
    public class WeaponPickeableEditor : Editor
    {
        private string[] tabs = { "Basic", "References", "Effects", "Events" };
        private int currentTab = 0;

        override public void OnInspectorGUI()
        {
            serializedObject.Update();
            WeaponPickeable myScript = target as WeaponPickeable;

            Texture2D myTexture = Resources.Load<Texture2D>("CustomEditor/WeaponPickeable_CustomEditor") as Texture2D;
            GUILayout.Label(myTexture);

            EditorGUILayout.BeginVertical();
            currentTab = GUILayout.Toolbar(currentTab, tabs);
            EditorGUILayout.Space(10f);
            EditorGUILayout.EndVertical();
            #region variables

            if (currentTab >= 0 || currentTab < tabs.Length)
            {
                switch (tabs[currentTab])
                {
                    case "Basic":
                        EditorGUILayout.LabelField("CUSTOMIZE YOUR WEAPON PICKEABLE", EditorStyles.boldLabel);
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("weapon"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("interactText"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("instantInteraction"));
                        break;
                    case "References":
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("image"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("graphics"));

                        break;
                    case "Effects":
                        EditorGUILayout.LabelField("EFFECTS", EditorStyles.boldLabel);
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("rotates"));
                        if (myScript.rotates) EditorGUILayout.PropertyField(serializedObject.FindProperty("rotationSpeed"));

                        EditorGUILayout.PropertyField(serializedObject.FindProperty("translates"));
                        if (myScript.translates) EditorGUILayout.PropertyField(serializedObject.FindProperty("translationSpeed"));
                        break;
                    case "Events":
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("events"));
                        break;

                }
            }

            #endregion

            serializedObject.ApplyModifiedProperties();

        }
    }
#endif
}
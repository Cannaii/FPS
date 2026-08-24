/// <summary>
/// This script belongs to cowsins� as a part of the cowsins� FPS Engine. All rights reserved. 
/// </summary>

using UnityEngine;
using System.Collections.Generic;
using System;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Presets;
#endif

#if INVENTORY_PRO_ADD_ON
using cowsins.Inventory;
#endif

namespace cowsins
{
    #region enum
    /// <summary>
    /// SHOOTING STYLE ENUMERATOR
    /// </summary>
    public enum ShootStyle
    {
        Hitscan,
        Projectile,
        Melee,
        Custom
    };

    public enum ShootingMethod
    {
        Press,
        PressAndHold,
        HoldAndRelease,
        HoldUntilReady
    };

    public enum ReloadingStyle
    {
        defaultReload, Overheat
    };

    #endregion

    #region others
    [System.Serializable]
    public class AudioClips
    {
        public AudioClip[] shooting; 
        public AudioClip holster, unholster, reload, emptyMagReload, emptyMagShoot;
    }
    [System.Serializable]
    public class BulletHoleImpactEntry
    {
        public string layerName;
        public GameObject bulletHoleImpact;
        [HideInInspector] public int cachedLayerIndex = -1;
    }

    [System.Serializable]
    public class BulletHoleImpact
    {
        public GameObject defaultImpact;
        public List<BulletHoleImpactEntry> bulletHoleImpact = new List<BulletHoleImpactEntry>();
    }


    [System.Serializable]
    public class LocomotionTransitionOverrides
    {
        public bool overrideTransitions;

        [Range(0.1f, 10f)] public float idleToWalkSpeed = 2f;

        [Range(0.1f, 10f)] public float walkToIdleSpeed = 2f;

        [Range(0.1f, 10f)] public float walkToRunSpeed = 2f;

        [Range(0.1f, 10f)] public float runToWalkSpeed = 2f;
    }

    #endregion

    #region weaponScriptableObject
    [CreateAssetMenu(fileName = "NewWeapon", menuName = "COWSINS/New Weapon", order = 1)]
    public class Weapon_SO : Item_SO
    {
        [Tooltip("Attach your weapon prefab here. This weapon prefab will be instantiated on your screen when you equip this weapon.")] public WeaponIdentification weaponObject;

        [Tooltip("Type of shooting. Hitscan = Instant hit on shooting." +
            "Projectile = spawn a bullet that travels through the world." +
            "Melee: Close range weapons such as swords or knives.")]
        public ShootStyle shootStyle;

        [Tooltip("Defines how Input is processed to shoot.")]
        public ShootingMethod shootMethod;

        [Tooltip("In order to shoot, you need to have a 100% progress value. Define how fast you want to reach 100% here.")] public float holdProgressSpeed;

        [Tooltip("The way the weapon reloads.")] public ReloadingStyle reloadStyle;
        [Tooltip("If true, any action (shoot, switch, drop) will cancel the reload process")] public bool allowCancelReload;

        [Tooltip("Type of ammo weapon will use.")] public BulletTypeIdentifier_SO bulletTypeIdentifier;

        [Tooltip("Your bullet objects")] public GameObject projectile;

        [Tooltip(" While true, the bullet will draw a parabola. ")] public bool projectileUsesGravity;

        [Tooltip("time to instantiate the projectile sicne you press shoot. Keep 0 if you want shooting to be instantly"), Min(0)] public float shootDelay;

        [Tooltip("Lifetime of the bullet")] public float bulletDuration;

        [Tooltip("Bullet velocity")] public float speed;

        [Tooltip("Does it explode when hits a target? If so, damage will depend on the distance between the target and the hit point")] public bool explosionOnHit;

        [Tooltip("Toggles the capacity of hurting youtself")] public bool hurtsPlayer;

        [Tooltip("Max reach of the explosion")] public float explosionRadius;

        [Min(0), Tooltip("Force applied to rigidbodies on explosion")] public float explosionForce;

        [Tooltip("VFX on explosion")] public GameObject explosionVFX;

        public bool continuousFire;

        [Tooltip("Time between each shot"), Min(.01f)] public float fireRate;

        [Tooltip("Time you will have to wait for the weapon to reload")] public float reloadTime;
        [Tooltip("Time you will have to wait for the weapon to reload when the magazine is empty")] public float emptyReloadTime = 1f;

        [Tooltip("If true, you can define a custom unholster time. If false, it will use the animation length")] public bool overrideUnholsterTime;
        [Tooltip("Time it takes to unholster the weapon")] public float unholsterTime;

        public float coolSpeed;

        [Range(.01f, .99f)] public float cooledPercentageAfterOverheat;

        [Tooltip("Turn it off to set a magazine size")] public bool infiniteBullets;

        [Tooltip("How many bullets you can have per magazine"), Min(1)] public int magazineSize = 1;

        [Tooltip("Bullets instantiated per each shot. Grants the possibility of making shotguns, burstguns etc. Amount of bullets spawned every shot.")]

        [Range(1,8)]public int bulletsPerFire; // Grants the possibility of making shotguns, burstguns etc. Amount of bullets spawned every shot.

        [Tooltip("How much ammo you lose per each fire ( & per each fire point )"), Min(0)]

        public int ammoCostPerFire;

        [Tooltip("How far the bullet is able to travel")] public int bulletRange;

        [Tooltip("Time elapsed until the next bullet spawns. Keep this at 0" +
            "in order to make shotguns." +
            "Change its value in order to make burst weapons.")]
        public float timeBetweenShots;

        // if bulletsPerFire != 1, then how much time do you wanna get for the next bullet to spawn ( while bullets spawned < bulletsPerFire ) 

        [Tooltip("How much the bullet is able to penetrate an object")] [Range(0, 10)] public float penetrationAmount;

        [Tooltip("Damage reduction multiplier. From 0 to 1 ( 0% to 100% )")] [Range(0, 1)] public float penetrationDamageReduction;

        [Tooltip("While true it grants the possibility of aiming the weapon")] public bool allowAim;

        [Tooltip("If true, the camera won´t be placed at the center of the aim point when aiming")] public bool ignoreAimPoint;

        [Tooltip("Distance from the camera when aiming")] public float aimDistance;

        [Tooltip("Final Rotation aiming")] public Vector3 aimingRotation;

        [Tooltip("Velocity to change between not aiming and aiming states")] [Range(1, 50)] public float aimingSpeed;

        [Tooltip("Camera field of view on aiming")] [Range(1, 179)] public float aimingFOV;

        [Tooltip("If true, you will be able to reload while aiming.")] public bool allowAimingIfReloading;

        [Tooltip("Player Movement Speed while Aiming"), Min(0f)] public float movementSpeedWhileAiming;

        [Tooltip("Sets a specific speed while aiming if true")] public bool setMovementSpeedWhileAiming;

        [Tooltip("Resize crosshair on shooting")] public float crosshairResize;

        public CrosshairParts crosshairParts = new CrosshairParts();

        [Tooltip("Apply spread per shot")] public bool applyBulletSpread;

        [Tooltip("How much spread is applied.")] [Range(0, .2f)] public float spreadAmount;

        [Tooltip("How much spread is applied while aiming.")] [Range(0, .2f)] public float aimSpreadAmount;

        [Tooltip("Apply recoil on shooting")] public bool applyRecoil;

        public AnimationCurve recoilX, recoilY;

        public float xRecoilAmount, yRecoilAmount;

        public float recoilRelaxSpeed;

        public bool applyDifferentRecoilOnAiming;

        public float xRecoilAmountOnAiming, yRecoilAmountOnAiming;

        [Tooltip("Damage dealt per bullet")] public float damagePerBullet;

        [Tooltip("Damage will decrease or increase depending on how far you are from the target")] public bool applyDamageReductionBasedOnDistance;

        [Tooltip("Damage reduction will be applied for distances larger than this"), Min(0)] public float minimumDistanceToApplyDamageReduction;

        [Tooltip("Adjust the damage reduction amount"), Range(.1f, 1)] public float damageReductionMultiplier;

        [Range(1, 3)] [Tooltip("Damage will get multiplied by this number when hitting a critical shot")] public float criticalDamageMultiplier;

        [Tooltip("Turn this true for a more realistic approach. It will stop using infinite magazines, so you will have to pick new magazines to add more bullets.")] public bool limitedMagazines;

        [Tooltip("Amount of initial magazines. (Full magazines, meaning that 2 magazines of 10 bullets each will result in having 20 initial bullets you can dispose o f)")] [Range(1, 100)] public int totalMagazines;

        [Tooltip("Spawn bullet shells to add realism")] public bool showBulletShells;

        [Tooltip("Graphics")] public BulletHoleImpact bulletHoleImpact;

        public GameObject muzzleVFX;

        public Rigidbody bulletGraphics;

        public bool useProceduralShot;

        public ProceduralShot_SO proceduralShotPattern;

        public AudioClips audioSFX;

        [Tooltip("Per each shot, pitch will be modified by a number between -pitchVariationFiringSFX and +pitchVariationFiringSFX"), Range(0, .2f)] public float pitchVariationFiringSFX;

        [Tooltip("If the player can grapple, and this is true, the grapple will be drawin from the first firePoint of the weapon")] public bool grapplesFromTip;

        [Tooltip("Cam Shake Amount on shoot"), Min(0)] public float camShakeAmount;

        [Tooltip("Cam Shake multiplier when aiming"), Range(0, 1)] public float camShakeAimMultiplier;

        [Tooltip("Cam Shake multiplier when crouching"), Range(0, 1)] public float camShakeCrouchMultiplier;

        [Tooltip(" If true it will apply a different FOV value when shooting to add an extra layer of detail. FOV will automatically lerp until it reaches its default value.")] public bool applyFOVEffectOnShooting;

        [Tooltip("FOV amount subtracted from the current fov on shooting"), Range(0, 180)] public float FOVValueToSubtract;

        [Tooltip("FOV amount subtracted from the current fov on shooting"), Range(0, 180)] public float AimingFOVValueToSubtract;

        [Tooltip("Amount of animations available to be played. A random shoot animation will be picked up each time"), Min(1)] public int amountOfShootAnimations;

        public TrailRenderer bulletTrail;

        //Melee Exclusive
        [Tooltip("Damage the melee weapon deals per hit")] [Range(0, 1000)] public float damagePerHit;

        [Range(0, 6), Tooltip("Attacking pace. The lower, the faster.")] public float attackRate;

        [Range(0, 2), Tooltip("Time to delay the melee hit")] public float hitDelay;

        [Tooltip("Per animation melee hit delays (index 0 -> shooting, 1 -> shooting2, ...)")] public float[] hitDelays;

        [Range(0, 5)]
        [Tooltip("How far you are able to land a hit")] public float attackRange;

        [HideInInspector] public bool dontShowMagazine;

        public LocomotionTransitionOverrides locomotionTransitions = new LocomotionTransitionOverrides();

#if UNITY_EDITOR
        public Preset currentPreset;
#endif

        public string presetName;

#if INVENTORY_PRO_ADD_ON
        public override void Use(InventoryProManager inventoryProManager, InventorySlot slot)
        {
            WeaponController weaponController = inventoryProManager._WeaponController;
            if (slot.IsInventorySlot)
            {
                if (weaponController.Inventory[weaponController.CurrentWeaponIndex] != null)
                {
                    ToastManager.Instance?.ShowToast(ToastManager.Instance.WeaponIsAlreadyUnholsteredMsg);
                    return;
                }
                SlotData slotData = slot.slotData;
                Weapon_SO weaponSO = (Weapon_SO)slotData.item;
                if (weaponSO == null) return;

                weaponController.InstantiateWeapon(weaponSO, weaponController.CurrentWeaponIndex, slotData.bulletsLeftInMagazine, slotData.totalBullets);
                inventoryProManager._GridGenerator.Operations.ClearSlotArea(slot);
                inventoryProManager._GridGenerator.UpdateHotbarSlot(weaponController.CurrentWeaponIndex, slotData.barrel, slotData.scope, slotData.stock, slotData.grip,
                    slotData.magazine, slotData.flashlight, slotData.laser);
            }
            else if (slot.IsHotbarSlot)
            {
                weaponController.CurrentWeaponIndex = slot.col;
                weaponController.weaponInventoryBehaviour.SelectWeapon(); 
            }
        }
#endif

    }
    #endregion

#if UNITY_EDITOR
    #region customEditor
    /// <summary>
    /// CUSTOM EDITOR STUFF
    /// </summary>
    [System.Serializable]
    [CustomEditor(typeof(Weapon_SO))]
    public class Weapon_SOEditor : Editor
    {
        private string[] tabs = { "Basic", "Shooting", "Stats", "Visuals", "Audio", "UI" };
        private int currentTab = 0;

        private bool isInventoryAddonAvailable;
        private bool showRecoilPlot = false;
        private bool showDefaultRecoilPlot = true;
        private bool showAimedRecoilPlot = true;
        private bool showLabels = true;
        private bool showCombinedPlot = true;
        private void OnEnable()
        {
            // Check if the Inventory Addon is available
#if INVENTORY_PRO_ADD_ON
                isInventoryAddonAvailable = true;
#else
                isInventoryAddonAvailable = false;
#endif
        }

        override public void OnInspectorGUI()
        {
            serializedObject.Update();
            Weapon_SO myScript = target as Weapon_SO;

            Texture2D myTexture = Resources.Load<Texture2D>("CustomEditor/weaponObject_CustomEditor") as Texture2D;
            GUILayout.Label(myTexture);

            EditorGUILayout.BeginVertical();
            EditorGUILayout.Space(10f);
            if (GUILayout.Button("Adding New Weapons Tutorial", GUILayout.Height(20)))
            {
                Application.OpenURL("https://www.cowsins.com/videos/OykOx8CgEMc");
            }
            EditorGUILayout.Space(10f);


            (bool hasMissingReferences, List<string> missingReferenceNames) = DetectMissingReferences(myScript);

            if (hasMissingReferences)
            {
                string missingReferencesString = string.Join(", ", missingReferenceNames.ToArray());
                EditorGUILayout.HelpBox("Missing references: " + missingReferencesString + ". Please fill in all required fields.", MessageType.Error);
            }




            EditorGUILayout.Space(10f);
            currentTab = GUILayout.Toolbar(currentTab, tabs);
            EditorGUILayout.Space(10f);
            EditorGUILayout.EndVertical();

            int style = (int)myScript.shootStyle;

            if (currentTab >= 0 || currentTab < tabs.Length)
            {
                switch (tabs[currentTab])
                {
                    case "Basic":
                        BeginBox("BASIC SETTINGS");
                        EditorGUILayout.Space(10f);
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("_name"));
                        EditorGUILayout.LabelField("This represents your first-person weapon in the game.", EditorStyles.helpBox);
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("weaponObject"));
                        EditorGUILayout.LabelField("This represents the graphics of your weapon on the ground.", EditorStyles.helpBox);
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("pickUpGraphics"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("icon"));
                        GUI.enabled = isInventoryAddonAvailable;
                        if (!isInventoryAddonAvailable)
                        {
                            EditorGUILayout.Space(10);
                            EditorGUILayout.HelpBox("These features are locked. Inventory Pro Manager + Save & Load Add-On is not installed.", MessageType.Info);
                            EditorGUILayout.Space(5);
                        }
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("itemSize"));
                        if (myScript.itemSize.x != myScript.itemSize.y)
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.Space(5);
                            EditorGUILayout.HelpBox($"Your item has an irregular size [{myScript.itemSize.x} : {myScript.itemSize.y}], which may cause icon stretching." +
                            "To maintain the correct aspect ratio, consider setting 'irregularItemIcon' with a properly scaled icon.", MessageType.Info);
                            EditorGUILayout.Space(5);
                            EditorGUILayout.PropertyField(serializedObject.FindProperty("irregularItemIcon"));
                            EditorGUI.indentLevel--;
                        }

                        GUI.enabled = true;
                        EditorGUILayout.Space(20f);
                        EditorGUILayout.BeginHorizontal();
                        {
                            GUILayout.FlexibleSpace();

                            Texture2D tex = AssetPreview.GetAssetPreview(myScript.icon);
                            GUILayout.Label("", GUILayout.Height(50), GUILayout.Width(50));
                            GUI.DrawTexture(GUILayoutUtility.GetLastRect(), tex);

                            GUILayout.FlexibleSpace();
                        }
                        EditorGUILayout.EndHorizontal();
                        EndBox();

                        break;
                    case "Shooting":
                        if (myScript.shootStyle == ShootStyle.Custom)
                        {
                            EditorGUILayout.Space(10f);
                            if (GUILayout.Button("Custom Shot Weapons Tutorial", GUILayout.Height(20)))
                            {
                                Application.OpenURL("https://www.cowsins.com/videos/OJ6uPo66Ams");
                            }
                            EditorGUILayout.Space(10f);
                        }
                        BeginBox("SHOOTING SETTINGS");

                        EditorGUILayout.PropertyField(serializedObject.FindProperty("shootStyle"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("shootMethod"));

                        if (myScript.shootMethod == ShootingMethod.HoldUntilReady || myScript.shootMethod == ShootingMethod.HoldAndRelease)
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.PropertyField(serializedObject.FindProperty("holdProgressSpeed"));
                            EditorGUI.indentLevel--;
                        }

                        EndBox();

                        switch (style)
                        {
                            case 0:
                                WeaponShootingSharedVariables(myScript);
                                break;
                            case 1:
                                BeginBox("PROJECTILE SETTINGS");
                                EditorGUILayout.PrefixLabel("Attach your Projectile here", EditorStyles.helpBox);
                                if (myScript.projectile != null && myScript.projectile.GetComponent<IBullet>() == null)
                                {
                                    EditorGUILayout.HelpBox($"Projectile prefab '{myScript.projectile.name}' does not have a component implementing IBullet!", MessageType.Error);
                                }
                                EditorGUILayout.PropertyField(serializedObject.FindProperty("projectile"));
                                EditorGUILayout.PropertyField(serializedObject.FindProperty("projectileUsesGravity"));
                                EditorGUILayout.PropertyField(serializedObject.FindProperty("speed"));
                                if (myScript.shootDelay > myScript.fireRate) myScript.shootDelay = myScript.fireRate;
                                EditorGUILayout.PropertyField(serializedObject.FindProperty("shootDelay"));

                                if (myScript.shootStyle == ShootStyle.Projectile && myScript.bulletDuration <= 0f)
                                {
                                    EditorGUILayout.HelpBox("Bullet Duration must be greater than 0 when using Projectile shoot style, otherwise the bullet will be destroyed immediately.", MessageType.Error);
                                }
                                
                                EditorGUILayout.PropertyField(serializedObject.FindProperty("bulletDuration"));
                                EditorGUILayout.PropertyField(serializedObject.FindProperty("hurtsPlayer"));
                                EditorGUILayout.PropertyField(serializedObject.FindProperty("explosionOnHit"));
                                if (myScript.explosionOnHit)
                                {
                                    EditorGUI.indentLevel++;
                                    EditorGUILayout.PropertyField(serializedObject.FindProperty("explosionRadius"));
                                    EditorGUILayout.PropertyField(serializedObject.FindProperty("explosionForce"));
                                    EditorGUILayout.LabelField("Check new options under 'Visuals' tab. ", EditorStyles.helpBox);
                                    EditorGUI.indentLevel--;
                                }
                                EndBox();
                                WeaponShootingSharedVariables(myScript);
                                EditorGUILayout.Space(5f);
                                break;
                            case 2:
                                BeginBox("MELEE OPTIONS");
                                EditorGUILayout.Space(2f);
                                var attackRangeProperty = serializedObject.FindProperty("attackRange");
                                EditorGUILayout.PropertyField(attackRangeProperty);
                                var attackRateProperty = serializedObject.FindProperty("attackRate");
                                EditorGUILayout.PropertyField(attackRateProperty);
                                var hitDelayProp = serializedObject.FindProperty("hitDelay");
                                var hitDelaysProp = serializedObject.FindProperty("hitDelays");

                                if (myScript.amountOfShootAnimations <= 1)
                                {
                                    EditorGUILayout.PropertyField(hitDelayProp);
                                    if (hitDelaysProp.arraySize != 0)
                                        hitDelaysProp.arraySize = 0;
                                }
                                else
                                {
                                    if (hitDelaysProp.arraySize != myScript.amountOfShootAnimations)
                                        hitDelaysProp.arraySize = myScript.amountOfShootAnimations;
                                    EditorGUILayout.PropertyField(hitDelaysProp, new GUIContent("Per-Attack Hit Delays"), true);
                                    if (hitDelayProp.floatValue != 0f)
                                        hitDelayProp.floatValue = 0f;
                                }
                                EndBox();
                                break;
                            case 3:
                                BeginBox("CUSTOM SHOT WEAPONS OPTIONS");
                                EditorGUILayout.PropertyField(serializedObject.FindProperty("continuousFire"));
                                if (!myScript.continuousFire)
                                {
                                    EditorGUI.indentLevel++;
                                    EditorGUILayout.PropertyField(serializedObject.FindProperty("fireRate"));
                                    EditorGUI.indentLevel--;
                                }
                                else EditorGUILayout.LabelField("Continuous Fire will call your custom method ONCE per frame.", EditorStyles.helpBox);
                                EndBox();
                                break;
                        }
                        break;
                    case "Stats":
                        switch (style)
                        {
                            case 0:
                                WeaponStatsSharedVariables(myScript);
                                break;
                            case 1:
                                WeaponStatsSharedVariables(myScript);
                                break;
                            case 2:
                                BeginBox("MELEE STATS");
                                EditorGUILayout.PropertyField(serializedObject.FindProperty("damagePerHit"));
                                EditorGUILayout.PropertyField(serializedObject.FindProperty("criticalDamageMultiplier")); 
                                
                                var applyWeightProperty = serializedObject.FindProperty("applyWeight");
                                EditorGUILayout.PropertyField(applyWeightProperty);

                                using (var group = new EditorGUILayout.FadeGroupScope(Convert.ToSingle(myScript.applyWeight)))
                                {
                                    if (group.visible == true)
                                    {
                                        EditorGUI.indentLevel++;
                                        var weightMultiplierProperty = serializedObject.FindProperty("weightMultiplier");
                                        EditorGUILayout.PropertyField(weightMultiplierProperty);
                                        EditorGUI.indentLevel--;
                                    }
                                }
                                if (!myScript.applyWeight) myScript.weightMultiplier = 1;
                                EndBox();   
                                break;
                            case 3:
                                DrawAimSettings(myScript);
                                break;
                        }
                        break;
                    case "Visuals":
                        switch (style)
                        {
                            case 0:
                                EditorGUILayout.Space(5f);
                                BeginBox("PROCEDURAL ANIMATIONS");
                                EditorGUILayout.PropertyField(serializedObject.FindProperty("useProceduralShot"));
                                if (myScript.useProceduralShot)
                                {
                                    EditorGUI.indentLevel++;
                                    EditorGUILayout.PropertyField(serializedObject.FindProperty("proceduralShotPattern"));
                                    EditorGUI.indentLevel--;
                                }
                                EndBox();
                                WeaponVisualSharedVariables(myScript);
                                break;
                            case 1:
                                BeginBox("PROCEDURAL ANIMATIONS");
                                GUILayout.Box("", new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.Height(8) });
                                EditorGUILayout.PropertyField(serializedObject.FindProperty("useProceduralShot"));
                                if (myScript.useProceduralShot)
                                {
                                    EditorGUI.indentLevel++;
                                    EditorGUILayout.PropertyField(serializedObject.FindProperty("proceduralShotPattern"));
                                    EditorGUI.indentLevel--;
                                }
                                EndBox();
                                WeaponVisualSharedVariables(myScript);
                                if (myScript.explosionOnHit)
                                {
                                    BeginBox("");
                                    EditorGUILayout.PropertyField(serializedObject.FindProperty("explosionVFX"));
                                    EndBox();
                                }
                                break;
                            case 2:
                                BeginBox("CAMERA");
                                EditorGUILayout.PropertyField(serializedObject.FindProperty("camShakeAmount"));
                                EditorGUILayout.PropertyField(serializedObject.FindProperty("applyFOVEffectOnShooting"));
                                if (myScript.applyFOVEffectOnShooting)
                                {
                                    EditorGUI.indentLevel++;
                                    EditorGUILayout.PropertyField(serializedObject.FindProperty("FOVValueToSubtract"));
                                    EditorGUI.indentLevel--;
                                }
                                EndBox();
                                BeginBox("ANIMATIONS");
                                EditorGUILayout.PropertyField(serializedObject.FindProperty("amountOfShootAnimations"));
                                EndBox();
                                BeginBox("IMPACTS");
                                EditorGUILayout.PropertyField(serializedObject.FindProperty("bulletHoleImpact"));
                                EndBox();
                                break;
                             case 3:
                                BeginBox("CAMERA");
                                EditorGUILayout.PropertyField(serializedObject.FindProperty("camShakeAmount"));
                                break;
                        }
                        break;
                    case "Audio":
                        WeaponAudioSharedVariables(myScript);
                        break;

                    case "UI":
                        WeaponUISharedVariables(myScript);
                        if (style == 2) myScript.allowAim = false;

                        break;

                }

                EditorGUILayout.Space(10f);
                EditorGUILayout.LabelField("PRESETS", EditorStyles.boldLabel);
                GUILayout.Box("", new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.Height(8) });
                EditorGUILayout.Space(2f);

                EditorGUILayout.PropertyField(serializedObject.FindProperty("currentPreset"));
                EditorGUILayout.Space(2f);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("presetName"));
                EditorGUILayout.Space(5f);
                if (GUILayout.Button("Save Settings as a Preset")) CowsinsUtilities.SavePreset(myScript, myScript.presetName);

                EditorGUILayout.Space(5f);

                if (GUILayout.Button("Apply Current Preset"))
                {
                    if (myScript.currentPreset != null) CowsinsUtilities.ApplyPreset(myScript.currentPreset, myScript);
                    else Debug.LogError("Can�t apply a non existing preset. Please, assign your desired preset to 'currentPreset'. ");
                }
            }
            if ((int)myScript.reloadStyle == 1) myScript.dontShowMagazine = true;
            else myScript.dontShowMagazine = false;


            EditorGUILayout.Space(20f);
            EditorGUILayout.BeginHorizontal();
            {
                GUILayout.FlexibleSpace();

                // LOCK ICON 

                GUI.backgroundColor = Color.clear;

                btnTexture = ActiveEditorTracker.sharedTracker.isLocked
                    ? Resources.Load<Texture2D>("CustomEditor/lockedIcon") as Texture2D
                    : Resources.Load<Texture2D>("CustomEditor/unlockedIcon") as Texture2D;

                button = new GUIContent(btnTexture);

                if (GUILayout.Button(button, GUIStyle.none, GUILayout.Width(lockIconSize), GUILayout.Height(lockIconSize)))
                    ActiveEditorTracker.sharedTracker.isLocked = !ActiveEditorTracker.sharedTracker.isLocked;

                GUILayout.FlexibleSpace();
            }
            EditorGUILayout.EndHorizontal();



            serializedObject.ApplyModifiedProperties();
        }
        private float lockIconSize = 40;
        private Texture btnTexture;
        private GUIContent button;
        private void WeaponUISharedVariables(Weapon_SO myScript)
        {
            BeginBox("USER INTERFACE (UI)");

            EditorGUILayout.PropertyField(serializedObject.FindProperty("crosshairResize"));

            CrosshairParts crosshairParts = myScript.crosshairParts;
            if (!crosshairParts.topPart && !crosshairParts.downPart && !crosshairParts.leftPart && !crosshairParts.rightPart && !crosshairParts.center)
                EditorGUILayout.HelpBox($"Crosshair will not be visible because there are no parts selected. If this is the intended behaviour, ignore this message.", MessageType.Warning);

            CowsinsEditorWindowUtilities.DrawCrosshairEditorSection(serializedObject, myScript.crosshairParts, "crosshairParts");

            EndBox();   

        }
        private void WeaponAudioSharedVariables(Weapon_SO myScript)
        {
            BeginBox("AUDIO");
            var audioSFXProperty = serializedObject.FindProperty("audioSFX");
            EditorGUILayout.PropertyField(audioSFXProperty);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("pitchVariationFiringSFX"));
            EndBox();
        }

        private void WeaponShootingSharedVariables(Weapon_SO myScript)
        {
            BeginBox("WEAPON SHOOTING SETTINGS");

            if (myScript.shootMethod != ShootingMethod.HoldUntilReady && myScript.shootMethod != ShootingMethod.HoldAndRelease)
            {
                EditorGUI.indentLevel++;
                float rpm = 60 / myScript.fireRate;
                EditorGUILayout.HelpBox($"RPM ( Revolutions Per Minute ): {rpm:F0}rpm", myScript.fireRate > 0 ? MessageType.Info : MessageType.Error);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("fireRate"));
                EditorGUI.indentLevel--;
            }

            var bulletRangeProperty = serializedObject.FindProperty("bulletRange");
            EditorGUILayout.PropertyField(bulletRangeProperty);

            var bulletsPerFireProperty = serializedObject.FindProperty("bulletsPerFire");
            EditorGUILayout.PropertyField(bulletsPerFireProperty);

            if (myScript.bulletsPerFire > 1)
            {
                EditorGUI.indentLevel++;
                var timeBetweenShotsProperty = serializedObject.FindProperty("timeBetweenShots");
                EditorGUILayout.PropertyField(timeBetweenShotsProperty);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.PropertyField(serializedObject.FindProperty("ammoCostPerFire"));
            if (myScript.ammoCostPerFire > myScript.bulletsPerFire) myScript.ammoCostPerFire = myScript.bulletsPerFire;

            EndBox();

            BeginBox("BULLET SPREAD");

            var applyBulletSpreadProperty = serializedObject.FindProperty("applyBulletSpread");
            EditorGUILayout.PropertyField(applyBulletSpreadProperty);

            using (var group = new EditorGUILayout.FadeGroupScope(Convert.ToSingle(myScript.applyBulletSpread)))
            {

                if (group.visible == true)
                {
                    EditorGUI.indentLevel++;
                    var spreadAmountProperty = serializedObject.FindProperty("spreadAmount");
                    EditorGUILayout.PropertyField(spreadAmountProperty);
                    if (myScript.allowAim) EditorGUILayout.PropertyField(serializedObject.FindProperty("aimSpreadAmount"));
                    EditorGUI.indentLevel--;
                }
            }
            if (!myScript.applyBulletSpread) myScript.spreadAmount = 0;

            EndBox();

            BeginBox("RECOIL");

            EditorGUILayout.PropertyField(serializedObject.FindProperty("applyRecoil"));

            if (myScript.applyRecoil)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("recoilX"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("recoilY"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("xRecoilAmount"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("yRecoilAmount"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("recoilRelaxSpeed"));
                if (myScript.allowAim)
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("applyDifferentRecoilOnAiming"));
                    if (myScript.applyDifferentRecoilOnAiming)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("xRecoilAmountOnAiming"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("yRecoilAmountOnAiming"));
                        EditorGUI.indentLevel--;
                    }
                }
                GUILayout.Space(5);

                if(GUILayout.Button(showRecoilPlot ? "Hide Recoil Plot" : "Show Recoil Plot")) showRecoilPlot = !showRecoilPlot;

                if(showRecoilPlot) PlotRecoil(myScript);

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(5f);
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();


            if (myScript.shootStyle != ShootStyle.Projectile)
            {
                BeginBox("PENETRATION (WALLBANG)");
                var penetrationAmountProperty = serializedObject.FindProperty("penetrationAmount");
                EditorGUILayout.PropertyField(penetrationAmountProperty);
                if (myScript.penetrationAmount != 0)
                {
                    EditorGUI.indentLevel++;
                    var penetrationDamageReductionProperty = serializedObject.FindProperty("penetrationDamageReduction");
                    EditorGUILayout.PropertyField(penetrationDamageReductionProperty);
                    EditorGUI.indentLevel--;
                }
                EndBox();
            }
        }

        private void WeaponStatsSharedVariables(Weapon_SO myScript)
        {
            EditorGUILayout.LabelField("WEAPON STATS", EditorStyles.boldLabel);
            EditorGUILayout.Space(5f);

            var limitedMagazinesProperty = serializedObject.FindProperty("limitedMagazines");
            var reloadStyleProperty = serializedObject.FindProperty("reloadStyle");
            var allowCancelReloadProperty = serializedObject.FindProperty("allowCancelReload");

            BeginBox("AMMO & MAGAZINES");

            if (myScript.dontShowMagazine == false)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("infiniteBullets"));
                if (!myScript.infiniteBullets)
                {
                    EditorGUI.indentLevel++;
                    var magazineSizeProperty = serializedObject.FindProperty("magazineSize");
                    EditorGUILayout.PropertyField(magazineSizeProperty);

                    if(limitedMagazinesProperty.boolValue && reloadStyleProperty.enumValueIndex == 0) 
                    EditorGUILayout.LabelField("Bullet Type Identifier unlocked. Check under Reload options. ", EditorStyles.helpBox);
                    EditorGUILayout.PropertyField(limitedMagazinesProperty);
                    EditorGUI.indentLevel--;
                    using (var group = new EditorGUILayout.FadeGroupScope(Convert.ToSingle(myScript.limitedMagazines)))
                    {

                        if (group.visible == true)
                        {
                            EditorGUI.indentLevel++;
                            var totalMagazinesProperty = serializedObject.FindProperty("totalMagazines");
                            EditorGUILayout.PropertyField(totalMagazinesProperty);
                            EditorGUI.indentLevel--;
                        }
                    }
                }
            }
            else
            {
                EditorGUI.indentLevel++;
                myScript.limitedMagazines = false;
                myScript.infiniteBullets = false;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("magazineSize"));
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("coolSpeed"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("cooledPercentageAfterOverheat"));
                EditorGUI.indentLevel--;
                EditorGUI.indentLevel--;
            }

            EndBox();   

            BeginBox("RELOAD");

            EditorGUILayout.PropertyField(reloadStyleProperty);
            EditorGUILayout.PropertyField(allowCancelReloadProperty);

            var bulletTypeIdentifierProperty = serializedObject.FindProperty("bulletTypeIdentifier");

            if(reloadStyleProperty.enumValueIndex == 0 && limitedMagazinesProperty.boolValue == true)
            {
                EditorGUI.indentLevel++;
                if(bulletTypeIdentifierProperty.objectReferenceValue == null )
                {
                    EditorGUILayout.HelpBox($"You´re using Universal Bullets Type. Leave Bullet Type Identifier unassigned if this is intended.", MessageType.Info);
                }
                EditorGUILayout.PropertyField(bulletTypeIdentifierProperty);
                EditorGUI.indentLevel--;
            }
            else
            {
                bulletTypeIdentifierProperty.objectReferenceValue = null;
            }

            Animator animator = null;
            float suggestedReload = 0f; bool hasSuggestedReload = false;
            float suggestedEmptyReload = 0f; bool hasSuggestedEmptyReload = false;
            if (animator == null && myScript.weaponObject != null)
            {
                animator = myScript.weaponObject.GetComponentInChildren<Animator>();
                if (animator != null)
                {
                    (bool success, float length) = CowsinsUtilities.CheckClipAvailability(animator, "reloading");
                    if (success) { suggestedReload = length; hasSuggestedReload = true; }

                    (bool emptySuccess, float emptyLength) = CowsinsUtilities.CheckClipAvailability(animator, "empty_reloading");
                    if (emptySuccess) { suggestedEmptyReload = emptyLength; hasSuggestedEmptyReload = true; }

                    (bool holsterSuccess, float holsterLength) = CowsinsUtilities.CheckClipAvailability(animator, "Holster");
                    (bool unholsterSuccess, float unholsterLength) = CowsinsUtilities.CheckClipAvailability(animator, "Unholster");
                }
            }

            if (hasSuggestedReload)
                EditorGUILayout.HelpBox($"Suggested reload time (from 'reloading' clip): {suggestedReload:F2} s", MessageType.Info);
            var reloadTimeProperty = serializedObject.FindProperty("reloadTime");
            EditorGUILayout.PropertyField(reloadTimeProperty);

            if (hasSuggestedEmptyReload)
                EditorGUILayout.HelpBox($"Suggested empty-reload time (from 'empty_reloading' clip): {suggestedEmptyReload:F2} s", MessageType.Info);

            var emptyReloadTimeProp = serializedObject.FindProperty("emptyReloadTime");
            if (Mathf.Approximately(emptyReloadTimeProp.floatValue, 0f))
                emptyReloadTimeProp.floatValue = reloadTimeProperty.floatValue;
            EditorGUILayout.PropertyField(emptyReloadTimeProp, new GUIContent("Empty Reload Time"));
            
            EndBox();

            BeginBox("UNHOLSTER");


            var overrideUnholsterTimeProperty = serializedObject.FindProperty("overrideUnholsterTime");
            EditorGUILayout.PropertyField(overrideUnholsterTimeProperty);

            if (myScript.overrideUnholsterTime)
            {
                EditorGUI.indentLevel++;
                if (myScript.weaponObject != null)
                {
                    animator = myScript.weaponObject.GetComponentInChildren<Animator>();
                    if (animator != null)
                    {
                        (bool unholsterSuccess, float unholsterLength) = CowsinsUtilities.CheckClipAvailability(animator, "Unholster");
                        if (unholsterSuccess) EditorGUILayout.HelpBox($"Suggested unholster time (from 'unholster' clip): {unholsterLength:F2} s", MessageType.Info);
                    }
                }
                EditorGUILayout.PropertyField(serializedObject.FindProperty("unholsterTime"));
                EditorGUI.indentLevel--;
            }
            EndBox();

            BeginBox("DAMAGE");

            float dps = myScript.damagePerBullet / myScript.fireRate;
            float criticalDps = myScript.damagePerBullet * myScript.criticalDamageMultiplier / myScript.fireRate;
            EditorGUILayout.HelpBox($"DPS (DAMAGE PER SECOND): {dps:F2} \nCRITICAL DPS: {criticalDps:F2}", MessageType.Info);

            var damagePerBulletProperty = serializedObject.FindProperty("damagePerBullet");
            EditorGUILayout.PropertyField(damagePerBulletProperty);

            EditorGUILayout.PropertyField(serializedObject.FindProperty("criticalDamageMultiplier"));

            if (myScript.shootStyle == ShootStyle.Hitscan)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("applyDamageReductionBasedOnDistance"));
                if (myScript.applyDamageReductionBasedOnDistance)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("minimumDistanceToApplyDamageReduction"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("damageReductionMultiplier"));
                    EditorGUI.indentLevel--;
                }
            }
            EndBox();

            DrawAimSettings(myScript);

            BeginBox("ITEM WEIGHT");

            var applyWeightProperty = serializedObject.FindProperty("applyWeight");
            EditorGUILayout.PropertyField(applyWeightProperty);


            using (var group = new EditorGUILayout.FadeGroupScope(Convert.ToSingle(myScript.applyWeight)))
            {
                if (group.visible == true)
                {
                    var weightMultiplierProperty = serializedObject.FindProperty("weightMultiplier");
                    EditorGUILayout.PropertyField(weightMultiplierProperty);
                }
            }
            if (!myScript.applyWeight) myScript.weightMultiplier = 1;

            EndBox();
        }

        private void WeaponVisualSharedVariables(Weapon_SO myScript)
        {
            BeginBox("GRAPPLE VISUALS");
            EditorGUILayout.PropertyField(serializedObject.FindProperty("grapplesFromTip"));
            EndBox();

            BeginBox("CAMERA");
            EditorGUILayout.PropertyField(serializedObject.FindProperty("camShakeAmount"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("camShakeCrouchMultiplier"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("camShakeAimMultiplier"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("applyFOVEffectOnShooting"));
            if (myScript.applyFOVEffectOnShooting)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("FOVValueToSubtract"));
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Your normal aiming FOV value is " + myScript.aimingFOV, EditorStyles.helpBox);
                EditorGUI.indentLevel--;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("AimingFOVValueToSubtract"));
                EditorGUI.indentLevel--;
            }

            EndBox();

            BeginBox("LOCOMOTION TRANSITIONS");
            var locomotionTransitions = serializedObject.FindProperty("locomotionTransitions");
            var overrideTransitions = locomotionTransitions.FindPropertyRelative("overrideTransitions");
            EditorGUILayout.PropertyField(overrideTransitions);

            using (var group = new EditorGUILayout.FadeGroupScope(Convert.ToSingle(overrideTransitions.boolValue)))
            {
                if (group.visible == true)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(locomotionTransitions.FindPropertyRelative("idleToWalkSpeed"));
                    EditorGUILayout.PropertyField(locomotionTransitions.FindPropertyRelative("walkToIdleSpeed"));
                    EditorGUILayout.PropertyField(locomotionTransitions.FindPropertyRelative("walkToRunSpeed"));
                    EditorGUILayout.PropertyField(locomotionTransitions.FindPropertyRelative("runToWalkSpeed"));
                    EditorGUI.indentLevel--;
                }
            }
            EndBox();

            BeginBox("BULLET SHELLS");
            var showBulletShellsProperty = serializedObject.FindProperty("showBulletShells");
            EditorGUILayout.PropertyField(showBulletShellsProperty);
            if (myScript.shootStyle == ShootStyle.Melee) myScript.showBulletShells = false;

            if (myScript.bulletsPerFire == 1) myScript.timeBetweenShots = 0;

            using (var group = new EditorGUILayout.FadeGroupScope(Convert.ToSingle(myScript.showBulletShells)))
            {
                if (group.visible == true)
                {
                    EditorGUI.indentLevel++;
                    var bulletGraphicsProperty = serializedObject.FindProperty("bulletGraphics");
                    EditorGUILayout.PropertyField(bulletGraphicsProperty);
                    EditorGUI.indentLevel--;
                }
            }

            if (myScript.explosionOnHit && myScript.shootStyle == ShootStyle.Projectile)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("explosionVFX"));
                EditorGUI.indentLevel--;
            }

            if (myScript.shootStyle != ShootStyle.Projectile)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("bulletHoleImpact"));
                EditorGUI.indentLevel--;
            }
            var MuzzleVFXProperty = serializedObject.FindProperty("muzzleVFX");
            EditorGUILayout.PropertyField(MuzzleVFXProperty);
            EditorGUILayout.LabelField("Leave this Bullet Trail Unassigned if you do not want to use Bullet Trails. No errors will show up.", EditorStyles.helpBox);
            var bulletTrailProperty = serializedObject.FindProperty("bulletTrail");
            EditorGUILayout.PropertyField(bulletTrailProperty);

            EndBox();
        }

        private (bool, List<string>) DetectMissingReferences(Weapon_SO script)
        {
            var missingReferences = new List<string>();

            Dictionary<string, bool> references = new Dictionary<string, bool>
            {
                { "weaponObject [Basic Tab]", script.weaponObject == null },
                { "pickUpGraphics [Basic Tab]", script.pickUpGraphics == null },
                { "icon [Basic Tab]", script.icon == null },
                { "projectile [Shooting Tab]", script.shootStyle == ShootStyle.Projectile && script.projectile == null },
                { "proceduralShotPattern [Visuals Tab]", script.useProceduralShot && script.proceduralShotPattern == null },
                { "bulletGraphics [Visuals Tab]", script.showBulletShells && script.bulletGraphics == null && script.shootStyle != ShootStyle.Melee },
                { "firingSFX [Audio Tab]", script.audioSFX?.shooting == null || script.audioSFX?.shooting?.Length <= 0},
                { "holsterSFX [Audio Tab]", script.audioSFX?.holster == null },
                { "unholsterSFX [Audio Tab]", script.audioSFX?.unholster == null },
                { "reloadSFX [Audio Tab]", script.shootStyle != ShootStyle.Melee && !script.infiniteBullets && script.reloadStyle == ReloadingStyle.defaultReload && script.audioSFX?.reload == null },
                { "emptyMagReloadSFX [Audio Tab]", script.shootStyle != ShootStyle.Melee && !script.infiniteBullets && script.reloadStyle == ReloadingStyle.defaultReload && script.audioSFX?.emptyMagReload == null },
                { "emptyMagShootSFX [Audio Tab]", script.shootStyle != ShootStyle.Melee && !script.infiniteBullets && script.reloadStyle == ReloadingStyle.defaultReload && script.audioSFX?.emptyMagShoot == null },
                { "Bullet Duration can´t be 0 [Shooting Tab]", script.shootStyle == ShootStyle.Projectile && script.bulletDuration == 0 },
            };

            foreach (var reference in references)
            {
                if (reference.Value)
                {
                    missingReferences.Add(reference.Key);
                }
            }

            return (missingReferences.Count > 0, missingReferences);
        }

        private void BeginBox(string title)
        {
            EditorGUILayout.BeginVertical(GUI.skin.GetStyle("HelpBox"));
            EditorGUI.indentLevel++;
            EditorGUILayout.Space(5f);
            if (!string.IsNullOrEmpty(title)) EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }

        private void EndBox()
        {
            EditorGUILayout.Space(10f);
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }

        private void PlotRecoil(Weapon_SO myScript)
        {
            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(GUILayout.Width(140));

            if (GUILayout.Button(showDefaultRecoilPlot ? "Hide Default Recoil Plot" : "Show Default Recoil Plot", GUILayout.Height(30)))
                showDefaultRecoilPlot = !showDefaultRecoilPlot;
            if (GUILayout.Button(showAimedRecoilPlot ? "Hide Aimed Recoil Plot" : "Show Aimed Recoil Plot", GUILayout.Height(30)))
                showAimedRecoilPlot = !showAimedRecoilPlot;
            if (GUILayout.Button(showCombinedPlot ? "Hide Combined Plot" : "Show Combined Plot", GUILayout.Height(30)))
                showCombinedPlot = !showCombinedPlot;
            if (GUILayout.Button(showLabels ? "Hide Labels" : "Show Labels", GUILayout.Height(30)))
                showLabels = !showLabels;
            GUILayout.Label($"Simulated Shots: {myScript.magazineSize}");
            GUILayout.Label($"X Recoil Reduction Ratio: {(myScript.applyDifferentRecoilOnAiming ? (myScript.xRecoilAmount / myScript.xRecoilAmountOnAiming) : 1):F2}");
            GUILayout.Label($"X Recoil Reduction Ratio: {(myScript.applyDifferentRecoilOnAiming ? (myScript.yRecoilAmount / myScript.yRecoilAmountOnAiming) : 1):F2}");
            GUILayout.EndVertical();

            GUILayout.BeginHorizontal();

            if (showDefaultRecoilPlot)
            {
                GUILayout.BeginVertical(GUILayout.Width(200));
                CowsinsEditorWindowUtilities.DrawRecoilPlot(myScript, false, "Default Recoil", false, showLabels);
                GUILayout.EndVertical();
            }

            if (showAimedRecoilPlot)
            {
                GUILayout.BeginVertical(GUILayout.Width(200));
                CowsinsEditorWindowUtilities.DrawRecoilPlot(myScript, myScript.applyDifferentRecoilOnAiming, "Aimed Recoil", true, showLabels);
                GUILayout.EndVertical();
            }

            if (showCombinedPlot)
            {
                GUILayout.BeginVertical(GUILayout.Width(200));
                CowsinsEditorWindowUtilities.DrawCombinedRecoilPlot(myScript, showLabels);
                GUILayout.EndVertical();
            }

            GUILayout.EndHorizontal();

            GUILayout.EndHorizontal();
        }

        private void DrawAimSettings(Weapon_SO myScript)
        {
            BeginBox("AIM / ADS SETTINGS");

            var allowAimProperty = serializedObject.FindProperty("allowAim");
            EditorGUILayout.PropertyField(allowAimProperty);

            EditorGUILayout.PropertyField(serializedObject.FindProperty("ignoreAimPoint"));

            using (var group = new EditorGUILayout.FadeGroupScope(Convert.ToSingle(myScript.allowAim)))
            {
                if (group.visible == true)
                {
                    EditorGUI.indentLevel++;
                    if (myScript.applyBulletSpread)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.LabelField("Check new options under spread options. ", EditorStyles.helpBox);
                        EditorGUI.indentLevel--;
                    }
                    EditorGUI.indentLevel++;
                    var aimingPositionProperty = serializedObject.FindProperty("aimDistance");
                    EditorGUILayout.PropertyField(aimingPositionProperty);
                    var aimingRotationProperty = serializedObject.FindProperty("aimingRotation");
                    EditorGUILayout.PropertyField(aimingRotationProperty);
                    var aimingSpeedProperty = serializedObject.FindProperty("aimingSpeed");
                    EditorGUILayout.PropertyField(aimingSpeedProperty);
                    var aimingFOVProperty = serializedObject.FindProperty("aimingFOV");
                    EditorGUILayout.PropertyField(aimingFOVProperty);

                    EditorGUILayout.PropertyField(serializedObject.FindProperty("allowAimingIfReloading"));

                    EditorGUILayout.PropertyField(serializedObject.FindProperty("setMovementSpeedWhileAiming"));

                    if (myScript.setMovementSpeedWhileAiming)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("movementSpeedWhileAiming"));
                        EditorGUI.indentLevel--;
                    }

                    EditorGUI.indentLevel--;
                }
            }
            EndBox();
        }
    }

    #endregion
#endif
}
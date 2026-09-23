#if UNITY_EDITOR
/// <summary>
/// This script belongs to cowsins� as a part of the cowsins� FPS Engine. All rights reserved. 
/// </summary>
using UnityEditor;
using UnityEngine;

namespace cowsins
{
    [System.Serializable]
    [CustomEditor(typeof(WeaponController))]
    public class WeaponControllerEditor : Editor
    {
        private string[] tabs = { "Inventory", "References", "Variables", "Secondary Attack", "Effects", "Events" };
        private int currentTab = 0;

        override public void OnInspectorGUI()
        {
            serializedObject.Update();
            WeaponController myScript = target as WeaponController;


            Texture2D myTexture = Resources.Load<Texture2D>("CustomEditor/weaponController_CustomEditor") as Texture2D;
            GUILayout.Label(myTexture);

            EditorGUILayout.BeginVertical();
            currentTab = GUILayout.Toolbar(currentTab, tabs);
            EditorGUILayout.Space(10f);
            EditorGUILayout.EndVertical();

            if (currentTab >= 0 || currentTab < tabs.Length)
            {
                SerializedProperty settings = serializedObject.FindProperty("settings");

                switch (tabs[currentTab])
                {
                    case "Inventory":
                        EditorGUILayout.LabelField("INVENTORY", EditorStyles.boldLabel);

                        EditorGUILayout.PropertyField(settings.FindPropertyRelative("inventorySize"));
                        int inventorySize = settings.FindPropertyRelative("inventorySize").intValue;

                        EditorGUILayout.Space(5);
                        EditorGUILayout.LabelField("Select the weapons you want to spawn with", EditorStyles.helpBox);
                        EditorGUILayout.Space(5);

                        SerializedProperty initialWeapons = settings.FindPropertyRelative("initialWeapons");

                        if (initialWeapons.arraySize > inventorySize)
                        {
                            initialWeapons.arraySize = inventorySize;
                        }

                        EditorGUILayout.PropertyField(initialWeapons, true);

                        if (initialWeapons.arraySize >= inventorySize)
                        {
                            EditorGUILayout.LabelField(
                                "You can�t add more initial weapons. This array can�t be bigger than the inventory size",
                                EditorStyles.helpBox
                            );
                        }
                        EditorGUILayout.PropertyField(settings.FindPropertyRelative("allowMouseWheelWeaponSwitch"));
                        EditorGUILayout.PropertyField(settings.FindPropertyRelative("allowNumberKeyWeaponSwitch"));
                        break;
                    case "References":
                        EditorGUILayout.LabelField("REFERENCES", EditorStyles.boldLabel);
                        //var weaponProperty = serializedObject.FindProperty("weapon");
                        //EditorGUILayout.PropertyField(weaponProperty);
                        SerializedProperty weaponControllerReferences = serializedObject.FindProperty("weaponControllerReferences");
                        EditorGUILayout.PropertyField(settings.FindPropertyRelative("mainCamera"));
                        EditorGUILayout.PropertyField(settings.FindPropertyRelative("cameraPivot"));
                        EditorGUILayout.PropertyField(settings.FindPropertyRelative("weaponHolder"));
                        break;
                    case "Variables":
         
                        EditorGUILayout.Space(10f);
                        EditorGUILayout.LabelField("RELOAD", EditorStyles.boldLabel);
                        EditorGUILayout.Space(2f);
                        SerializedProperty autoReloadProp = settings.FindPropertyRelative("autoReload");
                        bool autoReload = autoReloadProp.boolValue;

                        EditorGUILayout.PropertyField(autoReloadProp);

                        if(autoReload)
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.PropertyField(settings.FindPropertyRelative("autoReloadDelay"));
                            EditorGUI.indentLevel--;
                        }
                        EditorGUILayout.PropertyField(settings.FindPropertyRelative("allowReloadWhileUnholstering"));

                        EditorGUILayout.Space(5f);
                        EditorGUILayout.LabelField("HOLSTER", EditorStyles.boldLabel);
                        EditorGUILayout.Space(2f);
                        EditorGUILayout.PropertyField(settings.FindPropertyRelative("holsterBehaviour"));
                        EditorGUILayout.Space(5f);
                        EditorGUILayout.LabelField("AIM SETTINGS", EditorStyles.boldLabel);
                        EditorGUILayout.Space(2f);
                        EditorGUILayout.PropertyField(settings.FindPropertyRelative("alternateAiming"));

                        EditorGUILayout.Space(5f);
                        EditorGUILayout.LabelField("HIT SETTINGS", EditorStyles.boldLabel);
                        EditorGUILayout.Space(2f);
                        EditorGUILayout.PropertyField(settings.FindPropertyRelative("hitLayer"));
                        break;
                    case "Secondary Attack":
                    
                        SerializedProperty canMeleeProp = settings.FindPropertyRelative("canMelee");
                        bool canMelee = canMeleeProp.boolValue;
                        EditorGUILayout.PropertyField(canMeleeProp);
                        if (canMelee)
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.PropertyField(settings.FindPropertyRelative("meleeObject"));
                            if(myScript.settings.meleeObject)
                            {
                                EditorGUI.indentLevel++;
                                EditorGUILayout.LabelField("You can leave �meleeHeadBone� unassigned if your camera does not move during your Melee Animations.", EditorStyles.helpBox);
                                EditorGUILayout.PropertyField(settings.FindPropertyRelative("meleeHeadBone"));
                                EditorGUI.indentLevel--;
                            }
                            SerializedProperty meleeDelayProp = settings.FindPropertyRelative("meleeDelay");
                            float meleeDelay = meleeDelayProp.floatValue;
                            EditorGUILayout.PropertyField(meleeDelayProp);
                            EditorGUILayout.PropertyField(settings.FindPropertyRelative("meleeAttackDamage"));
                            EditorGUILayout.PropertyField(settings.FindPropertyRelative("meleeRange"));
                            EditorGUILayout.PropertyField(settings.FindPropertyRelative("meleeCamShakeAmount"));
                            EditorGUILayout.PropertyField(settings.FindPropertyRelative("meleeAudioClip"));
                            EditorGUILayout.PropertyField(settings.FindPropertyRelative("reEnableMeleeAfterAction"));

                            if (meleeDelayProp.floatValue > 1f)
                            {
                                meleeDelayProp.floatValue = 1f;
                            }
                            EditorGUI.indentLevel--;
                        }
                        break;
                    case "Effects":
                        EditorGUILayout.Space(2f);
                        EditorGUILayout.PropertyField(settings.FindPropertyRelative("impactEffects"));
                        EditorGUILayout.Space(2f);
                        break;
                    case "Events":
                        EditorGUILayout.PropertyField(settings.FindPropertyRelative("userEvents"));
                        EditorGUILayout.PropertyField(settings.FindPropertyRelative("customPrimaryShot"));
                        break;
                }
                EditorGUILayout.Space(10f);

                serializedObject.ApplyModifiedProperties();
            }
        }
    }
}
#endif
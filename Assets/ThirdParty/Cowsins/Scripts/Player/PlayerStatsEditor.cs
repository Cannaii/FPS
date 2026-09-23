#if UNITY_EDITOR
/// <summary>
/// This script belongs to cowsins™ as a part of the cowsins´ FPS Engine. All rights reserved. 
/// </summary>
using UnityEngine;
using UnityEditor;
namespace cowsins
{
    [System.Serializable]
    [CustomEditor(typeof(PlayerStats))]
    public class PlayerStatsEditor : Editor
    {
        private string[] tabs = { "Player Stats", "Fall Damage", "Auto Heal", "Events" };
        private int currentTab = 0;

        override public void OnInspectorGUI()
        {
            serializedObject.Update();
            PlayerStats myScript = target as PlayerStats;

            Texture2D myTexture = Resources.Load<Texture2D>("CustomEditor/playerState_CustomEditor") as Texture2D;
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
                    case "Player Stats":
                        EditorGUILayout.LabelField("PLAYER STATES", EditorStyles.boldLabel);
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("health"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("shield"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("maxHealth"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("maxShield"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("freezePlayerOnDeath"));
                        break;
                    case "Fall Damage":
                        EditorGUILayout.LabelField("FALL DAMAGE", EditorStyles.boldLabel);
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("takesFallDamage"));
                        if (myScript.TakesFallDamage)
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.PropertyField(serializedObject.FindProperty("minimumHeightDifferenceToApplyDamage"));
                            EditorGUILayout.PropertyField(serializedObject.FindProperty("fallDamageMultiplier"));
                            EditorGUI.indentLevel--;
                        }
                        break;
                    case "Auto Heal":
                        EditorGUILayout.LabelField("AUTO HEAL", EditorStyles.boldLabel);
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("enableAutoHeal"));
                        if (myScript.EnableAutoHeal)
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.PropertyField(serializedObject.FindProperty("healRate"));
                            EditorGUILayout.PropertyField(serializedObject.FindProperty("healAmount"));
                            EditorGUILayout.PropertyField(serializedObject.FindProperty("restartAutoHealAfterBeingDamaged"));
                            if (myScript.RestartAutoHealAfterBeingDamaged)
                            {
                                EditorGUI.indentLevel++;
                                EditorGUILayout.PropertyField(serializedObject.FindProperty("restartAutoHealTime"));
                                EditorGUI.indentLevel--;
                            }
                            EditorGUI.indentLevel--;
                        }
                        break;
                    case "Events":
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("userEvents"));
                        break;

                }
            }

            #endregion

            serializedObject.ApplyModifiedProperties();

        }
    }

}
#endif
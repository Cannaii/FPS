#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace cowsins
{
    [CustomPropertyDrawer(typeof(WeaponControllerSettings.ImpactEffects))]
    public class ImpactEffectsDrawer : PropertyDrawer
    {
        private Dictionary<string, bool> foldouts = new Dictionary<string, bool>();

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var defaultImpactProp = property.FindPropertyRelative("defaultImpact");
            var impactsProp = property.FindPropertyRelative("impacts");

            float yPos = position.y;

            // Default impact field
            var defaultRect = new Rect(position.x, yPos, position.width, EditorGUI.GetPropertyHeight(defaultImpactProp));
            EditorGUI.PropertyField(defaultRect, defaultImpactProp, true);
            yPos += EditorGUI.GetPropertyHeight(defaultImpactProp) + 5;

            // Impacts list with foldouts
            for (int i = 0; i < impactsProp.arraySize; i++)
            {
                var element = impactsProp.GetArrayElementAtIndex(i);
                var layerNameProp = element.FindPropertyRelative("layerName");
                var impactProp = element.FindPropertyRelative("impact");

                string layerKey = layerNameProp.stringValue;
                if (string.IsNullOrEmpty(layerKey))
                    layerKey = $"Layer{i}";

                if (!foldouts.ContainsKey(layerKey))
                    foldouts[layerKey] = true;

                // Foldout row
                Rect foldoutRect = new Rect(position.x, yPos, position.width - 25, EditorGUIUtility.singleLineHeight);
                foldouts[layerKey] = EditorGUI.Foldout(foldoutRect, foldouts[layerKey], layerKey, true);

                // Remove button
                if (GUI.Button(new Rect(position.x + position.width - 20, yPos, 20, EditorGUIUtility.singleLineHeight), "x"))
                {
                    impactsProp.DeleteArrayElementAtIndex(i);
                    foldouts.Remove(layerKey);
                    break;
                }

                yPos += EditorGUIUtility.singleLineHeight + 2;

                if (foldouts[layerKey])
                {
                    // Layer name field
                    var layerRect = new Rect(position.x + 15, yPos, position.width - 15, EditorGUIUtility.singleLineHeight);
                    EditorGUI.PropertyField(layerRect, layerNameProp, new GUIContent("Layer Name"));
                    yPos += EditorGUIUtility.singleLineHeight + 2;

                    // Impact prefab field
                    var impactRect = new Rect(position.x + 15, yPos, position.width - 15, EditorGUI.GetPropertyHeight(impactProp));
                    EditorGUI.PropertyField(impactRect, impactProp, new GUIContent("Impact Prefab"), true);
                    yPos += EditorGUI.GetPropertyHeight(impactProp) + 10;
                }
            }

            yPos += EditorGUIUtility.singleLineHeight;

            // Auto-populate button
            if (GUI.Button(new Rect(position.x, yPos, position.width, EditorGUIUtility.singleLineHeight),
                          "Refresh Impact Layers"))
            {
                AutoPopulateImpacts(property);
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight * 3;

            var defaultImpactProp = property.FindPropertyRelative("defaultImpact");
            height += EditorGUI.GetPropertyHeight(defaultImpactProp) + 10;

            var impactsProp = property.FindPropertyRelative("impacts");
            for (int i = 0; i < impactsProp.arraySize; i++)
            {
                var element = impactsProp.GetArrayElementAtIndex(i);
                var impactProp = element.FindPropertyRelative("impact");

                string layerKey = element.FindPropertyRelative("layerName").stringValue;
                if (string.IsNullOrEmpty(layerKey)) layerKey = $"Layer{i}";

                height += EditorGUIUtility.singleLineHeight + 2;

                if (!foldouts.ContainsKey(layerKey) || foldouts[layerKey])
                {
                    height += EditorGUIUtility.singleLineHeight + 2; // layer name
                    height += EditorGUI.GetPropertyHeight(impactProp) + 10;
                }
            }

            return height;
        }

        private void AutoPopulateImpacts(SerializedProperty property)
        {
            var impactsProp = property.FindPropertyRelative("impacts");
            for (int i = 0; i < 32; i++)
            {
                string layerName = LayerMask.LayerToName(i);
                if (string.IsNullOrEmpty(layerName)) continue;

                if (layerName == "Default" || layerName == "Ignore Raycast") continue;

                if (!ContainsLayer(impactsProp, layerName))
                    AddImpactEntry(impactsProp, layerName, i);
            }
            property.serializedObject.ApplyModifiedProperties();
        }

        private bool ContainsLayer(SerializedProperty impactsProp, string layerName)
        {
            for (int i = 0; i < impactsProp.arraySize; i++)
            {
                var element = impactsProp.GetArrayElementAtIndex(i);
                if (element.FindPropertyRelative("layerName").stringValue == layerName)
                    return true;
            }
            return false;
        }

        private void AddImpactEntry(SerializedProperty impactsProp, string layerName, int layerIndex)
        {
            impactsProp.arraySize++;
            var newEntry = impactsProp.GetArrayElementAtIndex(impactsProp.arraySize - 1);
            newEntry.FindPropertyRelative("layerName").stringValue = layerName;
            newEntry.FindPropertyRelative("cachedLayerIndex").intValue = layerIndex;
            newEntry.FindPropertyRelative("impact").objectReferenceValue = null;

            foldouts[layerName] = true;
        }
    }
}
#endif

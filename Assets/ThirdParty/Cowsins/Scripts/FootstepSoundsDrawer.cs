#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace cowsins
{
    [CustomPropertyDrawer(typeof(PlayerMovementSettings.FootStepsSounds))]
    public class FootstepSoundsDrawer : PropertyDrawer
    {
        private Dictionary<string, bool> foldouts = new Dictionary<string, bool>();

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var defaultStepProp = property.FindPropertyRelative("defaultStep");
            var surfaceSoundsProp = property.FindPropertyRelative("surfaceSounds");

            float yPos = position.y;

            var defaultRect = new Rect(position.x, yPos, position.width, EditorGUI.GetPropertyHeight(defaultStepProp));
            EditorGUI.PropertyField(defaultRect, defaultStepProp, true);
            yPos += EditorGUI.GetPropertyHeight(defaultStepProp);

            // Surface sounds list with foldouts
            for (int i = 0; i < surfaceSoundsProp.arraySize; i++)
            {
                var element = surfaceSoundsProp.GetArrayElementAtIndex(i);
                var layerNameProp = element.FindPropertyRelative("layerName");
                var soundsProp = element.FindPropertyRelative("sounds");

                string layerKey = layerNameProp.stringValue;
                if (!foldouts.ContainsKey(layerKey))
                    foldouts[layerKey] = true;

                // Foldout showing only the layer name
                Rect foldoutRect = new Rect(position.x, yPos, position.width - 25, EditorGUIUtility.singleLineHeight);
                foldouts[layerKey] = EditorGUI.Foldout(foldoutRect, foldouts[layerKey], layerKey, true);

                // Remove button
                if (GUI.Button(new Rect(position.x + position.width - 20, yPos, 20, EditorGUIUtility.singleLineHeight), "×"))
                {
                    surfaceSoundsProp.DeleteArrayElementAtIndex(i);
                    foldouts.Remove(layerKey);
                    break;
                }

                yPos += EditorGUIUtility.singleLineHeight + 2;

                if (foldouts[layerKey])
                {
                    // Layer name field (optional, can remove if fully hidden)
                    var layerRect = new Rect(position.x + 15, yPos, position.width - 15, EditorGUIUtility.singleLineHeight);
                    EditorGUI.PropertyField(layerRect, layerNameProp, new GUIContent("Layer Name"));
                    yPos += EditorGUIUtility.singleLineHeight + 2;

                    // Sounds array
                    var soundsRect = new Rect(position.x + 15, yPos, position.width - 15, EditorGUI.GetPropertyHeight(soundsProp));
                    EditorGUI.PropertyField(soundsRect, soundsProp, new GUIContent("Sounds"), true);
                    yPos += EditorGUI.GetPropertyHeight(soundsProp) + 10;
                }
            }

            yPos += EditorGUIUtility.singleLineHeight;

            // Auto-populate button
            if (GUI.Button(new Rect(position.x, yPos, position.width, EditorGUIUtility.singleLineHeight),
                          "Refresh Footsteps Layers"))
            {
                AutoPopulateFootsteps(property);
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight * 4;

            var defaultStepProp = property.FindPropertyRelative("defaultStep");
            height += EditorGUI.GetPropertyHeight(defaultStepProp) + 10;

            var surfaceSoundsProp = property.FindPropertyRelative("surfaceSounds");
            for (int i = 0; i < surfaceSoundsProp.arraySize; i++)
            {
                var element = surfaceSoundsProp.GetArrayElementAtIndex(i);
                var soundsProp = element.FindPropertyRelative("sounds");

                string layerKey = element.FindPropertyRelative("layerName").stringValue;
                height += EditorGUIUtility.singleLineHeight + 2;

                if (!foldouts.ContainsKey(layerKey) || foldouts[layerKey])
                {
                    height += EditorGUIUtility.singleLineHeight + 2; // layer name
                    height += EditorGUI.GetPropertyHeight(soundsProp) + 10;
                }
            }

            return height;
        }

        private void AutoPopulateFootsteps(SerializedProperty property)
        {
            var surfaceSoundsProp = property.FindPropertyRelative("surfaceSounds");
            LayerMask whatIsGround = GetWhatIsGroundLayerMask(property.serializedObject);
            PopulateFromLayerMask(surfaceSoundsProp, whatIsGround);
            property.serializedObject.ApplyModifiedProperties();
        }

        private LayerMask GetWhatIsGroundLayerMask(SerializedObject serializedObject)
        {
            var target = (PlayerMovement)serializedObject.targetObject;
            return target.playerSettings.whatIsGround;
        }

        private void PopulateFromLayerMask(SerializedProperty surfaceSoundsProp, LayerMask whatIsGround)
        {
            for (int i = 0; i < 32; i++)
            {
                if ((whatIsGround & (1 << i)) != 0)
                {
                    string layerName = LayerMask.LayerToName(i);
                    if (string.IsNullOrEmpty(layerName))
                        layerName = $"Layer{i}";

                    if (layerName == "Default" || layerName == "Interactable")
                        continue;

                    if (!ContainsLayer(surfaceSoundsProp, layerName))
                        AddSurfaceEntry(surfaceSoundsProp, layerName, i);
                }
            }
        }

        private bool ContainsLayer(SerializedProperty surfaceSoundsProp, string layerName)
        {
            for (int i = 0; i < surfaceSoundsProp.arraySize; i++)
            {
                var element = surfaceSoundsProp.GetArrayElementAtIndex(i);
                if (element.FindPropertyRelative("layerName").stringValue == layerName)
                    return true;
            }
            return false;
        }

        private void AddSurfaceEntry(SerializedProperty surfaceSoundsProp, string layerName, int layerIndex)
        {
            surfaceSoundsProp.arraySize++;
            var newEntry = surfaceSoundsProp.GetArrayElementAtIndex(surfaceSoundsProp.arraySize - 1);
            newEntry.FindPropertyRelative("layerName").stringValue = layerName;
            newEntry.FindPropertyRelative("cachedLayerIndex").intValue = layerIndex;

            var soundsProp = newEntry.FindPropertyRelative("sounds");
            soundsProp.ClearArray();

            foldouts[layerName] = true;
        }
    }
}
#endif

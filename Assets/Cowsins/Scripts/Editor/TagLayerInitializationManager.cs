#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// This simplifies migration from another project by eliminating the need to manually update tags and layers.
/// </summary>
namespace cowsins.Inspector
{
    [InitializeOnLoad]
    public class TagLayerInitializationManager
    {

        static TagLayerInitializationManager()
        {
            EditorApplication.update += InitializeOnEditorOpen;
        }

        private static void InitializeOnEditorOpen()
        {
            // Check if TagLayerInitializationManager is already initialized for this session
            if (SessionState.GetBool("TagLayerInitializationDone", false))
            {
                EditorApplication.update -= InitializeOnEditorOpen;
                return;
            }

            // initialization is done for this session
            SessionState.SetBool("TagLayerInitializationDone", true);
            EditorApplication.update -= InitializeOnEditorOpen;

            // Define a list of necessary Tags
            List<string> tags = new List<string>
        {
            "Enemy",
            "FirePoint",
            "Critical",
            "Weapons",
            "Ramp",
            "BodyShot",
        };
            // Define a list of necessary Layers
            List<string> layers = new List<string>
        {
            "Ground",
            "Weapons",
            "Enemy",
            "Object",
            "Interactable",
            "Grass",
            "Metal",
            "Mud",
            "Wood",
            "UITop",
            "PostProcessing",
            "Effects",
            "Player",
            "Ladder"
        };

            // Add each tag defined previously
            foreach (var tag in tags)
            {
                AddTag(tag);
            }
            // Add each layer defined previously
            foreach (var layer in layers)
            {
                AddLayer(layer);
            }

        }
        public static void AddTag(string tag)
        {
            // Gathers the tags
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty tagsProp = tagManager.FindProperty("tags");

            // If tag does not exist, it gets added
            if (!PropertyExists(tagsProp, 0, tagsProp.arraySize, tag))
            {
                tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
                SerializedProperty newTagProp = tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1);
                newTagProp.stringValue = tag;
                Debug.Log("Tag added: " + tag);
            }

            tagManager.ApplyModifiedProperties();
        }

        public static void AddLayer(string layer)
        {
            // Gathers the layers
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layersProp = tagManager.FindProperty("layers");
            // If layer does not exist, it gets added
            if (!PropertyExists(layersProp, 0, layersProp.arraySize, layer))
            {
                int emptyLayerIndex = FindFirstEmptyLayerIndex(layersProp);
                if (emptyLayerIndex != -1)
                {
                    SerializedProperty newLayerProp = layersProp.GetArrayElementAtIndex(emptyLayerIndex);
                    newLayerProp.stringValue = layer;
                    Debug.Log("Layer added: " + layer);
                }
                else
                {
                    Debug.LogError("No empty layer slots available.");
                }
            }

            tagManager.ApplyModifiedProperties();
        }

        private static bool PropertyExists(SerializedProperty property, int start, int end, string value)
        {
            for (int i = start; i < end; i++)
            {
                SerializedProperty t = property.GetArrayElementAtIndex(i);
                if (t.stringValue.Equals(value))
                {
                    return true;
                }
            }
            return false;
        }

        // Check if there is room for a new layer
        private static int FindFirstEmptyLayerIndex(SerializedProperty layersProp)
        {
            for (int i = 8; i < layersProp.arraySize; i++)
            {
                SerializedProperty layerProp = layersProp.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(layerProp.stringValue))
                {
                    return i;
                }
            }
            // No empty slots found
            return -1;
        }
    }
}
#endif
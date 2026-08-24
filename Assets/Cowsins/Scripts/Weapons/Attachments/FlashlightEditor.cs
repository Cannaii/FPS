#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace cowsins
{
    /// <summary>
    /// Simple Editor script for the Flashlight.
    /// Assists the user to easily create a Spotlight for a flashlight.
    /// </summary>
    [CustomEditor(typeof(Flashlight))]
    public class FlashlightEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawDefaultInspector();

            var flashlight = (Flashlight)target;
            SerializedProperty lightProp = serializedObject.FindProperty("lightSource");

            Light assignedLight = lightProp.objectReferenceValue as Light;

            // if no light has been found in the hierarchy then show "Create Spotlight" button
            if (assignedLight == null)
            {
                if (GUILayout.Button("Create Spotlight"))
                {
                    Undo.RecordObject(flashlight, "Create Spotlight");
                    GameObject lightGO = new GameObject("Flashlight_Spotlight");
                    Undo.RegisterCreatedObjectUndo(lightGO, "Create Spotlight");
                    lightGO.transform.SetParent(flashlight.gameObject.transform, false);
                    Light l = lightGO.AddComponent<Light>();
                    l.type = LightType.Spot;
                    l.spotAngle = 60f;
                    l.range = 10f;
                    l.intensity = 1f;

                    lightProp.objectReferenceValue = l;
                    serializedObject.ApplyModifiedProperties();

                    EditorUtility.SetDirty(flashlight);
                    EditorUtility.SetDirty(lightGO);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(flashlight);
                }
            }
            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif


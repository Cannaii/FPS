#if UNITY_EDITOR
using UnityEditor;
namespace cowsins
{
    [CustomEditor(typeof(CrosshairShape))]
    public class CrosshairShapeEditor : Editor
    {
        override public void OnInspectorGUI()
        {
            serializedObject.Update();
            var myScript = target as CrosshairShape;

            CowsinsEditorWindowUtilities.DrawCrosshairEditorSection(serializedObject, myScript.DefaultCrosshairParts, "defaultCrosshairParts");

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
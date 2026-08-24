#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace cowsins
{
    [CustomPropertyDrawer(typeof(CompatibleAttachments.CompatibleAttachmentEntry))]
    public class CompatibleAttachmentEntryDrawer : PropertyDrawer
    {
        // Controls the state of the foldout of each compatible attachment list ( whether they are folded or unfolded individually )
        private readonly Dictionary<int, bool> foldoutStates = new Dictionary<int, bool>();

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            // Get children
            var typeProperty = property.FindPropertyRelative("type");
            var attachmentsProperty = property.FindPropertyRelative("attachments");

            // Draw Type Field
            var typeRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(typeRect, typeProperty);

            // Ensure Foldout state is tracked Individually
            var foldoutKey = property.propertyPath.GetHashCode();
            if (!foldoutStates.ContainsKey(foldoutKey))
                foldoutStates[foldoutKey] = false;

            // Gather the name of the current type to display it in the foldout.
            // Null or Error Falls back to "Attachment"
            string typeName = typeProperty.enumDisplayNames[typeProperty.enumValueIndex];
            if (string.IsNullOrEmpty(typeName))
                typeName = "Attachment";

            // Construct the Foldout name with the Current Attachment Name. Ex: Compatible Scopes.
            string foldoutLabel = $"Compatible {typeName}s";

            // Draw Foldout
            var foldoutRect = new Rect(position.x, typeRect.y + EditorGUIUtility.singleLineHeight + 2, position.width, EditorGUIUtility.singleLineHeight);
            foldoutStates[foldoutKey] = EditorGUI.Foldout(foldoutRect, foldoutStates[foldoutKey], foldoutLabel, true);

            float currentY = foldoutRect.y + EditorGUIUtility.singleLineHeight + 2;

            // If the current Foldout is unfolded:
            if (foldoutStates[foldoutKey])
            {
                EditorGUI.indentLevel++;

                // Draw each list element
                for (int i = 0; i < attachmentsProperty.arraySize; i++)
                {
                    var element = attachmentsProperty.GetArrayElementAtIndex(i);
                    float elementHeight = EditorGUI.GetPropertyHeight(element, true);

                    var elementRect = new Rect(position.x, currentY, position.width - 20, elementHeight);
                    EditorGUI.PropertyField(elementRect, element, new GUIContent($"{typeName} {i + 1}"), true);

                    // Draw Remove button
                    var removeBtnRect = new Rect(position.x + position.width - 18, currentY, 18, EditorGUIUtility.singleLineHeight);
                    if (GUI.Button(removeBtnRect, "-"))
                    {
                        attachmentsProperty.DeleteArrayElementAtIndex(i);
                        break;
                    }

                    currentY += elementHeight + 2;
                }

                // Draw Add button
                float addBtnWidth = 125f;
                var addBtnRect = new Rect(position.x + position.width - addBtnWidth, currentY, addBtnWidth, EditorGUIUtility.singleLineHeight * 1.5f);

                if (GUI.Button(addBtnRect, $"Add {typeName}"))
                {
                    attachmentsProperty.InsertArrayElementAtIndex(attachmentsProperty.arraySize);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }


        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var attachmentsProp = property.FindPropertyRelative("attachments");
            var foldoutKey = property.propertyPath.GetHashCode();

            float height = EditorGUIUtility.singleLineHeight + EditorGUIUtility.singleLineHeight + 2;

            if (foldoutStates.TryGetValue(foldoutKey, out bool isExpanded) && isExpanded)
            {
                for (int i = 0; i < attachmentsProp.arraySize; i++)
                {
                    var element = attachmentsProp.GetArrayElementAtIndex(i);
                    height += EditorGUI.GetPropertyHeight(element, true) + 2;
                }

                // Add space for the "+" button
                height += EditorGUIUtility.singleLineHeight + 2;
            }
            height += 10;
            return height;
        }
    }
}

#endif
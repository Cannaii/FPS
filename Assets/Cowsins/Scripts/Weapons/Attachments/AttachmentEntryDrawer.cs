#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace cowsins
{
    [CustomPropertyDrawer(typeof(DefaultAttachment.AttachmentEntry))]
    public class AttachmentEntryDrawer : PropertyDrawer
    {
        private const float Spacing = 4f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            // Get children
            var typeProperty = property.FindPropertyRelative("type");
            var attachmentProperty = property.FindPropertyRelative("attachment");

            float fieldWidth = (position.width - Spacing) / 2f;

            // TypeRect: Left Side
            var typeRect = new Rect(position.x, position.y, fieldWidth, position.height);
            // Attachemnt Rect: Right Side
            var attachmentRect = new Rect(position.x + fieldWidth + Spacing, position.y, fieldWidth, position.height);

            // Draw the Properties
            // GUIContent.None is used to prevent drawing the field titles
            EditorGUI.PropertyField(typeRect, typeProperty, GUIContent.none);
            EditorGUI.PropertyField(attachmentRect, attachmentProperty, GUIContent.none);

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }
    }
}

#endif
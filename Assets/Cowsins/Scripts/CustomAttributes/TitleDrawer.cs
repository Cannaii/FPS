#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

    namespace cowsins
    {
        [CustomPropertyDrawer(typeof(TitleAttribute))]
        public class TitleDrawer : PropertyDrawer
        {
            float propertySeparation;
            public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
            {
                TitleAttribute titleAttribute = (TitleAttribute)attribute;

                // Create style for the title
                GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 16,
                    fontStyle = FontStyle.Bold
                };

                // Calculate rects
                Rect titleRect = new Rect(position.x, position.y + titleAttribute.upMargin, position.width, EditorGUIUtility.singleLineHeight);
                float spacing = 1f;
                float dividerHeight = titleAttribute.divider ? 2f : 0f;
                Rect dividerRect = new Rect(position.x, titleRect.yMax + spacing, position.width, dividerHeight); 
                Rect propertyRect = new Rect(position.x, dividerRect.yMax + 3, position.width, EditorGUIUtility.singleLineHeight);


                // Draw title
                EditorGUI.LabelField(titleRect, titleAttribute.title, titleStyle);

                // Draw divider if enabled
                if (titleAttribute.divider)
                {
                    EditorGUI.DrawRect(dividerRect, Color.grey);
                }

                EditorGUI.PropertyField(propertyRect, property, new GUIContent(property.displayName));
        }

            public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            {
                return ( EditorGUIUtility.singleLineHeight + propertySeparation ) * 2.1f + 4 + ((TitleAttribute)attribute).upMargin;
            }
        }
    }


#endif

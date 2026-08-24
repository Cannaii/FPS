#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace cowsins
{
    [InitializeOnLoad]
    public static class DraggableButtonInSceneView
    {
        public static int buttonCount = 4;

        private const string ShowDraggableButtonKey = "Cowsins_ShowDraggableButton";
        public static bool showDraggableButton;

        private static Vector2 buttonPosition = new Vector2(10, 10);
        private static bool isDragging = false;
        private static GUIStyle buttonStyle;
        private static Texture2D logoIcon;
        private static Vector2 buttonSize = new Vector2(40, 35);
        private static Vector2 imageSize = new Vector2(21, 21);
        private static bool showMenu = false;

        private static Texture2D option1Image, option2Image, option3Image, option4Image;

        public delegate void AddButtonDelegate(Rect menuRect);
        public static event AddButtonDelegate OnAddExternalButtons;

        static DraggableButtonInSceneView()
        {
            showDraggableButton = EditorPrefs.GetBool(ShowDraggableButtonKey, true);
            SceneView.duringSceneGui += OnSceneGUI;

            logoIcon = Resources.Load<Texture2D>("CustomEditor/LogoIcon");

            option1Image = Resources.Load<Texture2D>("CustomEditor/FolderWeapon");
            option2Image = Resources.Load<Texture2D>("CustomEditor/FolderSO");
            option3Image = Resources.Load<Texture2D>("CustomEditor/AddWeapon");
            option4Image = Resources.Load<Texture2D>("CustomEditor/Tutorials");
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            if (!showDraggableButton)
                return;

            if (buttonStyle == null)
                buttonStyle = new GUIStyle(GUI.skin.button);

            Handles.BeginGUI();

            Rect buttonRect = new Rect(buttonPosition.x, buttonPosition.y, buttonSize.x, buttonSize.y);

            HandleDragging(buttonRect, sceneView);

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && buttonRect.Contains(Event.current.mousePosition))
            {
                showMenu = !showMenu;
                Event.current.Use();
                sceneView.Repaint();
            }

            GUI.Button(buttonRect, GUIContent.none, buttonStyle);
            Rect imageRect = new Rect(
                buttonRect.x + (buttonSize.x - imageSize.x) / 2,
                buttonRect.y + (buttonSize.y - imageSize.y) / 2,
                imageSize.x,
                imageSize.y
            );
            if (logoIcon) GUI.DrawTexture(imageRect, logoIcon, ScaleMode.ScaleToFit);

            // Draw the menu
            if (showMenu && !isDragging)
            {
                Rect menuRect = new Rect(
                    buttonRect.x + (buttonPosition.x < sceneView.position.width / 2 ? buttonSize.x : -40 * buttonCount),
                    buttonRect.y,
                    100,
                    buttonSize.y
                );
                DrawMenu(menuRect);
            }

            Handles.EndGUI();
        }

        private static void DrawMenu(Rect menuRect)
        {
            GUI.Box(menuRect, GUIContent.none, GUI.skin.box);

            GUIContent option1Content = new GUIContent(option1Image);
            GUIContent option2Content = new GUIContent(option2Image);
            GUIContent option3Content = new GUIContent(option3Image);
            GUIContent option4Content = new GUIContent(option4Image);

            float itemWidth = menuRect.width / 3;
            float itemHeight = menuRect.height;

            if (GUI.Button(new Rect(GetButtonPosition(menuRect, 0), menuRect.y, itemWidth, itemHeight), option1Content))
            {
                PingFolder("Assets/Cowsins/Prefabs/Weapons");
            }

            if (GUI.Button(new Rect(GetButtonPosition(menuRect, 1), menuRect.y, itemWidth, itemHeight), option2Content))
            {
                PingFolder("Assets/Cowsins/ScriptableObjects/Weapons");
            }

            if (GUI.Button(new Rect(GetButtonPosition(menuRect, 2), menuRect.y, itemWidth, itemHeight), option3Content))
            {
                CustomTabEditorWindow.OpenWeaponsTab();
            }

            if (GUI.Button(new Rect(GetButtonPosition(menuRect, 3), menuRect.y, itemWidth, itemHeight), option4Content))
            {
                Application.OpenURL("https://www.cowsins.com/");
            }

            OnAddExternalButtons?.Invoke(menuRect);
        }

        private static void PingFolder(string folderPath)
        {
            Object folder = AssetDatabase.LoadAssetAtPath<Object>(folderPath);
            if (folder != null)
            {
                EditorGUIUtility.PingObject(folder);
                Selection.activeObject = folder;
            }
            else
            {
                Debug.LogError($"Folder is empty or not found: {folderPath}. Did you remove or rename the folder?");
            }
        }

        private static void HandleDragging(Rect buttonRect, SceneView sceneView)
        {
            Event e = Event.current;

            if (e.type == EventType.MouseDown && e.button == 1 && buttonRect.Contains(e.mousePosition))
            {
                isDragging = true;
                showMenu = false;
                e.Use();
            }

            if (isDragging && e.type == EventType.MouseDrag)
            {
                buttonPosition += e.delta;
                ClampButtonPosition(sceneView);
                e.Use();
                sceneView.Repaint();
            }

            if (e.type == EventType.MouseUp && e.button == 1)
            {
                isDragging = false;
                SnapToNearestEdge(sceneView);
                sceneView.Repaint();
            }
        }

        private static void ClampButtonPosition(SceneView sceneView)
        {
            Vector2 viewSize = new Vector2(sceneView.position.width, sceneView.position.height);
            buttonPosition.x = Mathf.Clamp(buttonPosition.x, 10, viewSize.x - buttonSize.x - 10);
            buttonPosition.y = Mathf.Clamp(buttonPosition.y, 10, viewSize.y - buttonSize.y - 10);
        }

        private static void SnapToNearestEdge(SceneView sceneView)
        {
            Vector2 viewSize = new Vector2(sceneView.position.width, sceneView.position.height);

            float left = buttonPosition.x - 10;
            float right = viewSize.x - (buttonPosition.x + buttonSize.x + 10);
            float top = buttonPosition.y - 10;
            float bottom = viewSize.y - (buttonPosition.y + buttonSize.y + 10);

            float min = Mathf.Min(left, right, top, bottom);

            if (min == left) buttonPosition.x = 10;
            else if (min == right) buttonPosition.x = viewSize.x - buttonSize.x - 10;
            else if (min == top) buttonPosition.y = 10;
            else if (min == bottom) buttonPosition.y = viewSize.y - buttonSize.y - 10;
        }

        public static float GetButtonPosition(Rect menuRect, int index)
        {
            float spacing = 5;
            float itemWidth = menuRect.width / 3;
            float offset = buttonPosition.x < menuRect.width ? 5 : -4;

            return menuRect.x + spacing * (index + 1) + itemWidth * index + offset;
        }

        public static void SetShowDraggableButton(bool value)
        {
            showDraggableButton = value;
            EditorPrefs.SetBool(ShowDraggableButtonKey, value);
        }
    }
}
#endif

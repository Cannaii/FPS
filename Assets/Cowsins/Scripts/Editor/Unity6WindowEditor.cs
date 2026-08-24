#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
namespace cowsins
{
    [InitializeOnLoad]
    public class Unity6EditorWindow : EditorWindow
    {
        private Texture2D headerImage;
        private Texture2D urpImage;
        private Texture2D builtInImage;
        private Texture2D hdrpImage;

        private Vector2 scrollPosition;

        private bool isUsingUnity6;

        static Unity6EditorWindow()
        {
            EditorApplication.delayCall += ShowWindowOnce;
        }

        private static void ShowWindowOnce()
        {
            if (!SessionState.GetBool("FirstInit", false))
            {
                if (!CowsinsUtilities.IsUsingUnity6() || EditorPrefs.GetBool("Unity6EditorWindowDontShowAgain") == true) return;
                ShowWindow();
                SessionState.SetBool("FirstInit", true);
            }
        }

        [MenuItem("Cowsins/Unity 6 Set Up")]
        public static void ShowWindow()
        {
            Unity6EditorWindow window = GetWindow<Unity6EditorWindow>("FPS ENGINE | Unity 6 Set Up");
            window.minSize = new Vector2(500, 550);
            window.maxSize = new Vector2(500, 550);
        }

        private void OnEnable()
        {
            headerImage = Resources.Load<Texture2D>("CustomEditor/Unity6Logo");
            urpImage = Resources.Load<Texture2D>("CustomEditor/URP");
            builtInImage = Resources.Load<Texture2D>("CustomEditor/BuiltIn");
            hdrpImage = Resources.Load<Texture2D>("CustomEditor/HDRP");

            isUsingUnity6 = CowsinsUtilities.IsUsingUnity6();
        }

        private void OnGUI()
        {
            // Header
            GUILayout.Label(headerImage, GUILayout.Width(position.width * 0.98f), GUILayout.Height(headerImage.height * (position.width / headerImage.width)));
            GUILayout.Space(10);
            // Unity 6 Information
            if (!isUsingUnity6) EditorGUILayout.HelpBox("You are not using Unity 6.", MessageType.Error);
            else EditorGUILayout.HelpBox("You are using FPS Engine in Unity 6. Explore helpful tutorials to learn how to configure FPS Engine for your preferred Render Pipeline.", MessageType.Info);
            DrawTutorialGallery();

            // We need to use Flexible Space to push the footer content to the bottom
            GUILayout.FlexibleSpace();

            DrawFooter();
        }


        private void DrawFooter()
        {
            Color footerColor = new Color(0.2f, 0.2f, 0.2f, 1f);
            Rect footerRect = new Rect(0, position.height - 21, position.width, 50);

            GUI.color = footerColor;
            GUI.DrawTexture(footerRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            bool dontShowAgain = EditorPrefs.GetBool("Unity6EditorWindowDontShowAgain", false);
            dontShowAgain = EditorGUILayout.Toggle("Don't show again", dontShowAgain, GUILayout.Width(position.width - 20));

            EditorPrefs.SetBool("Unity6EditorWindowDontShowAgain", dontShowAgain);
        }

        private void DrawTutorialGallery()
        {
            GUILayout.Space(10);
            GUILayout.BeginVertical("Box");
            GUILayout.Label("Tutorials", EditorStyles.boldLabel);
            GUILayout.EndVertical();

            scrollPosition = GUILayout.BeginScrollView(scrollPosition);
            GUILayout.BeginHorizontal();

            DrawTutorialButton(urpImage, "https://www.cowsins.com/videos/PyAywKlqKrY");
            DrawTutorialButton(builtInImage, "https://www.cowsins.com/videos/aXW34tlzNGE");
            DrawTutorialButton(hdrpImage, "https://www.cowsins.com/videos/kyJFwmcs-6Q");

            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();
        }

        private void DrawTutorialButton(Texture2D image, string link)
        {
            float imageWidth = 300;
            float imageHeight = 200;
            float buttonSize = 50;

            GUILayout.BeginVertical();
            GUILayout.Label(image, GUILayout.Width(imageWidth), GUILayout.Height(imageHeight));
            Rect imageRect = GUILayoutUtility.GetLastRect();
            float buttonX = imageRect.x + (imageRect.width / 2) - (buttonSize / 2);
            float buttonY = imageRect.y + (imageRect.height / 2) - (buttonSize / 2);

            GUIStyle playButtonStyle = new GUIStyle(GUI.skin.button);
            Color halfOpacityBlack = new Color(0, 0, 0, 0.5f);
            GUI.backgroundColor = halfOpacityBlack;
            playButtonStyle.normal.textColor = Color.white;

            if (GUI.Button(new Rect(buttonX, buttonY, buttonSize, buttonSize), "▶", playButtonStyle))
            {
                Application.OpenURL(link);
            }

            GUI.backgroundColor = Color.white;

            GUILayout.EndVertical();
        }

    }
}
#endif
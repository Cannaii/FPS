#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace cowsins
{
    public class HomeTab : ITab
    {
        public string TabName => "Home";

        private Vector2 scrollPosition = Vector2.zero;
        private int activeTab = 0;

        public void StartTab() { }

        public void OnGUI()
        {
            GUILayout.BeginHorizontal();

            NavBar();

            GUILayout.Space(25);

            switch (activeTab)
            {
                case 0: Home(); break;
                case 1: Preferences(); break;
                case 2: MoreByCowsins(); break;
                default: Home(); break;
            }

            GUILayout.EndHorizontal();
        }

        private void NavBar()
        {
            GUILayout.BeginVertical(GUI.skin.GetStyle("HelpBox"), GUILayout.Width(Screen.width * 0.25f));

            if (GUILayout.Button("Home", GUILayout.Height(25))) activeTab = 0;
            if (GUILayout.Button("Preferences", GUILayout.Height(25))) activeTab = 1;
            if (GUILayout.Button("More by Cowsins", GUILayout.Height(25))) activeTab = 2;

            // EditorGUILayout.HelpBox($"\nCowsins is facing issues with the YouTube channel, so some tutorials are temporarily available on cowsins.com.\n", MessageType.Warning);

            GUILayout.Label(Resources.Load<Texture2D>("CustomEditor/CowsinsManager/reviewCowsinsManager"), GUILayout.Width(240), GUILayout.Height(330));
            Rect rect = GUILayoutUtility.GetLastRect();
            if (rect.Contains(Event.current.mousePosition))
            {
                if (Event.current.type == EventType.MouseUp && Event.current.button == 0)
                {
                    Application.OpenURL("https://assetstore.unity.com/packages/templates/systems/fps-engine-218594");
                }
            }

            GUILayout.EndVertical();
        }

        private void Home()
        {
            GUILayout.BeginVertical();

            GUILayout.Label("Welcome to FPS Engine by Cowsins!", CowsinsEditorWindowUtilities.BigHeadingStyle(), GUILayout.Height(30));

            GUILayout.Label(Resources.Load<Texture2D>("CustomEditor/CowsinsManager/CowsinsManagerHeader"), GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.705f), GUILayout.Height(110));

            GUILayout.Space(5);

            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));

            GUILayout.BeginHorizontal(GUI.skin.GetStyle("HelpBox"));

            CowsinsEditorWindowUtilities.DrawTutorialCard(Resources.Load<Texture2D>("CustomEditor/weapon-tutorial"), "https://www.cowsins.com/videos/OykOx8CgEMc", .7f);
            CowsinsEditorWindowUtilities.DrawTutorialCard(Resources.Load<Texture2D>("CustomEditor/BuiltIn"), "https://www.cowsins.com/videos/aXW34tlzNGE", .7f);
            CowsinsEditorWindowUtilities.DrawTutorialCard(Resources.Load<Texture2D>("CustomEditor/URP"), "https://www.cowsins.com/videos/PyAywKlqKrY", .7f);
            CowsinsEditorWindowUtilities.DrawTutorialCard(Resources.Load<Texture2D>("CustomEditor/hdrp"), "https://www.cowsins.com/videos/kyJFwmcs-6Q", .7f);

            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();

            GUILayout.Space(10);
            GUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(.5f, .5f, .5f, 1);

            CowsinsEditorWindowUtilities.DrawLinkCard(Resources.Load<Texture2D>("CustomEditor/CowsinsManager/documentationIcon"), "Documentation", "https://cowsinss-organization.gitbook.io/fps-engine-documentation", .77f, .4f);
            GUILayout.FlexibleSpace();
            CowsinsEditorWindowUtilities.DrawLinkCard(Resources.Load<Texture2D>("CustomEditor/CowsinsManager/tutorialsIcon"), "Tutorials", "https://www.cowsins.com/", .77f, .4f);
            GUILayout.FlexibleSpace();
            CowsinsEditorWindowUtilities.DrawLinkCard(Resources.Load<Texture2D>("CustomEditor/CowsinsManager/supportIcon"), "Support", "https://discord.gg/759gSeTT9m", .77f, .4f);
            GUILayout.Space(10);

            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        private void Preferences()
        {
            GUILayout.BeginVertical();
            GUILayout.Label("Preferences", CowsinsEditorWindowUtilities.BigHeadingStyle(), GUILayout.Height(30));

            GUILayout.Space(10);
            GUILayout.BeginVertical(GUI.skin.GetStyle("HelpBox"));
            GUILayout.Label("Scene View", GUILayout.Height(20));
            GUILayout.Space(2);
            bool newToggleValue = GUILayout.Toggle(DraggableButtonInSceneView.showDraggableButton, "Show Draggable Button in Scene View");
            if (newToggleValue != DraggableButtonInSceneView.showDraggableButton)
            {
                DraggableButtonInSceneView.SetShowDraggableButton(newToggleValue);
            }
            GUILayout.Space(5);
            GUILayout.EndVertical();

            GUILayout.Space(10);
            GUILayout.BeginVertical(GUI.skin.GetStyle("HelpBox"));
            GUILayout.Label("Cowsins Manager", GUILayout.Height(20));
            bool dontShowAgain = EditorPrefs.GetBool("FirstTimeShowAgain", false);
            dontShowAgain = GUILayout.Toggle(dontShowAgain, "Show On Start-Up");
            EditorPrefs.SetBool("FirstTimeShowAgain", dontShowAgain);
            GUILayout.Space(5);
            GUILayout.EndVertical();

            GUILayout.EndVertical();
        }

        private void MoreByCowsins()
        {
            GUILayout.BeginVertical();
            GUILayout.Label("More by Cowsins", CowsinsEditorWindowUtilities.BigHeadingStyle(), GUILayout.Height(30));

            PackageCard(Resources.Load<Texture2D>("CustomEditor/CowsinsManager/InventorySaveLoadAddOnThumbnail"), "Inventory Pro + Save & Load", 
                "The definitive solution to add Advanced systems like Inventories, Chests, Shops, Crafting, Save & Load & much more in FPS Engine!",
                "https://assetstore.unity.com/packages/templates/systems/inventory-pro-add-on-for-fps-engine-318131", "FPS Engine Official Add-On","", Color.green, Color.grey);
            PackageCard(Resources.Load<Texture2D>("CustomEditor/CowsinsManager/SaveLoadAddOnThumbnail"), "Save & Load",
                "A powerful all-in-one Save & Load solution built specifically for FPS Engine! Effortlessly scalable and customizable, it easily allows you to save and load your game data!",
                "https://assetstore.unity.com/packages/templates/systems/save-load-add-on-for-fps-engine-316848", "FPS Engine Official Add-On", "", Color.green, Color.grey);
            PackageCard(Resources.Load<Texture2D>("CustomEditor/CowsinsManager/PlatformerEngineThumbnail"), "Platformer Engine",
                "An advanced and powerful solution for 2D and 2.5D Platformer games, adaptable,easily scalable and suitable for any kind of Platformer game, supporting a wide variety of settings and features.",
                "https://assetstore.unity.com/packages/templates/systems/platformer-engine-2d-2-5d-266973", "Cowsins Official Package", string.Empty, Color.yellow, Color.grey);
            PackageCard(Resources.Load<Texture2D>("CustomEditor/CowsinsManager/BulletHellEngineThumbnail"), "Bullet Hell Engine",
                "A powerful solution for Bullet Hell games. Highly customizable and easy to work with, supporting a wide variety of settings and features.",
                "https://assetstore.unity.com/packages/tools/game-toolkits/bullet-hell-engine-249720", "Cowsins Official Package", string.Empty, Color.yellow, Color.grey);

            GUILayout.EndVertical();
        }

        private void PackageCard(Texture2D cardImage,string title,string description, string url, string label1, string label2, Color label1Color, Color label2Color)
        {
            GUI.backgroundColor = Color.white;
            GUILayout.BeginHorizontal(GUI.skin.GetStyle("HelpBox"));
            GUILayout.Space(5);
            CowsinsEditorWindowUtilities.DrawTutorialCard(cardImage, url, .7f);
            GUILayout.Space(10);
            GUILayout.BeginVertical();
            GUILayout.Space(5);
            GUILayout.BeginHorizontal();
            if(!string.IsNullOrEmpty(label1))
                Label(label1, 120, label1Color);
            GUILayout.Space(5);
            if (!string.IsNullOrEmpty(label2))
                Label(label2, 80, label2Color);
            GUILayout.EndHorizontal();
            GUILayout.Label(title, CowsinsEditorWindowUtilities.BigHeadingStyle(), GUILayout.Height(30));

            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.wordWrap = true; 
            style.fontSize = 12; 
            style.normal.textColor = Color.white; 
            GUILayout.Label(description, style, GUILayout.Width(300), GUILayout.Height(60));

            if(string.IsNullOrEmpty(url))
            {
                GUI.backgroundColor = new Color(0, 0, 0, 0.5f);
                if (GUILayout.Button("Coming Soon", GUILayout.Height(25))) { }
            }
            else
            {
                GUI.backgroundColor = Color.white;
                if (GUILayout.Button("View Package", GUILayout.Height(25))) Application.OpenURL(url);
            }
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
        }

        private void Label(string label, int width, Color labelColor)
        {
            GUI.color = labelColor;
            GUILayout.BeginVertical(GUI.skin.GetStyle("HelpBox"), GUILayout.Width(width));
            GUILayout.Label(label, GUILayout.Height(17));
            GUILayout.EndVertical();
            GUI.color = Color.white;
        }
    }
}
#endif

#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace cowsins
{
    [InitializeOnLoad]
    public class CustomTabEditorWindow : EditorWindow
    {
        private List<ITab> tabs;
        private int selectedTabIndex;
        private Vector2 scrollPosition;
        Color darkColor = new Color(.15f, .15f, .15f);

        static CustomTabEditorWindow()
        {
            EditorApplication.delayCall += ShowWindowOnce;
        }
        private static void ShowWindowOnce()
        {
            if (!SessionState.GetBool("First_Init", false))
            {
                if (!EditorPrefs.GetBool("FirstTimeShowAgain")) return;
                OpenWindow();
                SessionState.SetBool("First_Init", true);
            }
        }

        [MenuItem("Cowsins/Cowsins Manager [Home] %q")]
        public static void OpenWindow()
        {
            var window = GetWindow<CustomTabEditorWindow>();
            window.titleContent = new GUIContent("Cowsins Manager");
            window.minSize = new Vector2(600, 450);
            window.maxSize = new Vector2(1000, 600);
            window.InitializeTabs();
            window.Show();
        }

        [MenuItem("Cowsins/Cowsins Manager [Weapons]")]
        public static void OpenWeaponsTab()
        {
            var window = GetWindow<CustomTabEditorWindow>();
            window.titleContent = new GUIContent("Cowsins Manager");
            window.minSize = new Vector2(600, 450);
            window.maxSize = new Vector2(1000, 600);
            window.InitializeTabs();

            // Find the "Weapons" tab and set it as the selected tab
            var weaponsTabIndex = window.tabs.FindIndex(tab => tab.TabName == "Weapons");
            if (weaponsTabIndex >= 0)
            {
                window.selectedTabIndex = weaponsTabIndex;
            }

            window.Show();
        }
        public static void CloseWindow()
        {
            var window = GetWindow<CustomTabEditorWindow>();
            window.Close();
        }
        private void InitializeTabs()
        {
            tabs = new List<ITab>();

            // Load all tabs that implement ITab
            var tabTypes = typeof(ITab).Assembly.GetTypes()
                .Where(t => typeof(ITab).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            foreach (var type in tabTypes)
            {
                ITab tabInstance = (ITab)Activator.CreateInstance(type);
                tabs.Add(tabInstance);
            }
        }

        private void OnGUI()
        {
            if (tabs == null || tabs.Count == 0)
            {
                GUILayout.FlexibleSpace(); // Push content down to center vertically

                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace(); // Center content horizontally

                GUILayout.BeginVertical();
                GUILayout.Label(AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Cowsins/UI/Logo/FPS_Engine_Logo_White.png"), GUILayout.Width(200), GUILayout.Height(30));
                GUILayout.Label("Please refresh the Cowsins Manager", GUILayout.Width(250));

                if (GUILayout.Button("Refresh", GUILayout.Width(100)))
                {
                    InitializeTabs();
                }

                GUILayout.EndVertical();

                GUILayout.FlexibleSpace(); // Center content horizontally
                GUILayout.EndHorizontal();

                GUILayout.FlexibleSpace(); // Push content up to center vertically
                return;
            }


            GUIStyle darkBackgroundStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = CreateColoredTexture(darkColor) }
            };

            GUILayout.BeginVertical(darkBackgroundStyle);

            GUILayout.Label(AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Cowsins/UI/Logo/FPS_Engine_Logo_White.png"), GUILayout.Width(200), GUILayout.Height(30));

            GUILayout.BeginHorizontal();

            for (int i = 0; i < tabs.Count; i++)
            {
                if (GUILayout.Toggle(selectedTabIndex == i, tabs[i].TabName, "Button", GUILayout.ExpandWidth(true)))
                {
                    if (selectedTabIndex != i) // Only call Start if the tab changes
                    {
                        selectedTabIndex = i;
                        tabs[selectedTabIndex].StartTab(); // Call Start method of the new tab
                    }
                }
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            GUILayout.EndVertical();

            GUILayout.Space(5);

            scrollPosition = GUILayout.BeginScrollView(scrollPosition);
            tabs[selectedTabIndex].OnGUI();
            GUILayout.EndScrollView();
        }
        Texture2D CreateColoredTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }
    }
}
#endif
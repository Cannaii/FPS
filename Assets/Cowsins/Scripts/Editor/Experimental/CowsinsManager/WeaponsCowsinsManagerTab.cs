#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace cowsins
{
    public class WeaponsTab : ITab
    {
        public string TabName => "Weapons";

        private Texture2D weaponsImage, supportImage;

        #region WEAPON_LISTING_VARIABLES

        private int gridColumns = 4;

        private string searchQuery = "";
        private List<Weapon_SO> filteredWeaponList;
        private List<Weapon_SO> weaponSOList = new List<Weapon_SO>();
        
        private Vector2 scrollPos;

        #endregion

        #region WEAPON_CREATION_ASSISTANT_VARIABLES

        private int tabIndex = 1;
        private WeaponCreatorAssistant weaponCreatorAssistantInstance;

        #endregion

        public void StartTab() { }

        public void OnGUI()
        {
            if (weaponSOList.Count == 0)
            {
                LoadWeaponSOFiles();
            }

            if(weaponsImage == null) 
            {
                weaponsImage = Resources.Load<Texture2D>("CustomEditor/weapon-tutorial");   
                supportImage = Resources.Load<Texture2D>("CustomEditor/discord-support");
            }

            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(GUILayout.Width(EditorGUIUtility.currentViewWidth * .5f));

            string shortcutKey = Application.platform == RuntimePlatform.OSXEditor ? "Cmd+Q" : "Ctrl+Q";
            EditorGUILayout.HelpBox($"\nTIP: You can open the Cowsins Manager directly by pressing {shortcutKey}.\n", MessageType.Info);

            tabIndex = GUILayout.Toolbar(tabIndex, new string[] { "Get Started", "Weapon Creation Assistant" });

            switch (tabIndex)
            {
                case 0:
                    EditorGUILayout.HelpBox($"\nDo you need help to get started? No worries! Learn how to make new Weapons in FPS Engine: \n", MessageType.Info);

                    CowsinsEditorWindowUtilities.DrawTutorialCard(weaponsImage, "https://www.cowsins.com/videos/OykOx8CgEMc", 1);

                    GUI.backgroundColor = Color.white;

                    GUILayout.Space(10);
                    EditorGUILayout.HelpBox($"\nDo you still need help? Join us on Discord and ask for support!\n", MessageType.Info);

                    GUILayout.Space(5);
                    GUILayout.Label(supportImage, GUILayout.Width(400), GUILayout.Height(105));
                    if (GUILayout.Button("Join Discord Server")) Application.OpenURL("https://discord.gg/759gSeTT9m");
                    GUILayout.Space(5);
                    break;
                case 1:
                    if (weaponCreatorAssistantInstance == null)
                    {
                        weaponCreatorAssistantInstance = ScriptableObject.CreateInstance<WeaponCreatorAssistant>();
                    }
                    weaponCreatorAssistantInstance.OnGUI();
                    break;
            }


            GUILayout.EndVertical();

            GUILayout.Space(5);

            GUILayout.BeginVertical(GUILayout.Width(EditorGUIUtility.currentViewWidth * .49f));

            GUILayout.Label("WEAPONS LIST", EditorStyles.boldLabel);

            int buttonSize = (int)(EditorGUIUtility.currentViewWidth * 0.4f / gridColumns);
            int padding = 5;
            int hoveredIndex = -1;
            Rect hoveredRect = new Rect();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Search Weapons:", GUILayout.Width(100));
            searchQuery = GUILayout.TextField(searchQuery);
            GUILayout.EndHorizontal();

            // Filter the weapon list
            filteredWeaponList = weaponSOList
            .Where(weapon => weapon != null && weapon.name.ToLower().Contains(searchQuery.ToLower()))
            .ToList();

            GUILayout.Space(10);
            // Create the grid
            for (int i = 0; i < filteredWeaponList.Count; i += gridColumns)
            {
                GUILayout.BeginHorizontal();

                for (int j = i; j < i + gridColumns && j < filteredWeaponList.Count; j++)
                {
                    Weapon_SO weapon = filteredWeaponList[j];
                    Texture2D icon = AssetPreview.GetAssetPreview(weapon.icon);

                    Rect buttonRect = GUILayoutUtility.GetRect(buttonSize, buttonSize);
                    if (GUI.Button(buttonRect, new GUIContent(icon, weapon.name)))
                    {
                        WeaponButtonClicked(j);
                    }

                    // Detect hover
                    if (buttonRect.Contains(Event.current.mousePosition))
                    {
                        hoveredIndex = j;
                        hoveredRect = buttonRect;
                    }

                    GUILayout.Space(padding);
                }

                GUILayout.EndHorizontal();
                GUILayout.Space(padding);
            }

            // Draw overlay and weapon name if the button is currently hovered
            if (hoveredIndex != -1)
            {
                Weapon_SO hoveredWeapon = weaponSOList[hoveredIndex];

                Color overlayColor = new Color(0, 0, 0, 0.5f);
                EditorGUI.DrawRect(hoveredRect, overlayColor);
                GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    normal = new GUIStyleState() { textColor = Color.white }
                };

                GUI.Label(hoveredRect, $"Edit {hoveredWeapon.name}", labelStyle);

                // Repaint the editor window 
                GUIUtility.ExitGUI();
            }


            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private void WeaponButtonClicked(int index)
        {
            Weapon_SO selectedWeapon = weaponSOList[index];
            EditorUtility.OpenPropertyEditor(selectedWeapon);
        }
        
        private void LoadWeaponSOFiles()
        {
            string[] guids = AssetDatabase.FindAssets("t:Weapon_SO");

            weaponSOList.Clear();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Weapon_SO weapon = AssetDatabase.LoadAssetAtPath<Weapon_SO>(path);
                if (weapon != null)
                {
                    weaponSOList.Add(weapon);
                }
            }
        }
    }

}
#endif
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.AnimatedValues;
using System.Collections.Generic;

namespace cowsins
{
    [CustomEditor(typeof(WeaponIdentification))]
    [InitializeOnLoad]
    public class WeaponIdentificationInspector : Editor
    {

        private string[] tabs = { "Basic", "Attachments" };
        private int currentTab = 0;

        private WeaponIdentification wID;
        private bool hasCameraChild = false;
        private bool hasLightChild = false;
        private Animator[] animatorChildren;
        private bool animatorsFoldout = true;
        private bool customizationFoldout = false;
        
        private static Camera previewCamera;
        private static RenderTexture previewTexture;
        private static bool showPreview = true;
        private static bool showAnims = false;
        private static bool previewInitialized = false;
        private static Vector2 animScrollPos;
        private static bool isPlayingAnim = false;
        private static double lastUpdateTime;
        private static Animator activeAnimator;
        private static AnimationClip activeClip;
        private static float currentClipTime;

        private static AnimBool showPreviewAnim;
        private static AnimBool showAnimsAnim;

        private static void InitializeAnimBools()
        {
            if (showPreviewAnim == null)
            {
                showPreviewAnim = new AnimBool(EditorPrefs.GetBool("Cowsins_ShowWeaponPreview", true));
                showPreviewAnim.valueChanged.AddListener(SceneView.RepaintAll);
                showPreviewAnim.speed = 4f;
            }
            if (showAnimsAnim == null)
            {
                showAnimsAnim = new AnimBool(false);
                showAnimsAnim.valueChanged.AddListener(SceneView.RepaintAll);
                showAnimsAnim.speed = 4f;
            }
        }

        static WeaponIdentificationInspector()
        {
            SceneView.duringSceneGui -= GlobalOnSceneGUI;
            SceneView.duringSceneGui += GlobalOnSceneGUI;
            EditorApplication.update -= EditorUpdate;
            EditorApplication.update += EditorUpdate;
            AssemblyReloadEvents.beforeAssemblyReload += CleanupStaticResources;
        }

        private static void EditorUpdate()
        {
            if (isPlayingAnim && activeAnimator != null && activeClip != null)
            {
                double now = EditorApplication.timeSinceStartup;
                float dt = (float)(now - lastUpdateTime);
                lastUpdateTime = now;

                currentClipTime += dt;
                
                if (currentClipTime >= activeClip.length)
                {
                    if (activeClip.isLooping)
                    {
                        currentClipTime %= activeClip.length;
                    }
                    else
                    {
                        currentClipTime = activeClip.length;
                    }
                }

                activeClip.SampleAnimation(activeAnimator.gameObject, currentClipTime);
                SceneView.RepaintAll();
            }
        }

        private static void CleanupStaticResources()
        {
            if (previewCamera != null)
            {
                DestroyImmediate(previewCamera.gameObject);
            }
            if (previewTexture != null)
            {
                previewTexture.Release();
                DestroyImmediate(previewTexture);
            }
        }

        private void OnEnable()
        {
            wID = (WeaponIdentification)target;
            hasCameraChild = wID.GetComponentInChildren<Camera>(true) != null;
            hasLightChild = wID.GetComponentInChildren<Light>(true) != null;
            animatorChildren = wID.GetComponentsInChildren<Animator>(true);
            if (!previewInitialized)
            {
                showPreview = EditorPrefs.GetBool("Cowsins_ShowWeaponPreview", true);
                previewInitialized = true;
            }
        }

        private static void GlobalOnSceneGUI(SceneView sceneView)
        {
            GameObject activeGO = Selection.activeGameObject;
            if (activeGO == null) return;

            WeaponIdentification activeWID = activeGO.GetComponentInParent<WeaponIdentification>();
            if (activeWID == null) return;

            if (!previewInitialized)
            {
                showPreview = EditorPrefs.GetBool("Cowsins_ShowWeaponPreview", true);
                previewInitialized = true;
            }
            InitializeAnimBools();

            showPreviewAnim.target = showPreview;
            showAnimsAnim.target = showAnims;

            Handles.BeginGUI();
            
            float width = 340f;
            float previewHeight = 190f;
            float animListHeight = 160f;
            float margin = 20f;
            
            float totalHeight = 22f; // Toolbar height
            totalHeight += previewHeight * showPreviewAnim.faded;
            totalHeight += animListHeight * showAnimsAnim.faded;
            
            // Anchor from Bottom-Right
            Rect areaRect = new Rect(sceneView.position.width - width - margin, sceneView.position.height - totalHeight - margin - 30f, width, totalHeight);
            
            // Draw window background
            GUI.Box(areaRect, GUIContent.none, "window");
            
            GUILayout.BeginArea(new Rect(areaRect.x + 2, areaRect.y + 2, areaRect.width - 4, areaRect.height - 4));

            // Custom Toolbar
            EditorGUI.BeginChangeCheck();
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            showPreview = GUILayout.Toggle(showPreview, " Camera Preview", EditorStyles.toolbarButton);
            showAnims = GUILayout.Toggle(showAnims, " Animations", EditorStyles.toolbarButton);
            GUILayout.EndHorizontal();

            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetBool("Cowsins_ShowWeaponPreview", showPreview);
            }

            if (showPreviewAnim.faded > 0.001f)
            {
                if (showPreview && Event.current.type == EventType.Repaint)
                {
                    if (previewCamera == null)
                    {
                        GameObject camObj = new GameObject("WeaponPreviewCamera");
                        camObj.hideFlags = HideFlags.HideAndDontSave;
                        previewCamera = camObj.AddComponent<Camera>();
                        previewCamera.cameraType = CameraType.Preview;
                        previewCamera.clearFlags = CameraClearFlags.Skybox;
                        previewCamera.fieldOfView = 60f;
                        previewCamera.nearClipPlane = 0.01f;
                        previewCamera.enabled = false;
                        
                        previewCamera.scene = activeWID.gameObject.scene;
                    }

                    if (previewCamera.scene != activeWID.gameObject.scene)
                    {
                        previewCamera.scene = activeWID.gameObject.scene;
                    }

                    if (previewTexture == null)
                    {
                        previewTexture = new RenderTexture((int)width, (int)previewHeight, 24, RenderTextureFormat.ARGB32);
                        previewTexture.antiAliasing = 1;
                    }

                    previewCamera.transform.position = activeWID.transform.position;
                    previewCamera.transform.rotation = activeWID.transform.rotation;
                    
                    previewCamera.targetTexture = previewTexture;
                    previewCamera.Render();
                    previewCamera.targetTexture = null;
                }

                GUI.BeginGroup(new Rect(0, 22, width - 4, previewHeight * showPreviewAnim.faded));
                if (previewTexture != null)
                {
                    GUI.DrawTexture(new Rect(2, 2, width - 8, previewHeight - 8), previewTexture, ScaleMode.StretchToFill, false);
                }
                GUI.EndGroup();
                
                GUILayout.Space(previewHeight * showPreviewAnim.faded);
            }

            if (showAnimsAnim.faded > 0.001f)
            {
                Animator currentAnimator = activeWID.Animator;
                if (currentAnimator == null) 
                {
                    currentAnimator = activeWID.GetComponentInChildren<Animator>(true);
                }

                if (currentAnimator != null)
                {
                    AnimatorController ac = null;
                    if (currentAnimator.runtimeAnimatorController is AnimatorOverrideController aoc)
                        ac = aoc.runtimeAnimatorController as AnimatorController;
                    else
                        ac = currentAnimator.runtimeAnimatorController as AnimatorController;

                    if (ac != null)
                    {
                        GUILayout.Space(4);
                        
                        GUIStyle listItemStyle = new GUIStyle(EditorStyles.helpBox);
                        listItemStyle.padding = new RectOffset(4, 4, 4, 4);

                        animScrollPos = GUILayout.BeginScrollView(animScrollPos, GUILayout.Height(animListHeight - 28f));
                        
                        var addedClips = new HashSet<AnimationClip>();
                        foreach (var clip in ac.animationClips)
                        {
                            if (clip != null && addedClips.Add(clip))
                            {
                                GUILayout.BeginHorizontal(listItemStyle);
                                GUILayout.Label(clip.name, EditorStyles.boldLabel);
                                GUILayout.FlexibleSpace();
                                if (GUILayout.Button("► Play", EditorStyles.miniButtonRight, GUILayout.Width(60)))
                                {
                                    activeClip = clip;
                                    currentClipTime = 0f;
                                    if (currentAnimator != null) currentAnimator.enabled = false;
                                    
                                    activeAnimator = currentAnimator;
                                    lastUpdateTime = EditorApplication.timeSinceStartup;
                                    isPlayingAnim = true;
                                }
                                GUILayout.EndHorizontal();
                            }
                        }
                        GUILayout.EndScrollView();

                        GUILayout.Space(2);
                        GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                        GUI.enabled = isPlayingAnim || (currentAnimator != null && !currentAnimator.enabled);
                        if (GUILayout.Button("■ Stop Player", EditorStyles.miniButton, GUILayout.Width(100)))
                        {
                            isPlayingAnim = false;
                            activeClip = null;
                            if (currentAnimator != null)
                            {
                                currentAnimator.Rebind();
                                currentAnimator.Update(0f);
                                currentAnimator.enabled = true;
                            }
                        }
                        GUI.enabled = true;
                        GUILayout.EndHorizontal();
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("Requires an assigned Animator Controller.", MessageType.Warning);
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("Animator not found on children.", MessageType.Error);
                }
            }
            
            GUILayout.EndArea();
            Handles.EndGUI();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            Texture2D myTexture = Resources.Load<Texture2D>("CustomEditor/weaponIdentification_CustomEditor") as Texture2D;
            GUILayout.Label(myTexture);

            if (hasCameraChild)
            {
                EditorGUILayout.HelpBox("A Camera has been found in this Weapon Prefab. This is not allowed.", MessageType.Error);
                GUILayout.Space(10);
            }
            if (hasLightChild)
            {
                EditorGUILayout.HelpBox("A Light Source has been found in this Weapon Prefab. Ignore this message if this is intentional.", MessageType.Warning);
                GUILayout.Space(10);
            }
            if (animatorChildren != null && animatorChildren.Length > 0)
            {
                EditorGUILayout.HelpBox($"Found {animatorChildren.Length} Animator(s) in children.", MessageType.Info);
                GUILayout.Space(10);
            }

            currentTab = GUILayout.Toolbar(currentTab, tabs);

            if (currentTab >= 0 || currentTab < tabs.Length)
            {
                switch (tabs[currentTab])
                {
                    case "Basic":
                        EditorGUILayout.Space(20f);
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("weapon"));

                        EditorGUILayout.PropertyField(serializedObject.FindProperty("FirePoint"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("aimPoint"));
                        EditorGUILayout.Space(10f);
                        EditorGUILayout.LabelField("You can leave �headBone� unassigned if your camera does not move during your Weapon Animations.", EditorStyles.helpBox);
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("headBone"));
                        if (animatorChildren != null && animatorChildren.Length > 0)
                        {
                            animatorsFoldout = EditorGUILayout.Foldout(animatorsFoldout, "Animators List", true);

                            if (animatorsFoldout)
                            {
                                EditorGUI.indentLevel++;

                                foreach (Animator animator in animatorChildren)
                                {
                                    EditorGUILayout.BeginHorizontal();

                                    EditorGUI.BeginDisabledGroup(true);
                                    // non-selectable
                                    EditorGUILayout.ObjectField(animator.gameObject.name, animator, typeof(Animator), true);
                                    EditorGUI.EndDisabledGroup();

                                    if (GUILayout.Button("See", GUILayout.Width(40)))
                                    {
                                        Selection.activeObject = animator.gameObject;
                                        EditorGUIUtility.PingObject(animator.gameObject);
                                    }

                                    EditorGUILayout.EndHorizontal();
                                }

                                EditorGUI.indentLevel--;
                            }
                        }

                        EditorGUILayout.Space(10f);
                        customizationFoldout = EditorGUILayout.Foldout(customizationFoldout, "Additional Customization", true);

                        if (customizationFoldout)
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.HelpBox("Shell Eject Point is optional. If left empty, Fire Point will be used.", MessageType.Info);
                            EditorGUILayout.PropertyField(serializedObject.FindProperty("shellEjectPoint"));
                            EditorGUI.indentLevel--;
                        }
                        break;
                    case "Attachments":
                        EditorGUILayout.Space(5f);

                        GUILayout.BeginHorizontal();
                        CowsinsEditorWindowUtilities.DrawLinkCard(Resources.Load<Texture2D>("CustomEditor/CowsinsManager/documentationIcon"), "Documentation", "https://cowsinss-organization.gitbook.io/fps-engine-documentation/how-to-use/working-with-attachments", .77f, .4f);
                        GUILayout.FlexibleSpace();
                        CowsinsEditorWindowUtilities.DrawLinkCard(Resources.Load<Texture2D>("CustomEditor/CowsinsManager/tutorialsIcon"), "Tutorial", "https://www.cowsins.com/videos/BuJ3d4ssMdQ", .77f, .4f);
                        GUILayout.FlexibleSpace();
                        CowsinsEditorWindowUtilities.DrawLinkCard(Resources.Load<Texture2D>("CustomEditor/CowsinsManager/supportIcon"), "Support", "https://discord.gg/759gSeTT9m", .77f, .4f);
                        GUILayout.Space(10);
                        GUILayout.EndHorizontal();

                        EditorGUILayout.Space(20f);
                        EditorGUILayout.HelpBox("Original / Default Attachments: Iron Sights, Original Magazines, etc...", MessageType.Info);
                        EditorGUILayout.Space(5f);
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultAttachments").FindPropertyRelative("defaultAttachmentsList"));

                        EditorGUILayout.Space(10f);
                        EditorGUILayout.HelpBox("Define All Available Attachments for this Weapon, including Default Attachments.", MessageType.Info);
                        EditorGUILayout.Space(5f);
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("compatibleAttachments").FindPropertyRelative("compatibleAttachmentsList"));

                        EditorGUILayout.Space(10f);
                        AutomaticAttachmentButton();

                        // Attachment State Visualization during the game
                        if (Application.isPlaying)
                        {
                            EditorGUILayout.Space(20f);
                            DrawRuntimeAttachmentState();
                        }

                        break;
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void AutomaticAttachmentButton()
        {
            if (GUILayout.Button("Automatically Assign Attachments", GUILayout.Height(35)))
            {
                var attachments = wID.GetComponentsInChildren<Attachment>(true);

                // Create a Temporary Dictionary for the found attachments and map them to their corresponding Attachment Type
                var groupedAttachments = new Dictionary<AttachmentType, List<Attachment>>();

                foreach (var attachment in attachments)
                {
                    var atcId = attachment.attachmentIdentifier;
                    if (atcId == null)
                    {
                        Debug.LogError($"<color=red>[COWSINS]</color> Attachment Identifier is null in {attachment}. Please, assign an attachment Identifier.", attachment);
                        continue;
                    }
                    var type = attachment.attachmentIdentifier.attachmentType;

                    if (!groupedAttachments.ContainsKey(type))
                        groupedAttachments[type] = new List<Attachment>();

                    groupedAttachments[type].Add(attachment);
                }

                // Gather Compatible Attachments 
                SerializedProperty compatibleListProp = serializedObject.FindProperty("compatibleAttachments")
                    .FindPropertyRelative("compatibleAttachmentsList");

                // Clear Compatible Attachments
                compatibleListProp.ClearArray();

                // Repopulate Compatible Attachments based on the Temporary Dictionary we just created
                int index = 0;
                foreach (var kvp in groupedAttachments)
                {
                    compatibleListProp.InsertArrayElementAtIndex(index);
                    SerializedProperty entryProp = compatibleListProp.GetArrayElementAtIndex(index);

                    entryProp.FindPropertyRelative("type").enumValueIndex = (int)kvp.Key;

                    var attachmentsArray = entryProp.FindPropertyRelative("attachments");
                    attachmentsArray.ClearArray();

                    for (int i = 0; i < kvp.Value.Count; i++)
                    {
                        attachmentsArray.InsertArrayElementAtIndex(i);
                        attachmentsArray.GetArrayElementAtIndex(i).objectReferenceValue = kvp.Value[i];
                    }

                    index++;
                }

                // Save the Changes
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(wID);

            }
        }

        // Shows current, default, and compatible attachments during runtime
        private void DrawRuntimeAttachmentState()
        {
            if (wID.AttachmentState == null) return;

            var state = wID.AttachmentState;

            // Calculate summary
            int totalSlots = System.Enum.GetValues(typeof(AttachmentType)).Length;
            int equippedCount = state.GetCurrentCount();
            int defaultCount = 0;
            int customCount = 0;

            foreach (AttachmentType type in System.Enum.GetValues(typeof(AttachmentType)))
            {
                var attachmentState = state.GetState(type);
                if (attachmentState == AttachmentState.Default) defaultCount++;
                else if (attachmentState == AttachmentState.Custom) customCount++;
            }

            int emptyCount = totalSlots - equippedCount;
            string summaryText = $"Attachments: {equippedCount}/{totalSlots} equipped ({defaultCount} default, {customCount} custom, {emptyCount} empty)";

            EditorGUILayout.LabelField("Runtime Attachment State", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(summaryText, MessageType.Info);

            EditorGUILayout.Space(5f);

            // Draw each attachment type
            foreach (AttachmentType type in System.Enum.GetValues(typeof(AttachmentType)))
            {
                DrawAttachmentTypeState(state, type);
            }

            EditorGUILayout.Space(10f);

            Repaint();
        }

        private void DrawAttachmentTypeState(AttachmentStateManager state, AttachmentType type)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            var attachmentState = state.GetState(type);
            var current = state.GetCurrent(type);
            var compatibleCount = state.GetCompatibleCount(type);

            Color stateColor = attachmentState switch
            {
                AttachmentState.None => Color.gray,
                AttachmentState.Default => new Color(0.5f, 0.8f, 1f),
                AttachmentState.Custom => new Color(0.5f, 1f, 0.5f),
                _ => Color.white
            };

            var originalColor = GUI.color;
            GUI.color = stateColor;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"  {type}", EditorStyles.boldLabel, GUILayout.Width(100));
            EditorGUILayout.LabelField($"{attachmentState}", GUILayout.Width(70));

            GUI.color = originalColor;

            // Show current attachment
            if (current != null)
            {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.ObjectField(current, typeof(Attachment), true);
                EditorGUI.EndDisabledGroup();
            }
            else
            {
                EditorGUILayout.LabelField($"Empty ({compatibleCount} compatible)", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

    }
}
#endif
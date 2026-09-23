using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UI;
#endif

namespace cowsins
{

    [System.Serializable]
    public class CowsinsButton : Button
    {
        public enum ButtonType
        {
            GameObjectTransition,
            SceneTransition,
            Other
        }

        [SerializeField, Tooltip("Select the behaviour of the button.")]
        private ButtonType buttonType;

        [SerializeField, Tooltip("GameObject to enable.You can add a CanvasGroup to this object to seamlessly play a fade in effect")]
        private GameObject gameObjectToEnable;

        [SerializeField, Tooltip("GameObjects to disable. No fade effect is played on these.")]
        private GameObject[] gameObjectsToDisable;

        [SerializeField, Tooltip("Sound effect when hovered")]
        private AudioClip hoverSFX;

        [SerializeField, Tooltip("Sound effect when clicked")]
        private AudioClip clickSFX;

#if UNITY_EDITOR
        [SerializeField, Tooltip("Scene asset to load (Editor only)")]
        private SceneAsset sceneAsset;
#endif

        [SerializeField, HideInInspector] private string sceneName;

        // Button Type Getter
        public ButtonType _ButtonType => buttonType;

        protected override void Start()
        {
            base.Start();
            // Run Button Click When the Button is Clicked
            this.onClick.AddListener(ButtonClick);
        }

        public void ButtonClick()
        {
            // Play sound
            SoundManager.Instance?.PlaySound(clickSFX,0,0,false);

            // Handle GameObjects Transitions
            if (buttonType == ButtonType.GameObjectTransition)
            {
                // Foreach gameobject that we need to disable, disable it.
                foreach (GameObject go in gameObjectsToDisable) go.SetActive(false);
                // Gather the CanvasGroup component of the game object we want to enable
                CanvasGroup canvasGroup = gameObjectToEnable.GetComponent<CanvasGroup>();
                // If the CanvasGroup exists begin animation
                if (canvasGroup)
                {
                    if (canvasGroup.alpha < 1) canvasGroup.alpha = 0;
                    MainMenuManager.Instance?.SetObjectToLerp(canvasGroup);
                }
                // Enable the gameobject
                gameObjectToEnable.SetActive(true);
            }
            else if (buttonType == ButtonType.SceneTransition)
            {
                if (!string.IsNullOrEmpty(sceneName))
                    SceneManager.LoadScene(sceneName);
                else
                    CowsinsUtilities.LogWarning("Scene not assigned for CowsinsButton.", this);
            }
        }
        public override void OnPointerEnter(PointerEventData eventData)
        {
            if(interactable)
                SoundManager.Instance?.PlaySound(hoverSFX, 0, 0, false);
            base.OnPointerEnter(eventData);
        }


#if UNITY_EDITOR
        // Property to allow editor access
        public SceneAsset _SceneAsset => sceneAsset;
        protected override void OnValidate()
        {
            if (sceneAsset != null && sceneName != sceneAsset.name)
                sceneName = sceneAsset.name;

            base.OnValidate();  
        }
#endif
    }
#if UNITY_EDITOR
    [CustomEditor(typeof(CowsinsButton))]
    public class CowsinsButtonEditor : ButtonEditor
    {

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            CowsinsButton myScript = target as CowsinsButton;
            EditorGUILayout.LabelField("COWSINS BUTTON", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("hoverSFX"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("clickSFX"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("buttonType"));
            if (myScript._ButtonType == CowsinsButton.ButtonType.GameObjectTransition)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("gameObjectToEnable"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("gameObjectsToDisable"));
            }
            else if (myScript._ButtonType == CowsinsButton.ButtonType.SceneTransition)
            {
                SerializedProperty sceneAssetProp = serializedObject.FindProperty("sceneAsset");
                EditorGUILayout.PropertyField(sceneAssetProp, new GUIContent("Scene To Load"));
            }

            EditorGUILayout.Space(10);
            GUILayout.Box("", new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.Height(8) });
            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("UNITY BASE BUTTON", EditorStyles.boldLabel);
            serializedObject.ApplyModifiedProperties();

            base.OnInspectorGUI();

        }
    }
#endif
}
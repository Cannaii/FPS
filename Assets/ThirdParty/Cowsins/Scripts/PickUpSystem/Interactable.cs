/// <summary>
/// This script belongs to cowsins™ as a part of the cowsins´ FPS Engine. All rights reserved. 
/// </summary>
using UnityEngine;
using UnityEngine.Events;
using UnityEditor;
namespace cowsins
{
    /// <summary>
    /// This component must be add to ANY interactable object in your environment.
    /// However, you might want to check other examples such as DoorInteractable.cs or PickUps
    /// They are different things, they do different things, but they both are INTERACTABLES and you can interact with both of them.
    /// Then, how does that work? Their custom scripts both inherit from interactable.cs, which inherits from MonoBehaviour, so even though they do 
    /// different things they are actually the same kind of thing.
    /// Since Interact() is a virtual void you can override it on your custom script inheriting from this one
    /// 
    /// For any doubts check the documentation or contact the support.
    /// 
    /// </summary>
    public partial class Interactable : Identifiable
    {
        [HideInInspector] public bool interactable = false;

        [Tooltip("Text that will be displayed on the Interaction UI"), Title("INTERACTABLE")]
        public string interactText;

        [SerializeField] private bool instantInteraction = false;

        public bool InstantInteraction => instantInteraction;

        [System.Serializable]
        public class InteractableEvents // Store your events
        {
            public UnityEvent OnInteract;
        }

        public InteractableEvents interactableEvents;

        [SaveField] protected bool alreadyInteracted = false;

        /// <summary>
        /// Make sure to override this on your new custom class.
        /// If you still wanna call this method, make sure to write the following line:
        /// base.Interact(player);
        /// </summary>
        public virtual void Interact(Transform player)
        {
            interactableEvents.OnInteract?.Invoke();
            alreadyInteracted = true;
#if SAVE_LOAD_ADD_ON
            StoreData();
#endif
        }

        public virtual void Highlight()
        {

        }
        public virtual void Unhighlight()
        {

        }

        public virtual bool IsForbiddenInteraction(IWeaponReferenceProvider weaponController)
        {
            return false; 
        }
    }


#if UNITY_EDITOR
    [System.Serializable]
    [CustomEditor(typeof(Interactable))]
    public class InteractableEditor : Editor
    {

        override public void OnInspectorGUI()
        {
            serializedObject.Update();
            Interactable myScript = target as Interactable;
            EditorGUILayout.Space(5);
            if (myScript.gameObject.layer != LayerMask.NameToLayer("Interactable"))
            {
                EditorGUILayout.HelpBox("Ensure your custom interactable object has the `Interactable` layer assigned.", MessageType.Error);
            }
            EditorGUILayout.Space(5);
            if (!myScript.gameObject.GetComponent<Collider>())
            {
                EditorGUILayout.HelpBox("Ensure your custom interactable object has a COLLIDER component assigned.", MessageType.Error);
            }
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("INTERACTABLE", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(serializedObject.FindProperty("interactText"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("interactableEvents"));
            serializedObject.ApplyModifiedProperties();

        }
    }
#endif
}
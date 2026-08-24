using UnityEngine;
#if SAVE_LOAD_ADD_ON
using cowsins.SaveLoad;
using Newtonsoft.Json.Linq;
#endif
using System.Collections.Generic;
using UnityEngine.SceneManagement;


#if UNITY_EDITOR
using UnityEditor;
#endif

namespace cowsins
{
    public class Identifiable : MonoBehaviour
    {
        [SerializeField, HideInInspector]
        protected string uniqueID;

        public string UniqueIDValue
        {
            get
            {
                // Generate an ID at runtime if it's not already assigned
                if (string.IsNullOrEmpty(uniqueID))
                {
                    GenerateUniqueID();
                }
                return uniqueID;
            }
        }

#if UNITY_EDITOR
        public virtual void OnValidate()
        {
            // Detect duplicates in the Editor by checking if the uniqueID is already set.
            // If it's set and we�re not in play mode, then we might have a duplicate.
            if (!Application.isPlaying && (HasDuplicateID() || string.IsNullOrEmpty(uniqueID)))
            {
                GenerateUniqueID();
            }
        }

        private bool HasDuplicateID()
        {
            // Check if any other Identifiable object has the same uniqueID
            if (string.IsNullOrEmpty(uniqueID)) return false;
            
#pragma warning disable CS0618
            Identifiable[] allIdentifiables = FindObjectsOfType<Identifiable>();
#pragma warning restore CS0618
            foreach (Identifiable identifiable in allIdentifiables)
            {
                if (identifiable != this && !string.IsNullOrEmpty(identifiable.uniqueID) && identifiable.uniqueID == uniqueID)
                {
                    return true;
                }
            }
            return false;
        }
#endif

        public void GenerateUniqueID()
        {
            uniqueID = System.Guid.NewGuid().ToString();
            // Debug.Log($"Assigned new UniqueID: {uniqueID}", this);

#if UNITY_EDITOR
            // Mark the object as dirty to save the new ID in the editor
            EditorUtility.SetDirty(this);
#endif
        }

        public void ReplaceUniqueID(string newUniqueID)
        {
            uniqueID = newUniqueID;

#if UNITY_EDITOR
            // Mark the object as dirty to save the new ID in the editor
            EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>
        /// Finds an Identifiable object by its unique ID in the scene.
        /// </summary>
        /// <param name="id">The unique ID to look for.</param>
        /// <returns>The Identifiable object if found, otherwise null.</returns>
        public static Identifiable FindByID(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
#pragma warning disable CS0618
            Identifiable[] allIdentifiables = FindObjectsOfType<Identifiable>();
#pragma warning restore CS0618
            foreach (Identifiable identifiable in allIdentifiables)
            {
                if (identifiable.UniqueIDValue == id)
                {
                    return identifiable;
                }
            }

            return null;
        }

#if SAVE_LOAD_ADD_ON
        public virtual CustomSaveData SaveFields()
        {
            CustomSaveData saveData = new CustomSaveData(SceneManager.GetActiveScene().name, this.transform.position);
            saveData = (CustomSaveData)saveData.SaveFields(this);
            return saveData;
        }

        public virtual void LoadFields(object data)
        {
            if (data is CustomSaveData gameData)
            {
                gameData.LoadFields(this, gameData);
            }

            LoadedState();
        }

        public virtual void StoreData()
        {
            if (GameDataManager.instance == null) return;

            if (GameDataManager.instance.customObjectsData == null)
            {
                GameDataManager.instance.customObjectsData = new Dictionary<string, CustomSaveData>();
            }
            GameDataManager.instance.customObjectsData[this.uniqueID] = SaveFields();
        }

        public virtual void SaveInstance()
        {
            if (GameDataManager.instance)
            {
                var saveData = SaveFields();
                GenerateUniqueID();
                saveData.Position = new SerializableVector3(transform.position);
                GameDataManager.instance.instantiatedObjects[UniqueIDValue] = saveData;
            }
        }

        public virtual void LoadedState()
        {

        }
#endif
    }
}
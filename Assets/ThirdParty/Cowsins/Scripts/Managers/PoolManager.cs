using UnityEngine;
using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace cowsins
{
    public class PoolManager : MonoBehaviour
    {
        public static PoolManager Instance;

        [SerializeField, Title("Pool Sizes")] private int defaultSize = 5;
        [SerializeField] private int weaponEffectsSize;
        [SerializeField] private int damagePopUpsSize;
        [SerializeField] private int bulletGraphicsSize;

        [SerializeField, Title("Return Times", upMargin = 10)] private float defaultReturnTime = 3f;
        [SerializeField] private float damagePopUpsReturnTime = .4f;    

        // GETTERS
        public int WeaponEffectsSize => weaponEffectsSize;
        public int DamagePopUpsSize => damagePopUpsSize;
        public int BulletGraphicsSize => bulletGraphicsSize;
        public float DefaultReturnTime => defaultReturnTime;
        public float DamagePopUpsReturnTime => damagePopUpsReturnTime;

        // INTERNAL USE
        private Dictionary<GameObject, Queue<GameObject>> poolDictionary = new Dictionary<GameObject, Queue<GameObject>>();
        private Dictionary<GameObject, List<GameObject>> activeObjects = new Dictionary<GameObject, List<GameObject>>();

        private void Awake()
        {
            // Initialize singleton
            if (Instance == null) Instance = this;
            else Destroy(this.gameObject);
        }

        /// <summary>
        /// Registers a new pool given a prefab and the size of the collection.
        /// </summary>
        public void RegisterPool(GameObject prefab, int size)
        {
            if (prefab == null)
            {
                return;
            }
            // Avoid registering duplicates
            if (poolDictionary.ContainsKey(prefab))
            {
                return;
            }

            Queue<GameObject> objectQueue = new Queue<GameObject>();
            List<GameObject> activeList = new List<GameObject>();

            for (int i = 0; i < size; i++)
            {
                GameObject obj = Instantiate(prefab, transform);
                obj?.SetActive(false);
                objectQueue.Enqueue(obj);
            }

            poolDictionary[prefab] = objectQueue;
            activeObjects[prefab] = activeList;
        }
        public GameObject GetFromPool(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            return GetFromPool(prefab, position, rotation, defaultReturnTime);
        }

        public GameObject GetFromPool(GameObject prefab, Vector3 position, Quaternion rotation, float returnToPool)
        {
            if (prefab == null)
            {
                return null;
            }
            if (!poolDictionary.ContainsKey(prefab))
            {
                // Default size if not preregistered
                RegisterPool(prefab, defaultSize);
            }

            while (poolDictionary[prefab].Count > 0)
            {
                GameObject obj = poolDictionary[prefab].Dequeue();
                if (obj != null) 
                {
                    obj.transform.position = position;
                    obj.transform.rotation = rotation;
                    obj.SetActive(true);

                    if (activeObjects.ContainsKey(prefab))
                        activeObjects[prefab].Add(obj);

                    HandleVFXAutoReturn(obj, prefab, returnToPool);
                    return obj;
                }
            }

            ExpandPool(prefab);
            return GetFromPool(prefab, position, rotation, returnToPool);
        }

        public void ReturnToPool(GameObject obj, GameObject prefab)
        {
            if (obj == null || prefab == null) return;

            if (!poolDictionary.ContainsKey(prefab))
            { 
                // Pool for this prefab no longer exists, destroy the object.
                Destroy(obj);
                return;
            }

            obj.SetActive(false);
            obj.transform.SetParent(this.transform);
            poolDictionary[prefab].Enqueue(obj);

            if (activeObjects.ContainsKey(prefab))
                activeObjects[prefab].Remove(obj);
        }

        private void ExpandPool(GameObject prefab)
        {
            GameObject obj = Instantiate(prefab, transform);
            obj.SetActive(false);
            poolDictionary[prefab].Enqueue(obj);
        }

        private void HandleVFXAutoReturn(GameObject obj, GameObject prefab, float returnToPool)
        {
            ParticleSystem ps = obj.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Play();
                // Automatically return to pool after VFX duration
                if (returnToPool > 0) StartCoroutine(ReturnAfterDelay(obj, prefab, ps.main.duration));
            }
            else
            {
                // If it's not a VFX, use a default delay
                if (returnToPool > 0) StartCoroutine(ReturnAfterDelay(obj, prefab, returnToPool));
            }
        }

        private IEnumerator ReturnAfterDelay(GameObject obj, GameObject prefab, float delay)
        {
            yield return new WaitForSeconds(delay);

            ReturnToPool(obj, prefab);
        }

        /// <summary>
        /// Removes all objects from a specific pool.
        /// </summary>
        public void RemovePool(GameObject prefab)
        {
            if (!poolDictionary.ContainsKey(prefab))
            {
                return;
            }

            // Disable and destroy active objects
            foreach (var obj in activeObjects[prefab])
            {
                Destroy(obj);
            }
            activeObjects.Remove(prefab);

            // Destroy all pooled objects
            foreach (var obj in poolDictionary[prefab])
            {
                Destroy(obj);
            }
            poolDictionary.Remove(prefab);
        }
    }
}

#if UNITY_EDITOR
namespace cowsins
{

    [CustomEditor(typeof(PoolManager))]
    public class PoolManagerEditor : Editor
    {
        override public void OnInspectorGUI()
        {
            serializedObject.Update();
            var myScript = target as PoolManager;

            DrawDefaultInspector();

            EditorGUILayout.Space(15);
            EditorGUILayout.LabelField("Pool Manager Information", EditorStyles.boldLabel);
            
            EditorGUILayout.LabelField("Pool Manager is a system that enhances the performance of your game. Instead of constantly instantiating and destroying objects such as bullets, " +
            "impacts, or other visual effects, the Pool Manager stores an array of them and enables and disables them based on the Player needs, reducing Garbage Collection overhead and enhances overall efficiency.", EditorStyles.helpBox);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
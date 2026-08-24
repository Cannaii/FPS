/// <summary>
/// This script belongs to cowsins™ as a part of the cowsins´ FPS Engine. All rights reserved. 
/// </summary>
using UnityEngine;
namespace cowsins
{
    public class DestroyMe : MonoBehaviour
    {
        public float timeToDestroy;

        private void Start() => Invoke("DestroyMeObj", timeToDestroy);

        private void DestroyMeObj() => Destroy(this.gameObject);

    }
}
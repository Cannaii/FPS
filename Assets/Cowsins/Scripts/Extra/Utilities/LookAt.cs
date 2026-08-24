/// <summary>
/// This script belongs to cowsins™ as a part of the cowsins´ FPS Engine. All rights reserved. 
/// </summary>
using UnityEngine;
namespace cowsins
{
    public class LookAt : MonoBehaviour
    {
        [SerializeField] private Transform player;

        private Vector3 lookAtPos;

        private void Awake()
        {
            if (player == null)
            {
                GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null)
                    player = playerObj.transform;
            }
        }

        private void Update()
        {
            if (player == null) return;

            Vector3 currentPos = transform.position;
            lookAtPos = new Vector3(player.position.x, currentPos.y, player.position.z);
            transform.rotation = Quaternion.LookRotation(lookAtPos - currentPos);
        }
    }
}
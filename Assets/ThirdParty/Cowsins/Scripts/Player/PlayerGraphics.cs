using UnityEngine;
namespace cowsins
{
    public class PlayerGraphics : MonoBehaviour
    {
        [SerializeField] private PlayerDependencies playerDependencies;

        private Transform playerTransform;
        private IPlayerMovementStateProvider playerMovementStateProvider; // IPlayerMovementStateProvider is implemented in PlayerMovement.cs

        private void Start()
        {
            playerTransform = playerDependencies.transform;
            playerMovementStateProvider = playerDependencies.PlayerMovementState;
        }
        private void Update()
        {
            transform.position = playerTransform.position;
            transform.rotation = playerMovementStateProvider.Orientation.Rotation;
        }
    }
}
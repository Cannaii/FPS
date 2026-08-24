using UnityEngine;
using UnityEngine.SceneManagement;
namespace cowsins
{
    public class DeathRestart : MonoBehaviour
    {
        [SerializeField] private PlayerDependencies playerDependencies;

        private InputManager inputManager;

        private void Awake() => inputManager = playerDependencies.InputManager;
        private void Update()
        {
            if (inputManager.Reloading) SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
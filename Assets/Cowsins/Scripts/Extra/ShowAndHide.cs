using UnityEngine;
using UnityEngine.InputSystem;
namespace cowsins
{
    public class ShowAndHide : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private InputActionReference toggleAction;

        private void OnEnable()
        {
            toggleAction.action.Enable();
            toggleAction.action.performed += TogglePanel;
        }

        private void OnDisable()
        {
            toggleAction.action.Disable();
            toggleAction.action.performed -= TogglePanel;
        }

        private void TogglePanel(InputAction.CallbackContext context) => panel.SetActive(!panel.activeSelf);
    }
}
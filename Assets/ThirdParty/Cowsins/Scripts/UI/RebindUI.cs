using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

namespace cowsins
{
    public class RebindUI : MonoBehaviour
    {
        [SerializeField]
        private InputActionReference inputActionReference;

        [SerializeField]
        private bool excludeMouseFromBinding = true;

        [Range(0, 15), SerializeField]
        private int selectedBinding;

        [SerializeField]
        private InputBinding.DisplayStringOptions displayStringOptions;

        [SerializeField, ReadOnly]
        private InputBinding inputBinding;

        private int bindingIndex;

        private string actionName;

        [SerializeField] private TextMeshProUGUI actionText;

        [SerializeField] private Button rebindButton;

        [SerializeField] private TextMeshProUGUI rebindText;

        [SerializeField] private Button resetButton;

        [SerializeField] private GameObject rebindOverlay;

        [SerializeField] private TextMeshProUGUI rebindOverlayTitle;

        private void OnValidate()
        {
            if (inputActionReference == null) return;

            GetBindingInfo();
            UpdateUI();
        }

        private void OnEnable()
        {
            rebindButton.onClick.AddListener(PerformRebind);
            resetButton.onClick.AddListener(ResetRebind);
        }

        private void Awake()
        {
            if (inputActionReference == null) return;

            GetBindingInfo();

            InputManager.LoadBindingOverride(actionName);

            UpdateUI();

            InputManager.rebindComplete += UpdateUI;
            InputManager.rebindCanceled += UpdateUI;
        }

        private void OnDisable()
        {
            rebindButton.onClick.RemoveListener(PerformRebind);
            resetButton.onClick.RemoveListener(ResetRebind);
        }

        private void OnDestroy()
        {
            InputManager.rebindComplete -= UpdateUI;
            InputManager.rebindCanceled -= UpdateUI;
        }
        
        private void GetBindingInfo()
        {
            if (inputActionReference.action != null)
                actionName = inputActionReference.action.name;

            if (inputActionReference.action.bindings.Count > selectedBinding)
            {
                inputBinding = inputActionReference.action.bindings[selectedBinding];
                bindingIndex = selectedBinding;
            }
        }

        private void UpdateUI()
        {
            if (actionText != null)
                actionText.text = actionName;

            if (rebindText != null)
            {
                if (Application.isPlaying)
                {
                    rebindText.text = InputManager.GetBindingName(actionName, bindingIndex);
                }
                else
                {
                    rebindText.text = inputActionReference.action.GetBindingDisplayString(bindingIndex);
                }
            }
        }

        private void PerformRebind()
        {
            InputManager.StartRebind(actionName, bindingIndex, rebindText, excludeMouseFromBinding, rebindOverlay, rebindOverlayTitle);
        }

        private void ResetRebind()
        {
            InputManager.ResetBinding(actionName, bindingIndex);

            UpdateUI();
        }
    }
}

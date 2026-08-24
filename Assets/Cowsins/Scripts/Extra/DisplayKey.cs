
using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;
namespace cowsins
{
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class DisplayKey : MonoBehaviour
    {
        private TextMeshProUGUI txt;

        private string displayString, currentDeviceGroup;

        private void Awake()
        {
            txt = GetComponent<TextMeshProUGUI>();

            if (DeviceDetection.Instance != null)
                Repaint(DeviceDetection.Instance.mode);
        }


        private void OnEnable()
        {
            if (DeviceDetection.Instance != null)
                DeviceDetection.Instance.OnInputModeChanged += Repaint;
            InputManager.rebindComplete += OnRebindComplete;
        }

        private void OnDisable()
        {
            if (DeviceDetection.Instance != null)
                DeviceDetection.Instance.OnInputModeChanged -= Repaint;
            InputManager.rebindComplete -= OnRebindComplete;
        }

        private void OnRebindComplete()
        {
            if (DeviceDetection.Instance != null)
                Repaint(DeviceDetection.Instance.mode);
        }

        public void Repaint(DeviceDetection.InputMode inputMode)
        {
            if (InputManager.inputActions == null) return;
            currentDeviceGroup = inputMode == DeviceDetection.InputMode.Keyboard ? "Keyboard" : "Gamepad";
            displayString = InputManager.inputActions.GameControls.Interacting.GetBindingDisplayString(InputBinding.MaskByGroup(currentDeviceGroup));
            txt.SetText(displayString);
        }

    }
}
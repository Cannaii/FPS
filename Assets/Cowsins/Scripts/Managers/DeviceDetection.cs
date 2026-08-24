using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace cowsins
{
    public class DeviceDetection : MonoBehaviour
    {
        public enum InputMode
        {
            Keyboard,
            Controller
        }

        public InputMode mode { get; private set; }

        public static DeviceDetection Instance;

        private InputDevice lastDevice;

        public event Action<InputMode> OnInputModeChanged;

        private void Awake()
        {
            if (Instance == null) Instance = this;
        }

        private void OnEnable() => InputSystem.onEvent += OnInputEvent;

        private void OnDisable() => InputSystem.onEvent -= OnInputEvent;

        private void OnInputEvent(InputEventPtr eventPtr, InputDevice device)
        {
            if (device != lastDevice && (eventPtr.IsA<StateEvent>() || eventPtr.IsA<DeltaStateEvent>()))
            {
                lastDevice = device;
                InputMode newMode;

                if (IsKeyboard(device))
                    newMode = InputMode.Keyboard;
                else if (IsController(device))
                    newMode = InputMode.Controller;
                else
                    return;

                if (newMode != mode)
                {
                    mode = newMode;
                    OnInputModeChanged?.Invoke(mode);
                }
            }
        }

        private bool IsController(InputDevice device)
        {
            return device is Gamepad;
        }

        private bool IsKeyboard(InputDevice device)
        {
            return device is Keyboard || device is Mouse;
        }
    }
}

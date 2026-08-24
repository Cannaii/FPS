using UnityEngine;
using UnityEngine.InputSystem;
using System;
using TMPro;

namespace cowsins
{
    /// <summary>
    /// Manages player inputs and broadcasts them to other scripts.
    /// </summary>
    public class InputManager : MonoBehaviour
    {
        #region events
        public Action OnInventoryOpenPressed, OnInventoryFavOpenPressed;
        public Action OnDrop, OnInspect, OnMelee, OnShoot, OnStopShoot, OnTogglePause, OnToggleFlashlight,OnBackUI, OnAimPressed, OnAimReleased;
        public Action OnJump, OnDash, OnStartGrapple, OnStopGrapple, OnMoveInputChanged, OnSprintPressed, OnSprintReleased, OnCrouchPressed, OnCrouchReleased;

        public static event Action rebindComplete;
        public static event Action rebindCanceled;
        public static event Action<InputAction, int> rebindStarted;
        #endregion

        #region variables
        // Inputs
        [HideInInspector] public bool Jumping, Sprinting, Crouching, Dashing, Shooting, Reloading, Aiming, Melee, Inspecting, Interacting, StartInteraction, Dropping, Nextweapon, Previousweapon,
            Pausing, OpenInventory, OpenFavMenu, ToggleFlashlight, Grappling, BackUI, SelectUI, WestButtonUI, NorthButtonUI;
        [HideInInspector] public float X, Y, Scrolling, Mousex, Mousey, Controllerx, Controllery;


        public static PlayerActions inputActions;
        private static int enabledInstances = 0;

        private PlayerDependencies playerDependencies;

        private IWeaponBehaviourProvider weaponBehaviour;
        private IWeaponReferenceProvider weaponController;

        private Vector2 lastMoveInput;
        private bool lastSprintState, lastCrouchState, lastAimState;
        private bool alternateAiming, alternateSprint, alternateCrouch;

        private System.Action<InputAction.CallbackContext> sprintStarted, sprintCanceled;
        private System.Action<InputAction.CallbackContext> crouchStarted, crouchCanceled;
        private System.Action<InputAction.CallbackContext> aimStarted, aimCanceled;
        private System.Action<InputAction.CallbackContext> pauseHandler, inventoryOpenHandler, inventoryFavOpenHandler;
        private System.Action<InputAction.CallbackContext> dropHandler, jumpHandler, dashHandler, inspectHandler;
        private System.Action<InputAction.CallbackContext> meleeHandler, flashlightHandler, grappleStartHandler, grappleStopHandler;
        private System.Action<InputAction.CallbackContext> backUIHandler, firingStartHandler, firingStopHandler;

        #endregion

        private void Awake()
        {
            sprintStarted = ctx => HandleActionInput(ref Sprinting,ref lastSprintState,true,alternateSprint,OnSprintPressed,OnSprintReleased);
            sprintCanceled = ctx => HandleActionInput(ref Sprinting,ref lastSprintState,false,alternateSprint,OnSprintPressed,OnSprintReleased);
            crouchStarted = ctx => HandleActionInput(ref Crouching, ref lastCrouchState, true, alternateCrouch, OnCrouchPressed, OnCrouchReleased);
            crouchCanceled = ctx => HandleActionInput(ref Crouching, ref lastCrouchState, false, alternateCrouch, OnCrouchPressed, OnCrouchReleased);
            aimStarted = ctx => HandleActionInput(ref Aiming, ref lastAimState, true, alternateAiming, OnAimPressed, OnAimReleased);
            aimCanceled = ctx => HandleActionInput(ref Aiming, ref lastAimState, false, alternateAiming, OnAimPressed, OnAimReleased);
            pauseHandler = ctx => OnTogglePause?.Invoke();
            inventoryOpenHandler = ctx => OnInventoryOpenPressed?.Invoke();
            inventoryFavOpenHandler = ctx => OnInventoryFavOpenPressed?.Invoke();
            dropHandler = ctx => OnDrop?.Invoke();
            jumpHandler = ctx => OnJump?.Invoke();
            dashHandler = ctx => OnDash?.Invoke();
            inspectHandler = ctx => OnInspect?.Invoke();
            meleeHandler = ctx => OnMelee?.Invoke();
            flashlightHandler = ctx => OnToggleFlashlight?.Invoke();
            grappleStartHandler = ctx => OnStartGrapple?.Invoke();
            grappleStopHandler = ctx => OnStopGrapple?.Invoke();
            backUIHandler = ctx => OnBackUI?.Invoke();
            firingStartHandler = ctx => {
                Shooting = true;
                OnShoot?.Invoke();
            };
            firingStopHandler = ctx => {
                Shooting = false;
                OnStopShoot?.Invoke();
            };
        }

        private void OnEnable()
        {
            Init();

            // track enabled instances
            enabledInstances++;

            inputActions.GameControls.Crouching.started += crouchStarted;
            inputActions.GameControls.Crouching.canceled += crouchCanceled;
            inputActions.GameControls.Sprinting.started += sprintStarted;
            inputActions.GameControls.Sprinting.canceled += sprintCanceled;
            inputActions.GameControls.Aiming.started += aimStarted;
            inputActions.GameControls.Aiming.canceled += aimCanceled;


            inputActions.GameControls.Pause.started += pauseHandler;
            inputActions.GameControls.InventoryOpen.performed += inventoryOpenHandler;
            inputActions.GameControls.InventoryFavOpen.performed += inventoryFavOpenHandler;
            inputActions.GameControls.Drop.started += dropHandler;
            inputActions.GameControls.Jumping.started += jumpHandler;
            inputActions.GameControls.Dashing.started += dashHandler;
            inputActions.GameControls.Inspect.started += inspectHandler;
            inputActions.GameControls.Melee.started += meleeHandler;
            inputActions.GameControls.ToggleFlashLight.started += flashlightHandler;
            inputActions.GameControls.Grapple.started += grappleStartHandler;
            inputActions.GameControls.Grapple.canceled += grappleStopHandler;
            inputActions.UI.Back.started += backUIHandler;
            inputActions.GameControls.Firing.started += firingStartHandler;
            inputActions.GameControls.Firing.canceled += firingStopHandler;
        }

        private void OnDisable()
        {
            inputActions.GameControls.Sprinting.started -= sprintStarted;
            inputActions.GameControls.Sprinting.canceled -= sprintCanceled;
            inputActions.GameControls.Crouching.started -= crouchStarted;
            inputActions.GameControls.Crouching.canceled -= crouchCanceled;
            inputActions.GameControls.Aiming.started -= aimStarted;
            inputActions.GameControls.Aiming.canceled -= aimCanceled;

            inputActions.GameControls.Pause.started -= pauseHandler;
            inputActions.GameControls.InventoryOpen.performed -= inventoryOpenHandler;
            inputActions.GameControls.InventoryFavOpen.performed -= inventoryFavOpenHandler;
            inputActions.GameControls.Drop.started -= dropHandler;
            inputActions.GameControls.Jumping.started -= jumpHandler;
            inputActions.GameControls.Dashing.started -= dashHandler;
            inputActions.GameControls.Inspect.started -= inspectHandler;
            inputActions.GameControls.Melee.started -= meleeHandler;
            inputActions.GameControls.ToggleFlashLight.started -= flashlightHandler;
            inputActions.GameControls.Grapple.started -= grappleStartHandler;
            inputActions.GameControls.Grapple.canceled -= grappleStopHandler;
            inputActions.UI.Back.started -= backUIHandler;
            inputActions.GameControls.Firing.started -= firingStartHandler;
            inputActions.GameControls.Firing.canceled -= firingStopHandler;

            // Only disable the static inputActions when no InputManager instances remain enabled
            enabledInstances = Mathf.Max(0, enabledInstances - 1);
            if (enabledInstances == 0)
            {
                inputActions.Disable();
            }
        }
        private void Update()
        {
            if (playerDependencies == null) return;

            if (Mouse.current != null)
            {
                float dx = Mouse.current.delta.x.ReadValue();
                float dy = Mouse.current.delta.y.ReadValue();

                Mousex = float.IsNaN(dx) ? 0f : dx;
                Mousey = float.IsNaN(dy) ? 0f : dy;
            }

            if (Gamepad.current != null)
            {
                float dx = Gamepad.current.rightStick.x.ReadValue();
                float dy = -Gamepad.current.rightStick.y.ReadValue();

                Controllerx = float.IsNaN(dx) ? 0f : dx;
                Controllery = float.IsNaN(dy) ? 0f : dy;
            }

            Vector2 moveInput = inputActions.GameControls.Movement.ReadValue<Vector2>();
            if (moveInput != lastMoveInput)
            {
                lastMoveInput = moveInput;

                X = moveInput.x;
                Y = moveInput.y;
                OnMoveInputChanged?.Invoke();
            }

            Reloading = inputActions.GameControls.Reloading.IsPressed();
            Melee = inputActions.GameControls.Melee.WasPressedThisFrame();

            Scrolling = inputActions.GameControls.Scrolling.ReadValue<Vector2>().y;
            Nextweapon = inputActions.GameControls.ChangeWeapons.WasPressedThisFrame() && inputActions.GameControls.ChangeWeapons.ReadValue<float>() > 0;
            Previousweapon = inputActions.GameControls.ChangeWeapons.WasPressedThisFrame() && inputActions.GameControls.ChangeWeapons.ReadValue<float>() < 0;

            Interacting = inputActions.GameControls.Interacting.IsPressed();
            StartInteraction = inputActions.GameControls.Interacting.WasPressedThisFrame();
            OpenInventory = inputActions.GameControls.InventoryOpen.WasPressedThisFrame();
            OpenFavMenu = inputActions.GameControls.InventoryFavOpen.WasPressedThisFrame();
            Dropping = inputActions.GameControls.Drop.WasPressedThisFrame();

            Inspecting = inputActions.GameControls.Inspect.IsPressed();

            ToggleFlashlight = inputActions.GameControls.ToggleFlashLight.WasPressedThisFrame();

            Grappling = inputActions.GameControls.Grapple.IsPressed();

            Dashing = inputActions.GameControls.Dashing.WasPressedThisFrame();
            Jumping = inputActions.GameControls.Jumping.WasPressedThisFrame();

            BackUI = inputActions.UI.Back.WasPressedThisFrame();
            SelectUI = inputActions.UI.Select.WasPressedThisFrame();
            WestButtonUI = inputActions.UI.WestButton.WasPressedThisFrame();
            NorthButtonUI = inputActions.UI.NorthButton.WasPressedThisFrame();
            Pausing = inputActions.GameControls.Pause.WasPressedThisFrame();
        }

        private void FixedUpdate()
        {
            Y = inputActions.GameControls.Movement.ReadValue<Vector2>().y;
        }

        public void ForceUncrouch()
        {
            if (Crouching)
            {
                Crouching = false;
                lastCrouchState = false;
                OnCrouchReleased?.Invoke();
            }
        }

        #region others

        private void HandleActionInput(ref bool currentState, ref bool lastState, bool pressed, bool useToggleMode, Action onPressed, Action onReleased)
        {
            bool newState = currentState;

            if (useToggleMode)
            {
                if (pressed)
                    newState = !currentState;
            }
            else
            {
                newState = pressed;
            }

            if (newState != lastState)
            {
                currentState = newState;
                lastState = newState;

                if (newState)
                    onPressed?.Invoke();
                else
                    onReleased?.Invoke();
            }
        }

        public void ToggleGameControls(bool enable)
        {
            if (enable) inputActions.GameControls.Enable();
            else inputActions.GameControls.Disable();
        }

        public void ToggleUIControls(bool enable)
        {
            if (enable) inputActions.UI.Enable();
            else inputActions.UI.Disable();
        }

        public float GatherRawMouseX(float currentSensX, float currentControllerSensX)
        {
            return (Mousex * currentSensX * Time.fixedDeltaTime + Controllerx * Time.deltaTime * currentControllerSensX); 
        }
        public float GatherRawMouseY(int sensYInverted, int sensYInvertedController, float currentSensY, float currentControllerSensY)
        {
            return (Mousey * currentSensY * sensYInverted * Time.fixedDeltaTime + Controllery * sensYInvertedController * Time.deltaTime * currentControllerSensY);
        }
        private void Init()
        {
            // Initialize Inputs
            if (inputActions == null) inputActions = new PlayerActions();
            inputActions.Enable();
            
            // Load saved bindings overrides
            LoadAllBindings();

            ToggleGameControls(true);
            ToggleUIControls(false);
        }

        private void LoadAllBindings()
        {
            // Iterate through all the acction maps in the Player Actions
            foreach (var actionMap in inputActions.asset.actionMaps)
            {
                foreach (var action in actionMap.actions)
                {
                    // For each of the bindings of each of the actions, load the binding binding from PlayerPrefs
                    for (int i = 0; i < action.bindings.Count; i++)
                    {
                        LoadBindingOverride(action, i);
                    }
                }
            }
        }

        private static void LoadBindingOverride(InputAction action, int bindingIndex)
        {

            // Gather the path from the Player Prefs
            string overridePath = PlayerPrefs.GetString(action.actionMap + action.name + bindingIndex);
            // If the path is valid, apply it to the action that needs to be loaded.
            if (!string.IsNullOrEmpty(overridePath))
            {
                action.ApplyBindingOverride(bindingIndex, overridePath);
            }
        }

        /// <summary>
        /// Sets the player that the InputManager will take as a reference
        /// </summary>
        /// <param name="player"></param>
        public void SetPlayer(PlayerDependencies player)
        {
            this.playerDependencies = player;

            weaponBehaviour = player.WeaponBehaviour;
            weaponController = player.WeaponReference;
        }
        
        public void SetPlayerInputModes(PlayerMovementSettings playerSettings)
        {
            this.alternateSprint = playerSettings.alternateSprint; 
            this.alternateCrouch = playerSettings.alternateCrouch;
        }
        public void SetWeaponInputModes(WeaponControllerSettings weaponControllerSettings)
        {
            this.alternateAiming = weaponControllerSettings.alternateAiming;
        }

        public static void StartRebind(string actionName, int bindingIndex, TextMeshProUGUI statusTxt, bool excludeMouse, GameObject rebindOverlay, TextMeshProUGUI rebindOverlayTitle)
        {
            // Find the Input Action based on its name
            InputAction action = inputActions.asset.FindAction(actionName);

            if (action == null || action.bindings.Count <= bindingIndex)
            {
                Debug.LogError("Action or Binding not Found");
                return;
            }

            // If it is valid check if it is a composite
            // Iterate through each each composite part and rebind it
            if (action.bindings[bindingIndex].isComposite)
            {
                var firstPartIndex = bindingIndex + 1;

                if (firstPartIndex < action.bindings.Count && action.bindings[firstPartIndex].isComposite) PerformRebind(action, bindingIndex, statusTxt, true, excludeMouse, rebindOverlay, rebindOverlayTitle);
            }
            else PerformRebind(action, bindingIndex, statusTxt, false, excludeMouse, rebindOverlay, rebindOverlayTitle);
        }
        private static void PerformRebind(InputAction actionToRebind, int bindingIndex, TextMeshProUGUI statusTxt, bool allCompositeParts, bool excludeMouse, GameObject rebindOverlay, TextMeshProUGUI rebindOverlayTitle)
        {
            if (actionToRebind == null || bindingIndex < 0)
                return;

            // Update the text status
            statusTxt.text = $"Press a {actionToRebind.expectedControlType}";
            rebindOverlay.SetActive(true);
            rebindOverlayTitle.text = $"Rebinding {actionToRebind.name}";
            actionToRebind.Disable();

            var rebind = actionToRebind.PerformInteractiveRebinding(bindingIndex);

            // Handle rebind completion
            rebind.OnComplete(operation =>
            {
                rebindOverlay.SetActive(false);
                // Enable the rebind and stop the operation
                actionToRebind.Enable();
                operation.Dispose();

                // Rebind for Composite
                if (allCompositeParts)
                {
                    var nextBindingIndex = bindingIndex + 1;
                    if (nextBindingIndex < actionToRebind.bindings.Count && actionToRebind.bindings[nextBindingIndex].isComposite) PerformRebind(actionToRebind, nextBindingIndex, statusTxt, allCompositeParts, excludeMouse, rebindOverlay, rebindOverlayTitle);
                }

                // Save the new rebinds
                SaveBindingOverride(actionToRebind);

                rebindComplete?.Invoke();
            });

            // Handle rebind cancel
            rebind.OnCancel(operation =>
            {
                rebindOverlay.SetActive(false);
                actionToRebind.Enable();
                operation.Dispose();

                rebindCanceled?.Invoke();
            });

            // Cancel rebind if pressing escape
            rebind.WithCancelingThrough("<Keyboard>/escape");

            // Exclude mouse
            if (excludeMouse)
                rebind.WithControlsExcluding("<Mouse>/escape");

            rebindStarted?.Invoke(actionToRebind, bindingIndex);
            // Actually start the rebind process
            rebind.Start();
        }


        /// <summary>
        /// Retrieve the name of a binding.
        /// </summary>
        /// <param name="actionName"></param>
        /// <param name="bindingIndex"></param>
        /// <returns></returns>
        public static string GetBindingName(string actionName, int bindingIndex)
        {
            if (inputActions == null) inputActions = new PlayerActions();

            InputAction action = inputActions.asset.FindAction(actionName);
            return action.GetBindingDisplayString(bindingIndex);
        }

        // Save the bindings into player prefs for each action
        private static void SaveBindingOverride(InputAction action)
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                PlayerPrefs.SetString(action.actionMap + action.name + i, action.bindings[i].overridePath);
            }
        }

        public static void LoadBindingOverride(string actionName)
        {
            if (inputActions == null)
                inputActions = new PlayerActions();
            // Gather the Input Action given its name
            InputAction action = inputActions.asset.FindAction(actionName);

            // For each binding apply the binding from PlayerPrefs
            for (int i = 0; i < action.bindings.Count; i++)
            {
                if (!string.IsNullOrEmpty(PlayerPrefs.GetString(action.actionMap + action.name + i)))
                    action.ApplyBindingOverride(i, PlayerPrefs.GetString(action.actionMap + action.name + i));
            }
        }

        /// <summary>
        /// Reset the bindings for the given action
        /// </summary>
        /// <param name="actionName"></param>
        /// <param name="bindingIndex"></param>
        public static void ResetBinding(string actionName, int bindingIndex)
        {
            // Gather the Input Action given its name
            InputAction action = inputActions.asset.FindAction(actionName);

            if (action == null || action.bindings.Count <= bindingIndex)
            {
                Debug.LogError("Action or Binding not found");
                return;
            }
            if (action.bindings[bindingIndex].isComposite)
            {
                for (int i = bindingIndex; i < action.bindings.Count && action.bindings[i].isComposite; i++)
                    action.RemoveBindingOverride(i);
            }
            else
                action.RemoveBindingOverride(bindingIndex);

            SaveBindingOverride(action);
        }

        public static void ResetAllBindings()
        {
            if (inputActions == null) return;
            foreach (var actionMap in inputActions.asset.actionMaps)
            {
                foreach (var action in actionMap.actions)
                {
                    action.RemoveAllBindingOverrides();
                    SaveBindingOverride(action);
                }
            }
            rebindComplete?.Invoke();
        }
        #endregion
    }

}

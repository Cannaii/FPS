/// <summary>
/// This script belongs to cowsins™ as a part of the cowsins´ FPS Engine. All rights reserved. 
/// </summary>
using cowsins;
using UnityEngine;
using System.Collections;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace cowsins
{
    public class WeaponSpecificEffects : MonoBehaviour
    {
        #region WeaponSway
        #region shared
        [System.Serializable]
        public enum SwayMethod
        {
            Simple, PivotBased
        }
        public SwayMethod swayMethod;
        public delegate void Sway();

        public Sway sway;
        #endregion
        #region simple
        [Header("Position")]
        [SerializeField] private float amount = 0.02f;

        [SerializeField] private float maxAmount = 0.06f;

        [SerializeField] private float smoothAmount = 6f;


        [Header("Tilting")]
        [SerializeField] private float tiltAmount = 4f;

        [SerializeField] private float maxTiltAmount = 5f;

        [SerializeField] private float smoothTiltAmount = 12f;

        private Vector3 initialPosition;

        private Quaternion initialRotation;

        private float InputX;

        private float InputY;

        private float playerMultiplier;
        #endregion
        #region pivotBased
        [SerializeField] private Transform pivot;

        [SerializeField] private float swaySpeed;

        [SerializeField] private Vector2 swayMovementAmount;

        [SerializeField] private Vector2 swayRotationAmount;

        [SerializeField] private float swayTiltAmount;
        #endregion
        #endregion

        #region CrouchTilt
        [SerializeField] private Vector3 tiltRot, tiltPosOffset;
        [SerializeField] private float tiltSpeed = 8f;

        private Vector3 originalLocalPos;
        private Quaternion originalLocalRot;
        private Coroutine tiltCoroutine;

        #endregion

        private PlayerDependencies playerDependencies;
        private IPlayerMovementStateProvider playerMovement; // IPlayerMovementStateProvider is implemented in PlayerMovement.cs
        private IWeaponBehaviourProvider weaponController; // IWeaponBehaviourProvider is implemented in WeaponController.cs
        private IPlayerControlProvider playerControl; // IPlayerControlProvider is implemented in PlayerControl.cs
        private InputManager inputManager;
        private void Start()
        {
            // Sway Set-up
            if (swayMethod == SwayMethod.Simple)
            {
                initialPosition = transform.localPosition;
                initialRotation = transform.localRotation;
                sway = SimpleSway;
            }
            else
            {
                sway = PivotSway;
            }

            // Crouch Tilt Set-up
            originalLocalRot  = transform.localRotation;
            originalLocalPos = transform.localPosition;
        }

        public void Initialize(PlayerDependencies playerDependencies)
        {
            playerMovement = playerDependencies.PlayerMovementState;
            weaponController = playerDependencies.WeaponBehaviour;
            playerControl = playerDependencies.PlayerControl;
            inputManager = playerDependencies.InputManager;

            playerDependencies.PlayerMovementEvents.Events.OnCrouchStart.AddListener(HandleCrouch);
            playerDependencies.PlayerMovementEvents.Events.OnCrouchStop.AddListener(HandleUnCrouch);
            playerDependencies.WeaponEvents.Events.OnUnholster.AddListener(HandleCrouchOnUnholster);
            playerDependencies.WeaponEvents.Events.OnAimStart.AddListener(OnAimStart);
            playerDependencies.WeaponEvents.Events.OnAimStop.AddListener(OnAimStop);
        }

        private void Update()
        {
            if (!playerControl.IsControllable) return;
            sway?.Invoke();
        }
        #region Weapon Sway Methods

        private void SimpleSway()
        {
            CalculateSway();
            MoveSway();
            TiltSway();
        }
        private void CalculateSway()
        {
            InputX = -inputManager.Mousex / 10 - 2 * inputManager.Controllerx;
            InputY = -inputManager.Mousey / 10 - 2 * inputManager.Controllery;

            if (weaponController.IsAiming) playerMultiplier = 5f;
            else playerMultiplier = 1f;
        }

        private void MoveSway()
        {
            float moveX = Mathf.Clamp(InputX * amount, -maxAmount, maxAmount) / playerMultiplier;
            float moveY = Mathf.Clamp(InputY * amount, -1, 1) / playerMultiplier;

            Vector3 finalPosition = new Vector3(moveX, moveY, 0);

            transform.localPosition = Vector3.Lerp(transform.localPosition, finalPosition + initialPosition, Time.fixedDeltaTime * smoothAmount * playerMultiplier);
        }

        private void TiltSway()
        {
            float moveX = Mathf.Clamp(InputX * tiltAmount, -maxTiltAmount, maxTiltAmount) / playerMultiplier;

            Quaternion finalRotation = Quaternion.Euler(0, 0, moveX);

            transform.localRotation = Quaternion.Lerp(transform.localRotation, finalRotation * initialRotation, Time.fixedDeltaTime * smoothTiltAmount * playerMultiplier);
        }

        private void PivotSway()
        {
            HandleSwayLocation();
            HandleSwayRotation();
        }
        private void HandleSwayRotation()
        {
            var right = Camera.main.transform.right;
            right.y = 0f;
            right.Normalize();

            // HANDLE HORIZONTAL ROTATION
            transform.RotateAround(pivot.position, new Vector3(0, 1, 0), Time.fixedDeltaTime * swayRotationAmount.x * -inputManager.Mousex);
            // HANDLE VERTICAL ROTATION
            transform.RotateAround(pivot.position, right, Time.fixedDeltaTime * swayRotationAmount.y * inputManager.Mousey);
            // HANDLE TILT ROTATION
            Quaternion swayRot = Quaternion.Lerp(transform.localRotation,
                Quaternion.Euler(new Vector3(transform.localRotation.eulerAngles.x, transform.localRotation.eulerAngles.y, inputManager.Mousex * swayTiltAmount)),
                Time.deltaTime * swaySpeed);

            swayRot = Quaternion.Lerp(transform.localRotation, Quaternion.Euler(Vector3.zero), Time.deltaTime * swaySpeed);

            transform.localRotation = swayRot;
        }

        private void HandleSwayLocation()
        {
            Vector3 finalPosition = new Vector3(-inputManager.Mousex, inputManager.Mousey, 0) / 100;
            finalPosition.x = Mathf.Clamp(finalPosition.x, -1, 1) * swayMovementAmount.x;
            finalPosition.y = Mathf.Clamp(finalPosition.y, -1, 1) * swayMovementAmount.y;

            transform.localPosition = Vector3.Lerp(transform.localPosition, finalPosition, swaySpeed * Time.deltaTime);
        }

        #endregion

        #region Crouch Tilt Methods

        private void HandleCrouchOnUnholster(bool prop, bool prop2)
        {
            if (playerMovement.IsCrouching) HandleCrouch();
        }
        private void HandleCrouch()
        {
            if (!weaponController.IsAiming)
                StartCrouchTilt(tiltRot, originalLocalPos + tiltPosOffset);
        }

        private void HandleUnCrouch()
        {
            StartCrouchTilt(originalLocalRot.eulerAngles, originalLocalPos);
        }

        private void OnAimStart(float fov)
        {
            if (playerMovement.IsCrouching)
                StartCrouchTilt(originalLocalRot.eulerAngles, originalLocalPos);
        }

        private void OnAimStop()
        {
            if (playerMovement.IsCrouching)
                StartCrouchTilt(tiltRot, originalLocalPos + tiltPosOffset);
        }

        private void StartCrouchTilt(Vector3 targetRot, Vector3 targetPos)
        {
            if (!gameObject.activeInHierarchy) return;

            if (tiltCoroutine != null) StopCoroutine(tiltCoroutine);
            tiltCoroutine = StartCoroutine(TiltRoutine(targetRot, targetPos));
        }

        private IEnumerator TiltRoutine(Vector3 targetRotation, Vector3 targetPosition)
        {
            Quaternion targetQuat = Quaternion.Euler(targetRotation);

            while (Quaternion.Angle(transform.localRotation, targetQuat) > 0.1f ||
                   Vector3.Distance(transform.localPosition, targetPosition) > 0.01f)
            {
                transform.localRotation = Quaternion.Lerp(transform.localRotation, targetQuat, Time.deltaTime * tiltSpeed);
                transform.localPosition = Vector3.Lerp(transform.localPosition, targetPosition, Time.deltaTime * tiltSpeed);
                yield return null;
            }
        }


        #endregion

    }
#if UNITY_EDITOR
    [CustomEditor(typeof(WeaponSpecificEffects))]
    public class WeaponSpecificEffectsEditor : Editor
    {
        override public void OnInspectorGUI()
        {
            serializedObject.Update();
            var myScript = target as WeaponSpecificEffects;

            EditorGUILayout.LabelField("WEAPON SWAY", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("swayMethod"));
            EditorGUILayout.Space(10f);

            if (myScript.swayMethod == WeaponSpecificEffects.SwayMethod.Simple)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("POSITION");
                EditorGUILayout.PropertyField(serializedObject.FindProperty("amount"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("maxAmount"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("smoothAmount"));
                EditorGUILayout.LabelField("ROTATION");
                EditorGUILayout.PropertyField(serializedObject.FindProperty("tiltAmount"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("maxTiltAmount"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("smoothTiltAmount"));
                EditorGUI.indentLevel--;
            }
            else
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("pivot"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("swaySpeed"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("swayMovementAmount"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("swayRotationAmount"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("swayTiltAmount"));
                EditorGUI.indentLevel--;

            }
            EditorGUILayout.Space(15f);
            EditorGUILayout.LabelField("CROUCH TILT", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("tiltRot"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("tiltPosOffset"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("tiltSpeed"));
            EditorGUILayout.Space(5f);

            serializedObject.ApplyModifiedProperties();

        }
    }
#endif

}

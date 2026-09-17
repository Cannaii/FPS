using AFPS.Simulation.Characters;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AFPS.Input
{
    /// <summary>
    /// 每个渲染帧读取键鼠和手柄，并在模拟 Tick 到来时生成一条稳定的玩家输入命令。
    /// 一次性按键会一直缓存到下一个 Tick，避免渲染帧率和模拟频率不同导致漏输入。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class LocalPlayerInputCollector : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float mouseSensitivity = 0.08f;
        [SerializeField, Min(0f)] private float gamepadLookSpeed = 180f;
        [SerializeField, Range(0f, 0.95f)] private float gamepadDeadZone = 0.15f;
        [SerializeField] private bool invertVerticalLook;
        [SerializeField] private bool lockCursor = true;
        [SerializeField, Range(1f, 89.9f)] private float maximumPitch = 89f;

        /// <summary>最近渲染帧采集到的本地水平移动输入，范围为 -1 到 1。</summary>
        private float moveX;

        /// <summary>最近渲染帧采集到的本地前后移动输入，范围为 -1 到 1。</summary>
        private float moveY;

        /// <summary>尚未被模拟 Tick 消费的跳跃按下事件。</summary>
        private bool jumpPressedSinceLastTick;

        /// <summary>本地玩家当前未量化的水平观察角，供相机立即显示。</summary>
        public float LookYaw { get; private set; }

        /// <summary>本地玩家当前未量化的垂直观察角，供相机立即显示。</summary>
        public float LookPitch { get; private set; }

        private void OnEnable()
        {
            ApplyCursorState();
        }

        private void OnDisable()
        {
            if (lockCursor && Cursor.lockState == CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void Update()
        {
            Vector2 keyboardMove = ReadKeyboardMove();
            Vector2 gamepadMove = Gamepad.current != null ? ApplyDeadZone(Gamepad.current.leftStick.ReadValue()) : Vector2.zero;
            Vector2 movement = gamepadMove.sqrMagnitude > keyboardMove.sqrMagnitude ? gamepadMove : keyboardMove;
            movement = Vector2.ClampMagnitude(movement, 1f);
            moveX = movement.x;
            moveY = movement.y;

            Vector2 lookDelta = Vector2.zero;
            if (Mouse.current != null)
            {
                lookDelta += Mouse.current.delta.ReadValue() * mouseSensitivity;
            }

            if (Gamepad.current != null)
            {
                lookDelta += ApplyDeadZone(Gamepad.current.rightStick.ReadValue()) * gamepadLookSpeed * Time.unscaledDeltaTime;
            }

            LookYaw = NormalizeYaw(LookYaw + lookDelta.x);
            float verticalDelta = invertVerticalLook ? lookDelta.y : -lookDelta.y;
            LookPitch = Mathf.Clamp(LookPitch + verticalDelta, -maximumPitch, maximumPitch);

            bool keyboardJump = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
            bool gamepadJump = Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame;
            jumpPressedSinceLastTick |= keyboardJump || gamepadJump;
        }

        /// <summary>
        /// 在连接建立或重生时设置相机的初始权威朝向，防止第一帧跳转。
        /// </summary>
        public void SetLookAngles(float yaw, float pitch)
        {
            LookYaw = NormalizeYaw(yaw);
            LookPitch = Mathf.Clamp(pitch, -maximumPitch, maximumPitch);
        }

        /// <summary>
        /// 为指定模拟 Tick 创建输入命令；跳跃事件被读取后立即清除。
        /// </summary>
        public PlayerInputCommand ConsumeCommand(uint tick)
        {
            PlayerInputCommand command = new PlayerInputCommand
            {
                Tick = tick,
                MoveX = moveX,
                MoveY = moveY,
                LookYaw = LookYaw,
                LookPitch = LookPitch,
                JumpPressed = jumpPressedSinceLastTick
            };

            jumpPressedSinceLastTick = false;
            return command;
        }

        private static Vector2 ReadKeyboardMove()
        {
            if (Keyboard.current == null)
            {
                return Vector2.zero;
            }

            float horizontal = (Keyboard.current.dKey.isPressed ? 1f : 0f) - (Keyboard.current.aKey.isPressed ? 1f : 0f);
            float vertical = (Keyboard.current.wKey.isPressed ? 1f : 0f) - (Keyboard.current.sKey.isPressed ? 1f : 0f);
            return new Vector2(horizontal, vertical);
        }

        private Vector2 ApplyDeadZone(Vector2 value)
        {
            float magnitude = value.magnitude;
            if (magnitude <= gamepadDeadZone)
            {
                return Vector2.zero;
            }

            float scaledMagnitude = Mathf.InverseLerp(gamepadDeadZone, 1f, magnitude);
            return value.normalized * scaledMagnitude;
        }

        private void ApplyCursorState()
        {
            if (!lockCursor)
            {
                return;
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private static float NormalizeYaw(float yaw)
        {
            yaw %= 360f;
            return yaw < 0f ? yaw + 360f : yaw;
        }
    }
}

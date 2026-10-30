using UnityEngine;

namespace AFPS.Presentation.Camera
{
    /// <summary>
    /// 显示本地第一人称观察方向。Yaw 和 Pitch 来自输入采集器的即时值，
    /// 不修改权威玩家位置，也不把镜头晃动反馈到角色模拟。
    /// </summary>
    public sealed class FirstPersonCameraRig : MonoBehaviour
    {
        [SerializeField] private Transform yawPivot;
        [SerializeField] private Transform pitchPivot;
        [SerializeField] private Transform followTarget;
        [SerializeField, Min(0f)] private float eyeHeight = 1.6f;
        [SerializeField, Min(0.001f)] private float nearClipPlane = 0.01f;
        [Tooltip("FOV与横移倾斜的指数响应时间，单位秒；约三倍此值到达95%。")]
        [SerializeField, Min(0.001f)] private float combatResponseSeconds = 0.06f;

        private UnityEngine.Camera targetCamera;
        private float baseFieldOfView;
        private Quaternion aimRotation = Quaternion.identity;
        private float stanceRatio = 1f;
        private float combatFieldOfView;
        private float combatRoll;
        private float appliedRoll;

        /// <summary>只消费模拟姿态；缩放站立眼高，不改变瞄准轴或反写模拟坐标。</summary>
        public void SetStance(float heightRatio)
        {
            stanceRatio = Mathf.Clamp(heightRatio, 0.01f, 1f);
        }

        /// <summary>deltaTime为非负渲染秒数，速度为观察方向右侧的米/秒；仅改变投影和绕观察轴倾斜。</summary>
        public void RenderCombat(float recoilWeight, float lateralSpeed, float deltaTime)
        {
            if (targetCamera == null || !IsFinite(deltaTime) || deltaTime < 0f)
            {
                return;
            }
            float response = IsFinite(combatResponseSeconds)
                ? Mathf.Max(0.001f, combatResponseSeconds) : 0.06f;
            // 指数响应按秒而非帧数计算；零delta保持当前值，长帧直接收敛但不会越过目标。
            float blend = 1f - Mathf.Exp(-deltaTime / response);
            float targetFov = IsFinite(recoilWeight) ? Mathf.Clamp01(recoilWeight) * 1.5f : 0f;
            float targetRoll = IsFinite(lateralSpeed) ? Mathf.Clamp(-lateralSpeed * 0.35f, -2f, 2f) : 0f;
            combatFieldOfView = Smooth(combatFieldOfView, targetFov, blend);
            combatRoll = Smooth(combatRoll, targetRoll, blend);
            targetCamera.fieldOfView = baseFieldOfView + combatFieldOfView;
            // 最大2度，只绕观察轴旋转，forward保持不变，不偏移准星射线。
            targetCamera.transform.rotation = aimRotation * Quaternion.Euler(0f, 0f, combatRoll);
            appliedRoll = combatRoll;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static float Smooth(float current, float target, float blend)
        {
            if (blend == 0f)
            {
                return current;
            }
            float next = Mathf.Lerp(current, target, blend);
            // 单位度；收敛后归零，避免退出射击后留下永久的浮点尾差。
            return Mathf.Abs(next - target) < 0.0001f ? target : next;
        }

        private void RemoveCombatRoll()
        {
            if (targetCamera != null && appliedRoll != 0f)
            {
                // 相机可能是Pitch节点的子物体；先去掉上帧局部roll，不能把它采集成下一帧瞄准基准。
                targetCamera.transform.rotation *= Quaternion.Euler(0f, 0f, -appliedRoll);
            }
            appliedRoll = 0f;
        }

        /// <summary>连接世代变化或停用时立即清理余量；不等待平滑尾部，不改变当前瞄准方向。</summary>
        public void ResetCombat()
        {
            RemoveCombatRoll();
            combatFieldOfView = 0f;
            combatRoll = 0f;
            if (targetCamera != null)
            {
                targetCamera.fieldOfView = baseFieldOfView;
                aimRotation = targetCamera.transform.rotation;
            }
        }

        private void Awake()
        {
            if (yawPivot == null)
            {
                yawPivot = transform;
            }

            if (pitchPivot == null)
            {
                pitchPivot = yawPivot;
            }

            BindCamera(GetComponent<UnityEngine.Camera>());
        }

        /// <summary>在渲染帧应用本地即时朝向，减少低 Tick 下的镜头输入延迟。</summary>
        public void Render(float yaw, float pitch)
        {
            RemoveCombatRoll();
            if (followTarget != null)
            {
                yawPivot.position = followTarget.position + Vector3.up * (eyeHeight * stanceRatio);
            }

            yawPivot.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (pitchPivot != yawPivot)
            {
                pitchPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
            else
            {
                yawPivot.rotation = Quaternion.Euler(pitch, yaw, 0f);
            }
            aimRotation = targetCamera != null ? targetCamera.transform.rotation : pitchPivot.rotation;
        }

        /// <summary>供 Bootstrap 在场景未手动接线时创建一个可工作的默认第一人称相机。</summary>
        public void Configure(Transform target, Transform cameraTransform)
        {
            followTarget = target;
            yawPivot = cameraTransform != null ? cameraTransform : transform;
            pitchPivot = yawPivot;
            BindCamera(yawPivot.GetComponent<UnityEngine.Camera>());
        }

        private void BindCamera(UnityEngine.Camera camera)
        {
            ResetCombat();
            if (targetCamera != camera)
            {
                targetCamera = camera;
                if (targetCamera != null)
                {
                    // 重复Configure不能把尚未恢复的战斗FOV当成新的基础值。
                    baseFieldOfView = targetCamera.fieldOfView;
                }
            }
            if (targetCamera != null)
            {
                targetCamera.nearClipPlane = Mathf.Max(0.001f, nearClipPlane);
                aimRotation = targetCamera.transform.rotation;
            }
        }

        private void OnDisable()
        {
            ResetCombat();
        }
    }
}

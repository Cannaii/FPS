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

        private UnityEngine.Camera targetCamera;
        private float baseFieldOfView;
        private Quaternion aimRotation = Quaternion.identity;

        /// <summary>仅改变投影 FOV（角度），不旋转瞄准轴或回写玩家输入。</summary>
        public void RenderCombat(float recoilWeight, float lateralSpeed = 0f)
        {
            if (targetCamera != null)
            {
                targetCamera.fieldOfView = baseFieldOfView + Mathf.Clamp01(recoilWeight) * 1.5f;
                // 横移速度单位米/秒；绕观察轴轻倾最多 2°，forward 保持不变，不偏移准星射线。
                targetCamera.transform.rotation = aimRotation
                    * Quaternion.Euler(0f, 0f, Mathf.Clamp(-lateralSpeed * 0.35f, -2f, 2f));
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

            targetCamera = GetComponent<UnityEngine.Camera>();
            ApplyCameraSettings();
        }

        /// <summary>在渲染帧应用本地即时朝向，减少低 Tick 下的镜头输入延迟。</summary>
        public void Render(float yaw, float pitch)
        {
            if (followTarget != null)
            {
                yawPivot.position = followTarget.position + Vector3.up * eyeHeight;
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
            targetCamera = yawPivot.GetComponent<UnityEngine.Camera>();
            ApplyCameraSettings();
        }

        private void ApplyCameraSettings()
        {
            if (targetCamera != null)
            {
                baseFieldOfView = targetCamera.fieldOfView;
                targetCamera.nearClipPlane = Mathf.Max(0.001f, nearClipPlane);
            }
        }
    }
}

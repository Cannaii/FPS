using UnityEngine;

namespace AFPS.Presentation.Characters
{
    /// <summary>
    /// 保存模拟校正造成的视觉位置偏移，并按半衰期逐渐将偏移衰减到零。
    /// 该偏移只参与渲染，不会写回玩家模拟状态。
    /// </summary>
    public sealed class VisualCorrectionSmoother
    {
        /// <summary>
        /// 当前仍需要在画面中保留的世界空间位置偏移。
        /// </summary>
        public Vector3 Offset { get; private set; }

        /// <summary>
        /// 捕获校正前的显示位置与校正后目标位置之间的差值。
        /// 超过最大平滑距离时直接清除偏移，避免画面缓慢穿越很远距离。
        /// </summary>
        public void Capture(Vector3 currentVisualPosition, Vector3 correctedTargetPosition, float maxSmoothDistance)
        {
            Vector3 newOffset = currentVisualPosition - correctedTargetPosition;
            Offset = newOffset.sqrMagnitude <= maxSmoothDistance * maxSmoothDistance ? newOffset : Vector3.zero;
        }

        /// <summary>
        /// 根据渲染帧时长衰减视觉偏移，并返回衰减后的值。
        /// 每经过一个半衰期，剩余偏移会减少到原来的一半。
        /// </summary>
        public Vector3 Update(float renderDeltaTime, float halfLife)
        {
            if (renderDeltaTime <= 0f)
            {
                return Offset;
            }

            if (halfLife <= 0f)
            {
                Offset = Vector3.zero;
                return Offset;
            }

            float remainingRatio = Mathf.Pow(0.5f, renderDeltaTime / halfLife);
            Offset *= remainingRatio;

            if (Offset.sqrMagnitude < 0.00000001f)
            {
                Offset = Vector3.zero;
            }

            return Offset;
        }

        /// <summary>
        /// 立即清除所有视觉校正偏移。
        /// </summary>
        public void Clear()
        {
            Offset = Vector3.zero;
        }
    }
}

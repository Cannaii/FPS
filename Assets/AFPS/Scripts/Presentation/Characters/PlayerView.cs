using AFPS.Simulation.Characters;
using UnityEngine;

namespace AFPS.Presentation.Characters
{
    /// <summary>
    /// 将玩家的模拟状态显示到 Unity 场景中。
    /// 该组件只负责画面表现，不负责计算移动结果。
    /// </summary>
    public sealed class PlayerView : MonoBehaviour
    {
        /// <summary>
        /// 表示玩家模拟位置的场景根节点。
        /// 该节点的位置对应 PlayerState.Position。
        /// </summary>
        [SerializeField]
        private Transform simulationTransform;

        /// <summary>
        /// 校正偏移衰减到一半所需的时间，单位为秒。
        /// 数值越小，画面越快靠近校正后的模拟位置。
        /// </summary>
        [SerializeField]
        [Min(0f)]
        private float correctionHalfLife = 0.08f;

        /// <summary>
        /// 允许使用平滑追赶的最大校正距离，单位为米。
        /// 超过该距离时直接跳到校正位置。
        /// </summary>
        [SerializeField]
        [Min(0f)]
        private float maxSmoothCorrectionDistance = 1f;

        /// <summary>
        /// 获取当前显示对象在世界坐标中的位置。
        /// 该值仅用于创建初始模拟状态。
        /// </summary>
        public Vector3 InitialPosition => simulationTransform.position;

        /// <summary>
        /// 最近一次完成的玩家模拟状态。
        /// 渲染时会根据该状态计算显示位置。
        /// </summary>
        private PlayerState latestState;

        /// <summary>
        /// 表示 PlayerView 是否已经收到过有效的模拟状态。
        /// </summary>
        private bool hasState;

        /// <summary>
        /// 管理只作用于画面的校正偏移。
        /// </summary>
        private readonly VisualCorrectionSmoother correctionSmoother = new VisualCorrectionSmoother();

        /// <summary>
        /// 在组件初始化时补全显示对象引用。
        /// </summary>
        private void Awake()
        {
            if (simulationTransform == null)
            {
                simulationTransform = transform;
            }
        }

        /// <summary>
        /// 保存最近完成的玩家模拟状态。
        /// 该方法不直接更新 Transform，画面位置由 Render 方法计算。
        /// </summary>
        /// <param name="state">最近完成的玩家模拟状态。</param>
        public void ApplyState(in PlayerState state)
        {
            latestState = state;
            hasState = true;
        }

        /// <summary>
        /// 接受校正后的模拟状态，同时保留校正前的画面位置并逐渐追赶新状态。
        /// </summary>
        public void ApplyCorrection(in PlayerState state, float tickAlpha, float tickDeltaTime)
        {
            Vector3 currentVisualPosition = simulationTransform.position;
            latestState = state;
            hasState = true;

            Vector3 correctedTargetPosition = CalculateExtrapolatedPosition(tickAlpha, tickDeltaTime);
            correctionSmoother.Capture(currentVisualPosition, correctedTargetPosition, maxSmoothCorrectionDistance);
        }

        /// <summary>
        /// 立即应用状态并清除所有视觉偏移，用于无法重放历史时的硬校正。
        /// </summary>
        public void SnapToState(in PlayerState state)
        {
            latestState = state;
            hasState = true;
            correctionSmoother.Clear();
            simulationTransform.position = state.Position;
        }

        /// <summary>
        /// 根据最近的模拟状态和当前 Tick 进度计算本地玩家的显示位置。
        /// 当前采用速度外推，使画面能够在两个固定 Tick 之间继续移动。
        /// 
        /// </summary>
        /// <param name="tickAlpha">
        /// 当前渲染时刻在 Tick 间隔中的进度，取值为 0 到 1。
        /// </param>
        /// <param name="tickDeltaTime">
        /// 单个模拟 Tick 的固定时长，单位为秒。
        /// </param>
        public void Render(float tickAlpha, float tickDeltaTime, float renderDeltaTime)
        {
            if (!hasState)
            {
                return;
            }

            Vector3 extrapolatedPosition = CalculateExtrapolatedPosition(tickAlpha, tickDeltaTime);
            Vector3 correctionOffset = correctionSmoother.Update(renderDeltaTime, correctionHalfLife);
            simulationTransform.position = extrapolatedPosition + correctionOffset;
        }

        private Vector3 CalculateExtrapolatedPosition(float tickAlpha, float tickDeltaTime)
        {
            float elapsedAfterTick = tickAlpha * tickDeltaTime;
            return latestState.Position + latestState.Velocity * elapsedAfterTick;
        }
    }
}

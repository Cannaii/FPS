using AFPS.Core.Tick;
using AFPS.Input;
using AFPS.Presentation.Characters;
using AFPS.Simulation.Characters;
using UnityEngine;
using AFPS.Core.Collections;

namespace AFPS.Bootstrap
{
    /// <summary>
    /// 组装移动实验所需的 Tick、输入、玩家模拟和画面显示模块。
    /// 该组件负责协调模块，但不直接实现移动算法。
    /// </summary>
    public sealed class MovementSandboxBootstrap : MonoBehaviour
    {
        /// <summary>
        /// 负责以固定频率产生模拟 Tick 的驱动器。
        /// </summary>
        [SerializeField]
        private SimulationTickRunner tickRunner;

        /// <summary>
        /// 负责采集本地键盘输入并生成玩家输入命令的组件。
        /// </summary>
        [SerializeField]
        private LocalPlayerInputCollector inputCollector;

        /// <summary>
        /// 负责将玩家模拟状态显示到场景中的组件。
        /// </summary>
        [SerializeField]
        private PlayerView playerView;

        /// <summary>
        /// 玩家在地面上的最大水平移动速度，单位为米每秒。
        /// </summary>
        [SerializeField]
        [Min(0f)]
        private float maxGroundSpeed = 6f;

        /// <summary>
        /// 玩家在地面上的水平加速度，单位为米每二次方秒。
        /// </summary>
        [SerializeField]
        [Min(0f)]
        private float groundAcceleration = 20f;

        /// <summary>
        /// 玩家受到的重力加速度大小，单位为米每二次方秒。
        /// 当前水平移动阶段暂时不会使用该值。
        /// </summary>
        [SerializeField]
        [Min(0f)]
        private float gravity = 20f;

        /// <summary>
        /// 玩家跳跃时获得的初始向上速度，单位为米每秒。
        /// 当前水平移动阶段暂时不会使用该值。
        /// </summary>
        [SerializeField]
        [Min(0f)]
        private float jumpSpeed = 8f;

        /// <summary>
        /// 当前客户端持有的最新玩家模拟状态。
        /// 每次执行 Tick 后都会替换为新的状态。
        /// </summary>
        private PlayerState currentState;

        /// <summary>
        /// 当前移动模拟使用的固定配置。
        /// 客户端和服务器以后必须使用一致的配置。
        /// </summary>
        private PlayerSimulationConfig simulationConfig;

        /// <summary>
        /// 表示移动实验是否已经完成初始化。
        /// </summary>
        private bool initialized;

        /// <summary>
        /// 客户端预测历史最多保存的 Tick 数量。
        /// 在 50 Tick/s 下，256 个 Tick 大约对应 5.12 秒历史。
        /// </summary>
        private const int PredictionBufferCapacity = 256;

        /// <summary>
        /// 按 Tick 保存本地玩家产生的历史输入命令。
        /// 收到服务器权威状态后，客户端会读取这些输入进行重放。
        /// </summary>
        private TickBuffer<PlayerInputCommand> inputHistory;

        /// <summary>
        /// 按 Tick 保存客户端完成每个 Tick 后得到的预测状态。
        /// 用于和服务器返回的同 Tick 权威状态进行比较。
        /// </summary>
        private TickBuffer<PlayerState> stateHistory;

        /// <summary>
        /// 组件启动时创建初始状态和运行时模拟配置。
        /// 使用 Start 可以确保 PlayerView 已经完成 Awake 初始化。
        /// </summary>
        private void Start()
        {
            if (tickRunner == null ||
                inputCollector == null ||
                playerView == null)
            {
                Debug.LogError(
                    "MovementSandboxBootstrap 缺少必要的组件引用。",
                    this);

                enabled = false;
                return;
            }

            simulationConfig = new PlayerSimulationConfig(
                maxGroundSpeed,
                groundAcceleration,
                gravity,
                jumpSpeed);

            currentState = new PlayerState
            {
                Tick = tickRunner.CurrentTick,
                Position = playerView.InitialPosition,
                Velocity = Vector3.zero,
                IsGrounded = true
            };

            playerView.ApplyState(currentState);
            initialized = true;
        }

        /// <summary>
        /// 组件启用时订阅固定 Tick 事件。
        /// </summary>
        private void OnEnable()
        {
            if (tickRunner != null)
            {
                tickRunner.TickOccurred += HandleTickOccurred;
            }
        }

        /// <summary>
        /// 组件停用或销毁时取消 Tick 事件订阅。
        /// </summary>
        private void OnDisable()
        {
            if (tickRunner != null)
            {
                tickRunner.TickOccurred -= HandleTickOccurred;
            }
        }

        /// <summary>
        /// 每个渲染帧结束时，根据当前 Tick 进度更新玩家显示位置。
        /// 该过程只影响画面，不修改玩家的模拟状态。
        /// </summary>
        private void LateUpdate()
        {
            if (!initialized)
            {
                return;
            }

            // 这是基础速度外推方案。
            // 当速度在 Tick 边界发生明显变化时，可能出现显示位置跳变。
            playerView.Render(
                tickRunner.TickAlpha,
                tickRunner.TickDeltaTime);
        }

        /// <summary>
        /// 每个固定 Tick 中采集一条输入命令，
        /// 执行玩家模拟并显示最新结果。
        /// </summary>
        /// <param name="tick">当前模拟 Tick 编号。</param>
        /// <param name="tickDeltaTime">单个 Tick 的固定时长，单位为秒。</param>
        private void HandleTickOccurred(
            uint tick,
            float tickDeltaTime)
        {
            if (!initialized)
            {
                return;
            }

            PlayerInputCommand command =
                inputCollector.ConsumeCommand(tick);

            currentState = PlayerSimulation.Simulate(
                currentState,
                command,
                simulationConfig,
                tickDeltaTime);

            playerView.ApplyState(currentState);
        }
    }
}
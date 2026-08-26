using AFPS.Core.Collections;
using AFPS.Core.Tick;
using AFPS.Input;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Prediction;
using AFPS.NetCode.Simulation;
using AFPS.Presentation.Characters;
using AFPS.Simulation.Characters;
using UnityEngine;

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
        /// 运行时模拟将该正数作为向下的加速度使用。
        /// </summary>
        [SerializeField]
        [Min(0f)]
        private float gravity = 20f;

        /// <summary>
        /// 玩家成功跳跃时获得的初始向上速度，单位为米每秒。
        /// </summary>
        [SerializeField]
        [Min(0f)]
        private float jumpSpeed = 8f;

        /// <summary>
        /// 客户端输入到达模拟服务器前等待的 Tick 数。
        /// </summary>
        [SerializeField]
        [Min(0)]
        private int simulatedInputDelayTicks = 3;

        /// <summary>
        /// 服务器权威状态返回客户端前等待的 Tick 数。
        /// </summary>
        [SerializeField]
        [Min(0)]
        private int simulatedStateDelayTicks = 3;

        /// <summary>
        /// 模拟服务器最大地面速度相对客户端配置的倍率。
        /// 保持 1 表示配置一致；调成其他值可故意制造预测误差。
        /// </summary>
        [SerializeField]
        [Min(0.01f)]
        private float simulatedServerSpeedScale = 1f;

        /// <summary>
        /// 允许忽略的位置误差阈值，单位为米。
        /// </summary>
        [SerializeField]
        [Min(0f)]
        private float positionErrorThreshold = 0.001f;

        /// <summary>
        /// 允许忽略的速度误差阈值，单位为米每秒。
        /// </summary>
        [SerializeField]
        [Min(0f)]
        private float velocityErrorThreshold = 0.001f;

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
        /// 在当前进程内维护独立权威状态和固定网络延迟的模拟服务器。
        /// </summary>
        private SimulatedAuthoritativeServer simulatedServer;

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
        /// 最近一次收到服务器确认的客户端输入 Tick，仅用于运行时观察。
        /// </summary>
        [SerializeField]
        private uint lastAcknowledgedInputTick;

        /// <summary>
        /// 最近一次同 Tick 比较得到的位置误差，单位为米。
        /// </summary>
        [SerializeField]
        private float latestPositionError;

        /// <summary>
        /// 最近一次同 Tick 比较得到的速度误差，单位为米每秒。
        /// </summary>
        [SerializeField]
        private float latestVelocityError;

        /// <summary>
        /// 最近一次同 Tick 比较是否发现落地状态不一致。
        /// </summary>
        [SerializeField]
        private bool latestGroundedMismatch;

        /// <summary>
        /// 因预测状态或输入历史缺失而无法完成重放的累计次数。
        /// </summary>
        [SerializeField]
        private int missingPredictionHistoryCount;

        /// <summary>
        /// 预测误差超过阈值并成功完成回滚重放的累计次数。
        /// </summary>
        [SerializeField]
        private int correctionCount;

        /// <summary>
        /// 历史缺失后直接采用服务器状态的累计次数。
        /// </summary>
        [SerializeField]
        private int hardCorrectionCount;

        /// <summary>
        /// 最近一次成功校正时重新模拟的 Tick 数量。
        /// </summary>
        [SerializeField]
        private int lastReplayedTickCount;

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

            inputHistory = new TickBuffer<PlayerInputCommand>(PredictionBufferCapacity);
            stateHistory = new TickBuffer<PlayerState>(PredictionBufferCapacity);

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

            PlayerSimulationConfig serverSimulationConfig = new PlayerSimulationConfig(
                maxGroundSpeed * simulatedServerSpeedScale,
                groundAcceleration,
                gravity,
                jumpSpeed);

            simulatedServer = new SimulatedAuthoritativeServer(
                currentState,
                serverSimulationConfig,
                tickRunner.TickDeltaTime,
                simulatedInputDelayTicks,
                simulatedStateDelayTicks);

            // 保存尚未处理任何输入时的模拟起点。
            stateHistory.Store(currentState.Tick, currentState);

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

            playerView.Render(
                tickRunner.TickAlpha,
                tickRunner.TickDeltaTime,
                Time.unscaledDeltaTime);
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

            PlayerInputCommand command = InputCommandBatchCodec.Canonicalize(inputCollector.ConsumeCommand(tick));

            // 保存当前 Tick 的输入，供服务器校正时重新模拟。
            inputHistory.Store(tick, command);
            simulatedServer.SendInput(tick, command);

            currentState = PlayerSimulation.Simulate(
                currentState,
                command,
                simulationConfig,
                tickDeltaTime);

            // 保存完成当前 Tick 后得到的本地预测状态。
            stateHistory.Store(tick, currentState);

            playerView.ApplyState(currentState);

            simulatedServer.Advance(tick);

            while (simulatedServer.TryReceiveState(tick, out AuthoritativePlayerState authoritativeState))
            {
                if (!ReconcileAuthoritativeState(authoritativeState))
                {
                    break;
                }
            }
        }

        /// <summary>
        /// 使用服务器权威状态校正客户端预测。
        /// 历史完整时回滚并重放；历史缺失时执行硬校正。
        /// </summary>
        /// <returns>是否可以继续处理同一 Tick 内到达的其他服务器状态。</returns>
        private bool ReconcileAuthoritativeState(in AuthoritativePlayerState authoritativeState)
        {
            lastAcknowledgedInputTick = authoritativeState.LastProcessedInputTick;

            ReconciliationResult result = ClientPredictionReconciler.Reconcile(
                authoritativeState,
                currentState.Tick,
                currentState,
                inputHistory,
                stateHistory,
                simulationConfig,
                tickRunner.TickDeltaTime,
                positionErrorThreshold,
                velocityErrorThreshold);

            latestPositionError = result.Error.Position;
            latestVelocityError = result.Error.Velocity;
            latestGroundedMismatch = result.Error.GroundedMismatch;
            lastReplayedTickCount = result.ReplayedTickCount;

            if (result.Status == ReconciliationStatus.NoCorrection)
            {
                return true;
            }

            if (result.Status == ReconciliationStatus.Corrected)
            {
                currentState = result.State;
                correctionCount++;
                playerView.ApplyCorrection(
                    currentState,
                    tickRunner.TickAlpha,
                    tickRunner.TickDeltaTime);
                return true;
            }

            missingPredictionHistoryCount++;
            hardCorrectionCount++;
            currentState = authoritativeState.State;
            inputHistory.Clear();
            stateHistory.Clear();
            stateHistory.Store(currentState.Tick, currentState);
            playerView.SnapToState(currentState);

            Debug.LogWarning(
                $"Tick {lastAcknowledgedInputTick} 的预测历史不完整，已执行硬校正并清空旧历史。",
                this);

            return false;
        }
    }
}

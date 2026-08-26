using System;
using AFPS.NetCode.Runtime;
using AFPS.NetCode.Transport;
using AFPS.NetCode.Transport.Simulation;
using AFPS.Transport.Unity;
using UnityEngine;

namespace AFPS.Bootstrap
{
    /// <summary>
    /// 从 Inspector 或命令行读取网络启动参数，并管理 Unity Transport 的生命周期。
    /// 该组件是传输事件的唯一轮询入口，并把事件同步分发给后续会话管理器。
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class UnityNetworkBootstrap : MonoBehaviour
    {
        /// <summary>
        /// 未被命令行覆盖时使用的进程启动模式。
        /// </summary>
        [SerializeField]
        private NetworkLaunchMode launchMode = NetworkLaunchMode.Host;

        /// <summary>
        /// Client 模式连接的服务器 IPv4 地址；Host 会自动改用 127.0.0.1。
        /// </summary>
        [SerializeField]
        private string serverAddress = "127.0.0.1";

        /// <summary>
        /// 服务器监听或客户端连接的 UDP 端口。
        /// </summary>
        [SerializeField]
        [Range(1, 65535)]
        private int port = 7777;

        /// <summary>
        /// Host 或 Dedicated Server 允许的最大客户端连接数。
        /// </summary>
        [SerializeField]
        [Min(1)]
        private int maxConnections = 32;

        /// <summary>
        /// 是否允许 -afpsMode 等进程参数覆盖 Inspector 配置。
        /// Dedicated Server 构建通常应保持开启。
        /// </summary>
        [SerializeField]
        private bool useCommandLineOverrides = true;

        /// <summary>
        /// Server 和 Client 各自复用的单包接收缓冲区容量，单位为字节。
        /// 当前协议包应保持在常规 MTU 内，默认 2048 字节足够容纳完整数据包。
        /// </summary>
        [SerializeField]
        [Min(256)]
        private int receiveBufferCapacity = 2048;

        /// <summary>
        /// 是否在不可靠发送通道外包裹网络劣化模拟器。
        /// </summary>
        [Header("Network Impairment Simulation")]
        [SerializeField]
        private bool enableNetworkImpairment;

        /// <summary>
        /// 模拟的单向固定延迟，单位为毫秒。
        /// </summary>
        [SerializeField]
        [Min(0f)]
        private float simulatedLatencyMilliseconds;

        /// <summary>
        /// 模拟延迟在正负范围内变化的最大抖动，单位为毫秒。
        /// </summary>
        [SerializeField]
        [Min(0f)]
        private float simulatedJitterMilliseconds;

        /// <summary>
        /// 模拟不可靠数据包被网络丢弃的百分比。
        /// </summary>
        [SerializeField]
        [Range(0f, 100f)]
        private float simulatedPacketLossPercent;

        /// <summary>
        /// 模拟相邻不可靠数据包发生到达顺序交换的百分比。
        /// </summary>
        [SerializeField]
        [Range(0f, 100f)]
        private float simulatedReorderPercent;

        /// <summary>
        /// 发生乱序时为前一数据包增加的额外延迟，单位为毫秒。
        /// </summary>
        [SerializeField]
        [Min(0f)]
        private float simulatedReorderExtraDelayMilliseconds = 20f;

        /// <summary>
        /// 网络劣化随机序列的基础种子；Host 两侧会从该值派生不同种子。
        /// </summary>
        [SerializeField]
        private uint networkSimulationSeed = 12345;

        /// <summary>
        /// 每个传输实例最多允许等待释放的模拟数据包数量。
        /// </summary>
        [SerializeField]
        [Min(1)]
        private int maxSimulatedQueuedPackets = 1024;

        private byte[] serverReceiveBuffer;
        private byte[] clientReceiveBuffer;

        /// <summary>
        /// 当前组件创建并拥有的网络运行时；启动失败或销毁后为 null。
        /// </summary>
        public GameNetworkRuntime Runtime { get; private set; }

        /// <summary>
        /// 当前成功启动的实际模式，供运行时 UI 和后续会话组装读取。
        /// </summary>
        public NetworkLaunchMode ActiveMode => Runtime?.Mode ?? NetworkLaunchMode.None;

        /// <summary>
        /// 每次轮询到连接、断开或数据事件时同步触发。
        /// Data 的 payload 引用内部复用缓冲区，只能在当前回调期间读取，不能长期保存。
        /// </summary>
        public event Action<NetworkTransportSide, GameTransportEvent, ArraySegment<byte>> TransportEventReceived;

        private void Awake()
        {
            TryStartNetwork();
        }

        private void Update()
        {
            if (Runtime == null)
            {
                return;
            }

            Runtime.Pump();
            DrainTransportEvents(Runtime.ServerTransport, NetworkTransportSide.Server, serverReceiveBuffer);
            DrainTransportEvents(Runtime.ClientTransport, NetworkTransportSide.Client, clientReceiveBuffer);
        }

        private void OnDestroy()
        {
            Runtime?.Dispose();
            Runtime = null;
        }

        /// <summary>
        /// 使用 Inspector 默认值和可选命令行覆盖启动当前进程的网络角色。
        /// </summary>
        public bool TryStartNetwork()
        {
            if (Runtime != null)
            {
                Debug.LogWarning("UnityNetworkBootstrap 已经创建网络运行时。", this);
                return false;
            }

            if (port <= 0 || port > ushort.MaxValue)
            {
                Debug.LogError("UDP 端口必须在 1 到 65535 之间。", this);
                enabled = false;
                return false;
            }

            if (receiveBufferCapacity < 256)
            {
                Debug.LogError("网络接收缓冲区容量不能小于 256 字节。", this);
                enabled = false;
                return false;
            }

            NetworkLaunchOptions options = new NetworkLaunchOptions(launchMode, serverAddress, (ushort)port, maxConnections);
            if (useCommandLineOverrides && !NetworkLaunchCommandLine.TryApply(Environment.GetCommandLineArgs(), options, out options, out string commandLineError))
            {
                Debug.LogError(commandLineError, this);
                enabled = false;
                return false;
            }

            if (!TryCreateImpairmentConfig(networkSimulationSeed, out NetworkImpairmentConfig baseImpairmentConfig, out string impairmentError))
            {
                Debug.LogError(impairmentError, this);
                enabled = false;
                return false;
            }

            uint transportSeedOffset = 0;
            GameNetworkRuntime runtime = new GameNetworkRuntime(() => CreateTransport(baseImpairmentConfig, transportSeedOffset++));
            if (!runtime.TryStart(options, out string startError))
            {
                runtime.Dispose();
                Debug.LogError(startError, this);
                enabled = false;
                return false;
            }

            Runtime = runtime;
            serverReceiveBuffer = runtime.ServerTransport != null ? new byte[receiveBufferCapacity] : null;
            clientReceiveBuffer = runtime.ClientTransport != null ? new byte[receiveBufferCapacity] : null;
            if (options.Mode == NetworkLaunchMode.DedicatedServer)
            {
                Application.runInBackground = true;
            }

            Debug.Log($"AFPS 网络已启动：模式={options.Mode}，地址={options.ServerAddress}，端口={options.Port}。", this);
            return true;
        }

        private void DrainTransportEvents(IGameTransport transport, NetworkTransportSide side, byte[] receiveBuffer)
        {
            if (transport == null || receiveBuffer == null)
            {
                return;
            }

            ArraySegment<byte> writableBuffer = new ArraySegment<byte>(receiveBuffer);
            while (transport.TryPollEvent(writableBuffer, out GameTransportEvent transportEvent))
            {
                if (transportEvent.Type == TransportEventType.ReceiveBufferTooSmall)
                {
                    Debug.LogError($"{side} 收到 {transportEvent.PayloadLength} 字节的数据包，超过 {receiveBuffer.Length} 字节接收缓冲区，数据包已丢弃。", this);
                    continue;
                }

                ArraySegment<byte> payload = transportEvent.Type == TransportEventType.Data ? new ArraySegment<byte>(receiveBuffer, 0, transportEvent.PayloadLength) : default;
                TransportEventReceived?.Invoke(side, transportEvent, payload);
            }
        }

        private IGameTransport CreateTransport(in NetworkImpairmentConfig baseConfig, uint seedOffset)
        {
            IGameTransport transport = new UnityGameTransport();
            if (!enableNetworkImpairment)
            {
                return transport;
            }

            NetworkImpairmentConfig instanceConfig = new NetworkImpairmentConfig(baseConfig.BaseLatencySeconds, baseConfig.JitterSeconds, baseConfig.PacketLossProbability, baseConfig.ReorderProbability, baseConfig.ReorderExtraDelaySeconds, unchecked(baseConfig.RandomSeed + seedOffset), baseConfig.MaxQueuedPackets);
            return new NetworkImpairmentTransport(transport, instanceConfig, () => Time.realtimeSinceStartupAsDouble);
        }

        private bool TryCreateImpairmentConfig(uint seed, out NetworkImpairmentConfig config, out string error)
        {
            if (!enableNetworkImpairment)
            {
                config = default;
                error = null;
                return true;
            }

            try
            {
                config = new NetworkImpairmentConfig(simulatedLatencyMilliseconds / 1000d, simulatedJitterMilliseconds / 1000d, simulatedPacketLossPercent / 100d, simulatedReorderPercent / 100d, simulatedReorderExtraDelayMilliseconds / 1000d, seed, maxSimulatedQueuedPackets);
                error = null;
                return true;
            }
            catch (ArgumentOutOfRangeException exception)
            {
                config = default;
                error = $"网络劣化模拟参数无效：{exception.Message}";
                return false;
            }
        }
    }
}

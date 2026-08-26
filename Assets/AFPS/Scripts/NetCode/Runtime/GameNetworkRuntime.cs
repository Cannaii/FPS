using System;
using AFPS.NetCode.Transport;

namespace AFPS.NetCode.Runtime
{
    /// <summary>
    /// 根据启动模式创建、启动和驱动客户端与服务器传输实例。
    /// 该类只管理网络生命周期，不消费连接或数据事件，也不创建玩家会话。
    /// </summary>
    public sealed class GameNetworkRuntime : IDisposable
    {
        private const string HostLoopbackAddress = "127.0.0.1";
        private readonly Func<IGameTransport> transportFactory;

        /// <summary>
        /// 当前成功启动的进程模式；未运行时为 None。
        /// </summary>
        public NetworkLaunchMode Mode { get; private set; }

        /// <summary>
        /// Host 和 Dedicated Server 使用的服务器传输；Client 模式下为 null。
        /// </summary>
        public IGameTransport ServerTransport { get; private set; }

        /// <summary>
        /// Client 和 Host 本地玩家使用的客户端传输；Dedicated Server 模式下为 null。
        /// </summary>
        public IGameTransport ClientTransport { get; private set; }

        /// <summary>
        /// 当前模式要求的所有传输实例是否都已经启动。
        /// </summary>
        public bool IsRunning
        {
            get
            {
                switch (Mode)
                {
                    case NetworkLaunchMode.Client:
                        return ClientTransport != null && ClientTransport.IsRunning;
                    case NetworkLaunchMode.Host:
                        return ServerTransport != null && ServerTransport.IsRunning && ClientTransport != null && ClientTransport.IsRunning;
                    case NetworkLaunchMode.DedicatedServer:
                        return ServerTransport != null && ServerTransport.IsRunning;
                    default:
                        return false;
                }
            }
        }

        public GameNetworkRuntime(Func<IGameTransport> transportFactory)
        {
            this.transportFactory = transportFactory ?? throw new ArgumentNullException(nameof(transportFactory));
        }

        /// <summary>
        /// 按配置启动传输。Host 始终先监听服务器，再创建一个连接回环地址的本地客户端。
        /// 任一步骤失败都会释放本次已经创建的全部网络资源。
        /// </summary>
        public bool TryStart(in NetworkLaunchOptions options, out string error)
        {
            if (IsRunning || ServerTransport != null || ClientTransport != null)
            {
                error = "网络运行时已经启动；需要切换模式时请先调用 Stop。";
                return false;
            }

            if (!ValidateOptions(options, out error))
            {
                return false;
            }

            try
            {
                switch (options.Mode)
                {
                    case NetworkLaunchMode.Client:
                        ClientTransport = CreateTransport();
                        if (!ClientTransport.TryStartClient(options.ServerAddress, options.Port, out error))
                        {
                            Stop();
                            return false;
                        }

                        break;
                    case NetworkLaunchMode.Host:
                        ServerTransport = CreateTransport();
                        if (!ServerTransport.TryStartServer(options.Port, options.MaxConnections, out error))
                        {
                            Stop();
                            return false;
                        }

                        ClientTransport = CreateTransport();
                        if (ReferenceEquals(ServerTransport, ClientTransport))
                        {
                            error = "Host 模式的传输工厂必须返回两个独立实例。";
                            Stop();
                            return false;
                        }

                        if (!ClientTransport.TryStartClient(HostLoopbackAddress, options.Port, out error))
                        {
                            Stop();
                            return false;
                        }

                        break;
                    case NetworkLaunchMode.DedicatedServer:
                        ServerTransport = CreateTransport();
                        if (!ServerTransport.TryStartServer(options.Port, options.MaxConnections, out error))
                        {
                            Stop();
                            return false;
                        }

                        break;
                }
            }
            catch (Exception exception)
            {
                error = $"启动网络传输时发生异常：{exception.Message}";
                Stop();
                return false;
            }

            Mode = options.Mode;
            error = null;
            return true;
        }

        /// <summary>
        /// 每个渲染帧推进当前模式拥有的传输。Host 固定先推进服务器，再推进本地客户端。
        /// </summary>
        public void Pump()
        {
            ServerTransport?.Pump();
            ClientTransport?.Pump();
        }

        /// <summary>
        /// 停止并释放当前模式拥有的全部传输实例，可在之后重新选择模式启动。
        /// </summary>
        public void Stop()
        {
            IGameTransport clientTransport = ClientTransport;
            IGameTransport serverTransport = ServerTransport;
            ClientTransport = null;
            ServerTransport = null;
            Mode = NetworkLaunchMode.None;

            clientTransport?.Dispose();
            if (serverTransport != null && !ReferenceEquals(serverTransport, clientTransport))
            {
                serverTransport.Dispose();
            }
        }

        public void Dispose() => Stop();

        private IGameTransport CreateTransport()
        {
            return transportFactory() ?? throw new InvalidOperationException("传输工厂返回了 null。");
        }

        private static bool ValidateOptions(in NetworkLaunchOptions options, out string error)
        {
            if (options.Mode == NetworkLaunchMode.None)
            {
                error = "必须选择 Client、Host 或 Dedicated Server 启动模式。";
                return false;
            }

            if (options.Port == 0)
            {
                error = "UDP 端口必须在 1 到 65535 之间。";
                return false;
            }

            if (options.Mode == NetworkLaunchMode.Client && string.IsNullOrWhiteSpace(options.ServerAddress))
            {
                error = "Client 模式必须提供服务器 IP 地址。";
                return false;
            }

            if (options.Mode != NetworkLaunchMode.Client && options.MaxConnections <= 0)
            {
                error = "Host 或 Dedicated Server 的最大连接数必须大于零。";
                return false;
            }

            error = null;
            return true;
        }
    }
}

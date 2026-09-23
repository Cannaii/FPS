namespace AFPS.NetCode.Runtime
{
    /// <summary>
    /// 描述当前进程启动网络所需的只读参数。
    /// </summary>
    public readonly struct NetworkLaunchOptions
    {
        /// <summary>
        /// 当前进程需要启动为 Client、Host 还是 Dedicated Server。
        /// </summary>
        public readonly NetworkLaunchMode Mode;

        /// <summary>
        /// Client 模式需要连接的服务器 IPv4 地址；Host 固定使用回环地址。
        /// </summary>
        public readonly string ServerAddress;

        /// <summary>
        /// 服务器监听或客户端连接的 UDP 端口。
        /// </summary>
        public readonly ushort Port;

        /// <summary>
        /// Host 或 Dedicated Server 允许同时存在的最大客户端连接数量。
        /// </summary>
        public readonly int MaxConnections;

        public NetworkLaunchOptions(NetworkLaunchMode mode, string serverAddress, ushort port, int maxConnections)
        {
            Mode = mode;
            ServerAddress = serverAddress;
            Port = port;
            MaxConnections = maxConnections;
        }
    }
}

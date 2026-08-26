namespace AFPS.NetCode.Runtime
{
    /// <summary>
    /// 当前进程在一局网络游戏中承担的启动模式。
    /// Host 由独立服务器传输实例和本地客户端传输实例共同组成。
    /// </summary>
    public enum NetworkLaunchMode : byte
    {
        /// <summary>
        /// 尚未选择或尚未成功启动网络模式。
        /// </summary>
        None = 0,

        /// <summary>
        /// 只启动客户端，并连接远程服务器。
        /// </summary>
        Client = 1,

        /// <summary>
        /// 同一进程内同时启动服务器和通过回环地址连接的本地客户端。
        /// </summary>
        Host = 2,

        /// <summary>
        /// 只启动服务器，不创建本地玩家客户端，供无图形专用服务器使用。
        /// </summary>
        DedicatedServer = 3
    }
}

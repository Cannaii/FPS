using System;

namespace AFPS.NetCode.Transport
{
    /// <summary>
    /// AFPS 上层协议使用的字节传输接口，使消息协议不依赖 Unity Transport。
    /// </summary>
    public interface IGameTransport : IDisposable
    {
        /// <summary>
        /// 当前传输实例是否已经作为客户端或服务器启动。
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// 当前传输实例承担的角色。
        /// </summary>
        TransportRole Role { get; }

        /// <summary>
        /// 启动监听服务器；失败原因仅在启动阶段返回，不进入高频数据路径。
        /// </summary>
        bool TryStartServer(ushort port, int maxConnections, out string error);

        /// <summary>
        /// 启动客户端并连接指定的 IP 地址和端口。
        /// </summary>
        bool TryStartClient(string address, ushort port, out string error);

        /// <summary>
        /// 推进底层网络驱动。主线程通常每个渲染帧调用一次。
        /// </summary>
        void Pump();

        /// <summary>
        /// 取出一个网络事件。Data 内容写入 receiveBuffer，且不会为每个包创建新数组。
        /// </summary>
        bool TryPollEvent(ArraySegment<byte> receiveBuffer, out GameTransportEvent transportEvent);

        /// <summary>
        /// 将 payload 交给指定连接和传输通道。
        /// </summary>
        TransportSendResult Send(TransportConnectionId connectionId, TransportDelivery delivery, ArraySegment<byte> payload);

        /// <summary>
        /// 主动断开指定连接。
        /// </summary>
        void Disconnect(TransportConnectionId connectionId);

        /// <summary>
        /// 停止实例并释放所有底层网络资源。
        /// </summary>
        void Stop();
    }
}

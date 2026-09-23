namespace AFPS.NetCode.Runtime
{
    /// <summary>
    /// 标识一个传输事件来自当前进程的服务器侧还是客户端侧实例。
    /// Host 同时拥有两侧，普通 Client 或 Dedicated Server 只拥有其中一侧。
    /// </summary>
    public enum NetworkTransportSide : byte
    {
        Server = 1,
        Client = 2
    }
}

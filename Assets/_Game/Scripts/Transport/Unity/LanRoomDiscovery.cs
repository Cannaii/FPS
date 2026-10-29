using System;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using AFPS.NetCode.Runtime;

namespace AFPS.Transport.Unity
{
    /// <summary>主线程非阻塞IPv4局域网发现；独立Socket，不占用或消费游戏UTP数据。</summary>
    public sealed class LanRoomDiscovery : IDisposable
    {
        public const int DiscoveryPort = 47777;
        private static readonly IPAddress Group = IPAddress.Parse("239.255.42.99");
        private const int MaximumPacketsPerPump = 32;
        private const int MaximumRepliesPerSecond = 32;
        private readonly byte[] receiveBuffer = new byte[256];
        private readonly byte[] sendBuffer = new byte[LanRoomDiscoveryProtocol.AdvertisementSize];
        private Socket socket;
        private bool announcing;
        private double nextQueryAt;
        private double previousTime;
        private double replyWindowStarted;
        private int repliesInWindow;
        private ulong nextNonce;

        public ulong InstanceId { get; private set; }
        public ulong CurrentQueryNonce { get; private set; }
        public int LocalPort { get; private set; }
        public bool IsRunning => socket != null;
        public string LastError { get; private set; }
        public LanRoomDirectory Rooms { get; } = new LanRoomDirectory();

        public bool TryStartBrowser(out string error) => TryStart(false, out error);
        public bool TryStartAnnouncer(out string error) => TryStart(true, out error);

        private bool TryStart(bool asAnnouncer, out string error)
        {
            if (IsRunning)
            {
                error = "局域网发现已经启动。";
                return false;
            }
            try
            {
                // 随机源或Socket不可用都由Try入口报告，不能在构造阶段阻断离线练习菜单。
                if (InstanceId == 0)
                {
                    InstanceId = RandomNonzero();
                    nextNonce = RandomNonzero();
                }
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                if (asAnnouncer)
                {
                    // 多个本机Host/独服共享组播端口；请求送给组成员，回复单播到浏览器临时端口。
                    socket.ExclusiveAddressUse = false;
                    socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                }
                socket.Bind(new IPEndPoint(IPAddress.Any, asAnnouncer ? DiscoveryPort : 0));
                socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 1);
                socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastLoopback, true);
                if (asAnnouncer)
                {
                    socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership,
                        new MulticastOption(Group, IPAddress.Any));
                }
                socket.Blocking = false;
                announcing = asAnnouncer;
                LocalPort = ((IPEndPoint)socket.LocalEndPoint).Port;
                nextQueryAt = 0;
                previousTime = 0;
                replyWindowStarted = 0;
                repliesInWindow = 0;
                LastError = null;
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                error = "局域网搜索不可用：" + exception.Message;
                Stop();
                LastError = error;
                return false;
            }
        }

        public void Refresh()
        {
            // 清目录不绕过1秒查询节流；快速连点刷新不能制造无限组播流量。
            Rooms.Clear();
        }

        public void PumpBrowser(double now)
        {
            if (socket == null || announcing || !ValidTime(now))
            {
                return;
            }
            if (now < previousTime)
            {
                nextQueryAt = now;
                CurrentQueryNonce = 0;
            }
            previousTime = now;
            Rooms.Advance(now);
            if (now >= nextQueryAt)
            {
                CurrentQueryNonce = nextNonce;
                nextNonce = unchecked(nextNonce + 1);
                if (nextNonce == 0)
                {
                    nextNonce = 1;
                }
                nextQueryAt = now + 1; // 单调秒，浏览器最多每秒一次查询。
                LanRoomDiscoveryProtocol.TryWriteQuery(CurrentQueryNonce,
                    new ArraySegment<byte>(sendBuffer, 0, LanRoomDiscoveryProtocol.QuerySize));
                Send(new IPEndPoint(Group, DiscoveryPort), LanRoomDiscoveryProtocol.QuerySize);
            }
            for (int packet = 0; packet < MaximumPacketsPerPump && socket != null; packet++)
            {
                if (!TryReceive(out int count, out IPEndPoint source))
                {
                    break;
                }
                if (LanRoomDiscoveryProtocol.TryReadAdvertisement(
                    new ArraySegment<byte>(receiveBuffer, 0, count), out ulong nonce, out var room)
                    && nonce == CurrentQueryNonce)
                {
                    // 地址只取真实UDP来源；报文只能声明游戏端口，不能让列表指向另一台主机。
                    Rooms.Observe(source.Address.ToString(), room, now);
                }
            }
        }

        public void PumpAnnouncer(in LanRoomAdvertisement room, double now)
        {
            if (socket == null || !announcing || !ValidTime(now) || !room.IsValid
                || room.InstanceId != InstanceId)
            {
                return;
            }
            if (now < replyWindowStarted || now - replyWindowStarted >= 1)
            {
                replyWindowStarted = now;
                repliesInWindow = 0;
            }
            for (int packet = 0; packet < MaximumPacketsPerPump && socket != null; packet++)
            {
                if (!TryReceive(out int count, out IPEndPoint source))
                {
                    break;
                }
                if (repliesInWindow < MaximumRepliesPerSecond
                    && LanRoomDiscoveryProtocol.TryReadQuery(
                        new ArraySegment<byte>(receiveBuffer, 0, count), out ulong nonce)
                    && LanRoomDiscoveryProtocol.TryWriteAdvertisement(nonce, room, new ArraySegment<byte>(sendBuffer)))
                {
                    repliesInWindow++;
                    Send(source, LanRoomDiscoveryProtocol.AdvertisementSize);
                }
            }
        }

        private bool TryReceive(out int count, out IPEndPoint source)
        {
            count = 0;
            source = null;
            try
            {
                // 零超时轮询避免空队列每帧抛WouldBlock异常；零字节数据报仍会被读出并拒绝。
                if (!socket.Poll(0, SelectMode.SelectRead))
                {
                    return false;
                }
                EndPoint endpoint = new IPEndPoint(IPAddress.Any, 0);
                count = socket.ReceiveFrom(receiveBuffer, ref endpoint);
                source = (IPEndPoint)endpoint;
                return true;
            }
            catch (SocketException exception)
            {
                if (exception.SocketErrorCode == SocketError.WouldBlock)
                {
                    return false;
                }
                if (exception.SocketErrorCode == SocketError.MessageSize)
                {
                    // 超长数据报丢弃但继续处理，不能让截断前缀被当作合法响应。
                    return true;
                }
                if (exception.SocketErrorCode == SocketError.ConnectionReset
                    || exception.SocketErrorCode == SocketError.ConnectionRefused)
                {
                    // 浏览器可能在回复到达前关闭临时端口；ICMP端口不可达只属于该对端，
                    // 不能因此关闭共享公告Socket，让后来的所有玩家都搜不到房间。
                    return true;
                }
                LastError = "局域网接收失败：" + exception.Message;
                Stop();
                return false;
            }
        }

        private void Send(IPEndPoint endpoint, int count)
        {
            try
            {
                socket.SendTo(sendBuffer, 0, count, SocketFlags.None, endpoint);
                LastError = null;
            }
            catch (SocketException exception)
            {
                if (exception.SocketErrorCode != SocketError.WouldBlock)
                {
                    LastError = "局域网发送失败：" + exception.Message;
                }
            }
        }

        public void Stop()
        {
            Socket closing = socket;
            socket = null;
            LocalPort = 0;
            CurrentQueryNonce = 0;
            Rooms.Clear();
            closing?.Dispose();
        }

        public void Dispose() => Stop();

        private static bool ValidTime(double time) => !double.IsNaN(time) && !double.IsInfinity(time) && time >= 0;

        private static ulong RandomNonzero()
        {
            byte[] bytes = new byte[8];
            using (var generator = RandomNumberGenerator.Create())
            {
                generator.GetBytes(bytes);
            }
            ulong value = BitConverter.ToUInt64(bytes, 0);
            return value == 0 ? 1UL : value;
        }
    }
}

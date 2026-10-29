using System;
using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace AFPS.NetCode.Runtime
{
    /// <summary>
    /// Validates the values entered by the LAN connection menu and converts them
    /// into the same launch options used by command-line startup.
    /// </summary>
    public static class NetworkConnectionInput
    {
        private const string HostLoopbackAddress = "127.0.0.1";

        public static bool TryCreateOptions(NetworkLaunchMode mode, string addressText, string portText, int maxConnections, out NetworkLaunchOptions options, out string error)
        {
            if (mode == NetworkLaunchMode.Practice)
            {
                // 练习不读取IP和端口，用户无需先修正联网输入框才能测试本机操作。
                options = new NetworkLaunchOptions(mode, HostLoopbackAddress, 0, 1);
                error = null;
                return true;
            }
            if (mode != NetworkLaunchMode.Host && mode != NetworkLaunchMode.Client)
            {
                options = default;
                error = "连接菜单仅支持创建房间、加入房间及单人练习。";
                return false;
            }

            if (mode == NetworkLaunchMode.Client && addressText != null
                && addressText.TrimStart().StartsWith("AF", StringComparison.OrdinalIgnoreCase))
            {
                options = default;
                if (!RoomCode.TryDecode(addressText, out string roomAddress, out ushort roomPort, out error))
                {
                    return false;
                }
                if (maxConnections <= 0)
                {
                    error = "最大连接人数必须大于零。";
                    return false;
                }
                // 房间码包含端口；旧端口输入框内容不得覆盖已经校验的端点。
                options = new NetworkLaunchOptions(mode, roomAddress, roomPort, maxConnections);
                return true;
            }

            if (!ushort.TryParse(portText?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out ushort port) || port == 0)
            {
                options = default;
                error = "端口必须是 1 到 65535 的整数。";
                return false;
            }

            if (maxConnections <= 0)
            {
                options = default;
                error = "最大连接人数必须大于零。";
                return false;
            }

            string address = HostLoopbackAddress;
            if (mode == NetworkLaunchMode.Client)
            {
                address = addressText?.Trim();
                if (!IPAddress.TryParse(address, out IPAddress parsedAddress) || parsedAddress.AddressFamily != AddressFamily.InterNetwork)
                {
                    options = default;
                    error = "请输入有效的 IPv4 地址或完整房间码。";
                    return false;
                }

                address = parsedAddress.ToString();
            }

            options = new NetworkLaunchOptions(mode, address, port, maxConnections);
            error = null;
            return true;
        }
    }
}

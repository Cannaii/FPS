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
            if (mode != NetworkLaunchMode.Host && mode != NetworkLaunchMode.Client)
            {
                options = default;
                error = "The connection menu supports only Host and Client modes.";
                return false;
            }

            if (!ushort.TryParse(portText?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out ushort port) || port == 0)
            {
                options = default;
                error = "Port must be a number from 1 to 65535.";
                return false;
            }

            if (maxConnections <= 0)
            {
                options = default;
                error = "Maximum connections must be greater than zero.";
                return false;
            }

            string address = HostLoopbackAddress;
            if (mode == NetworkLaunchMode.Client)
            {
                address = addressText?.Trim();
                if (!IPAddress.TryParse(address, out IPAddress parsedAddress) || parsedAddress.AddressFamily != AddressFamily.InterNetwork)
                {
                    options = default;
                    error = "Enter a valid IPv4 address, for example 192.168.1.20.";
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

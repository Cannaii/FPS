using System;
using System.Globalization;

namespace AFPS.NetCode.Runtime
{
    /// <summary>
    /// 将 Unity 进程命令行中的 AFPS 参数覆盖到 Inspector 默认启动参数。
    /// 支持“-参数=值”和“-参数 值”两种形式。
    /// </summary>
    public static class NetworkLaunchCommandLine
    {
        public static bool TryApply(string[] arguments, in NetworkLaunchOptions defaults, out NetworkLaunchOptions options, out string error)
        {
            NetworkLaunchMode mode = defaults.Mode;
            string address = defaults.ServerAddress;
            ushort port = defaults.Port;
            int maxConnections = defaults.MaxConnections;

            if (arguments == null)
            {
                options = defaults;
                error = null;
                return true;
            }

            for (int i = 0; i < arguments.Length; i++)
            {
                if (TryReadValue(arguments, ref i, "-afpsMode", out string modeValue, out error))
                {
                    if (!TryParseMode(modeValue, out mode))
                    {
                        options = default;
                        error = $"无法识别 AFPS 启动模式“{modeValue}”。可用值为 client、host、server。";
                        return false;
                    }

                    continue;
                }

                if (error != null)
                {
                    options = default;
                    return false;
                }

                if (TryReadValue(arguments, ref i, "-afpsAddress", out string addressValue, out error))
                {
                    if (string.IsNullOrWhiteSpace(addressValue))
                    {
                        options = default;
                        error = "AFPS 服务器地址不能为空。";
                        return false;
                    }

                    address = addressValue;
                    continue;
                }

                if (error != null)
                {
                    options = default;
                    return false;
                }

                if (TryReadValue(arguments, ref i, "-afpsPort", out string portValue, out error))
                {
                    if (!ushort.TryParse(portValue, NumberStyles.None, CultureInfo.InvariantCulture, out port) || port == 0)
                    {
                        options = default;
                        error = $"AFPS UDP 端口“{portValue}”必须在 1 到 65535 之间。";
                        return false;
                    }

                    continue;
                }

                if (error != null)
                {
                    options = default;
                    return false;
                }

                if (TryReadValue(arguments, ref i, "-afpsMaxConnections", out string maxConnectionsValue, out error))
                {
                    if (!int.TryParse(maxConnectionsValue, NumberStyles.None, CultureInfo.InvariantCulture, out maxConnections) || maxConnections <= 0)
                    {
                        options = default;
                        error = $"AFPS 最大连接数“{maxConnectionsValue}”必须大于零。";
                        return false;
                    }
                }

                if (error != null)
                {
                    options = default;
                    return false;
                }
            }

            options = new NetworkLaunchOptions(mode, address, port, maxConnections);
            error = null;
            return true;
        }

        private static bool TryReadValue(string[] arguments, ref int index, string optionName, out string value, out string error)
        {
            string argument = arguments[index];
            if (argument.Equals(optionName, StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= arguments.Length || arguments[index + 1].StartsWith("-", StringComparison.Ordinal))
                {
                    value = null;
                    error = $"命令行参数 {optionName} 缺少值。";
                    return false;
                }

                value = arguments[++index];
                error = null;
                return true;
            }

            string prefix = optionName + "=";
            if (argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                value = argument.Substring(prefix.Length);
                error = null;
                return true;
            }

            value = null;
            error = null;
            return false;
        }

        private static bool TryParseMode(string value, out NetworkLaunchMode mode)
        {
            if (value.Equals("client", StringComparison.OrdinalIgnoreCase))
            {
                mode = NetworkLaunchMode.Client;
                return true;
            }

            if (value.Equals("host", StringComparison.OrdinalIgnoreCase))
            {
                mode = NetworkLaunchMode.Host;
                return true;
            }

            if (value.Equals("server", StringComparison.OrdinalIgnoreCase) || value.Equals("dedicated", StringComparison.OrdinalIgnoreCase) || value.Equals("dedicatedserver", StringComparison.OrdinalIgnoreCase))
            {
                mode = NetworkLaunchMode.DedicatedServer;
                return true;
            }

            mode = NetworkLaunchMode.None;
            return false;
        }
    }
}

using System;
using System.Net;
using System.Net.Sockets;

namespace AFPS.NetCode.Runtime
{
    /// <summary>局域网直连端点的可复制房间码；不分配公网短码、不提供身份认证或NAT穿透。</summary>
    public static class RoomCode
    {
        private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
        private const string Prefix = "AF1-";
        public const int CodeLength = 19;

        public static bool TryEncode(string address, ushort port, out string code, out string error)
        {
            code = null;
            if (port == 0 || !IPAddress.TryParse(address, out var endpoint)
                || endpoint.AddressFamily != AddressFamily.InterNetwork)
            {
                error = "房间码需要有效的 IPv4 地址和 1～65535 端口。";
                return false;
            }
            byte[] bytes = new byte[8];
            endpoint.GetAddressBytes().CopyTo(bytes, 0);
            if (!IsUnicast(bytes))
            {
                error = "不能为未指定地址、广播或组播地址生成房间码。";
                return false;
            }
            // IPv4及端口按网络序排列；CRC16-CCITT-FALSE保护前6字节，校验仅用于发现抄写错误。
            bytes[4] = (byte)(port >> 8);
            bytes[5] = (byte)(port & 255);
            ushort checksum = Checksum(bytes);
            bytes[6] = (byte)(checksum >> 8);
            bytes[7] = (byte)(checksum & 255);
            char[] payload = new char[13];
            for (int group = 0; group < payload.Length; group++)
            {
                int value = 0;
                for (int bit = 0; bit < 5; bit++)
                {
                    int sourceBit = group * 5 + bit;
                    value <<= 1;
                    if (sourceBit < 64)
                    {
                        value |= (bytes[sourceBit / 8] >> (7 - sourceBit % 8)) & 1;
                    }
                }
                payload[group] = Alphabet[value];
            }
            string encoded = new string(payload);
            code = Prefix + encoded.Substring(0, 4) + "-" + encoded.Substring(4, 4)
                + "-" + encoded.Substring(8, 5);
            error = null;
            return true;
        }

        public static bool TryDecode(string text, out string address, out ushort port, out string error)
        {
            address = null;
            port = 0;
            error = "房间码格式错误，请完整复制 AF1 开头的房间码。";
            // 有界输入，避免在不受信任的粘贴文本上分配任意大的临时字符串。
            if (text == null || text.Length > 64)
            {
                return false;
            }
            string code = text.Trim();
            if (code.Length != CodeLength || !code.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)
                || code[8] != '-' || code[13] != '-')
            {
                return false;
            }
            byte[] bytes = new byte[8];
            int targetBit = 0;
            for (int index = 4; index < code.Length; index++)
            {
                if (index == 8 || index == 13)
                {
                    continue;
                }
                int value = Alphabet.IndexOf(char.ToUpperInvariant(code[index]));
                if (value < 0)
                {
                    return false;
                }
                for (int bit = 4; bit >= 0; bit--)
                {
                    int digit = (value >> bit) & 1;
                    if (targetBit < 64)
                    {
                        bytes[targetBit / 8] |= (byte)(digit << (7 - targetBit % 8));
                    }
                    else if (digit != 0)
                    {
                        // 64位只需13个Base32字符，末位补0；拒绝非规范别名，避免校验绕过。
                        return false;
                    }
                    targetBit++;
                }
            }
            if (Checksum(bytes) != (ushort)((bytes[6] << 8) | bytes[7]))
            {
                error = "房间码校验失败，请重新复制。";
                return false;
            }
            ushort decodedPort = (ushort)((bytes[4] << 8) | bytes[5]);
            if (!IsUnicast(bytes) || decodedPort == 0)
            {
                error = "房间码中的服务器地址或端口无效。";
                return false;
            }
            address = new IPAddress(new[] { bytes[0], bytes[1], bytes[2], bytes[3] }).ToString();
            port = decodedPort;
            error = null;
            return true;
        }

        private static bool IsUnicast(byte[] address)
        {
            // 回环允许同机多进程测试；0网段、224～255组播/保留地址不能作为直连服务器。
            return address[0] > 0 && address[0] < 224;
        }

        private static ushort Checksum(byte[] bytes)
        {
            uint checksum = 0xffff;
            for (int index = 0; index < 6; index++)
            {
                checksum ^= (uint)bytes[index] << 8;
                for (int bit = 0; bit < 8; bit++)
                {
                    checksum = ((checksum & 0x8000) != 0
                        ? (checksum << 1) ^ 0x1021 : checksum << 1) & 0xffff;
                }
            }
            return (ushort)checksum;
        }
    }
}

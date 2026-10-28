using System;
using System.Security.Cryptography;

namespace AFPS.NetCode.Sessions
{
    /// <summary>
    /// 仅用于当前服务器进程/对局的256位随机持有凭据，不能用实体ID、时间或确定性随机数替代。
    /// UTP本身不加密；此凭据防猜测和错误身份恢复，不提供抗窃听保证。
    /// </summary>
    public readonly struct SessionResumeCredential : IEquatable<SessionResumeCredential>
    {
        internal readonly ulong Part0;
        internal readonly ulong Part1;
        internal readonly ulong Part2;
        internal readonly ulong Part3;

        public bool IsValid => (Part0 | Part1 | Part2 | Part3) != 0;

        internal SessionResumeCredential(ulong part0, ulong part1, ulong part2, ulong part3)
        {
            Part0 = part0;
            Part1 = part1;
            Part2 = part2;
            Part3 = part3;
        }

        public static SessionResumeCredential Create()
        {
            byte[] bytes = new byte[32];
            try
            {
                using (RandomNumberGenerator generator = RandomNumberGenerator.Create())
                {
                    // 全零保留为“无凭据”。有限重试并失败关闭，不能回退到可预测随机源。
                    for (int attempt = 0; attempt < 4; attempt++)
                    {
                        generator.GetBytes(bytes);
                        var credential = new SessionResumeCredential(
                            ReadUInt64(bytes, 0), ReadUInt64(bytes, 8),
                            ReadUInt64(bytes, 16), ReadUInt64(bytes, 24));
                        if (credential.IsValid)
                        {
                            return credential;
                        }
                    }
                }
                throw new CryptographicException("未能生成有效的会话恢复凭据。");
            }
            finally
            {
                Array.Clear(bytes, 0, bytes.Length);
            }
        }

        public bool Equals(SessionResumeCredential other)
        {
            // 固定比较全部256位，不按首个不同字节提前退出；托管运行时不承诺机器码级恒时。
            ulong difference = (Part0 ^ other.Part0) | (Part1 ^ other.Part1)
                | (Part2 ^ other.Part2) | (Part3 ^ other.Part3);
            return difference == 0;
        }

        public override bool Equals(object obj) => obj is SessionResumeCredential other && Equals(other);

        // 凭据无需作为字典键，避免散列或日志意外泄漏内容。
        public override int GetHashCode() => 0;

        public override string ToString() => "[会话恢复凭据]";

        private static ulong ReadUInt64(byte[] bytes, int offset)
        {
            ulong result = 0;
            for (int i = 0; i < 8; i++)
            {
                result |= (ulong)bytes[offset + i] << (i * 8);
            }
            return result;
        }
    }
}

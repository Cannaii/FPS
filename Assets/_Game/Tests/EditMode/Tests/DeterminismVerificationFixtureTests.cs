using System;
using System.IO;
using System.Security.Cryptography;
using AFPS.NetCode.Replay;
using AFPS.Verification.Editor;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class DeterminismVerificationFixtureTests
    {
        [Test]
        public void PortableFixtureHasFrozenInputBytesAndOneContinuousWraparoundSequence()
        {
            byte[] bytes = DeterminismVerificationBuild.CreateFixture();
            using (var sha = SHA256.Create())
            {
                string hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
                // v29仅更新轨迹头与输入包的协议字节；世界、输入及预期模拟哈希保持原语义。
                Assert.That(hash, Is.EqualTo("70F851BACA366663E324190BD4947D70052F65B1654A9A6F9FFF6EFF86403AB6"));
            }
            using (var stream = new MemoryStream(bytes, false))
            {
                var result = AuthoritativeMovementTraceReplay.Run(stream);
                Assert.That(result.Frames, Is.EqualTo(2048));
                Assert.That(result.Resets, Is.EqualTo(1));
                Assert.That(result.EntityId, Is.EqualTo(1));
                Assert.That(result.Identity.AlgorithmVersion, Is.EqualTo(21));
            }
        }
    }
}

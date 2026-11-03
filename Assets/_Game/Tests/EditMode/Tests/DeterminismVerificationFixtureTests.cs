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
                // 算法22/AFCE与世界包v2更新轨迹头；输入字节及预期移动哈希保持原语义。
                Assert.That(hash, Is.EqualTo("5005C7C0B8A8970C113C48A12801A639FAE4A206BFA2E4F64172BD5422FC3485"));
            }
            using (var stream = new MemoryStream(bytes, false))
            {
                var result = AuthoritativeMovementTraceReplay.Run(stream);
                Assert.That(result.Frames, Is.EqualTo(2048));
                Assert.That(result.Resets, Is.EqualTo(1));
                Assert.That(result.EntityId, Is.EqualTo(1));
                Assert.That(result.Identity.AlgorithmVersion, Is.EqualTo(22));
            }
        }
    }
}

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
                // 同一输入包才有跨平台比较意义；协议或套件变更必须显式更新该黄金值。
                Assert.That(hash, Is.EqualTo("344B6321BD2A4219F02895C032A3A87014A2E08F382C0E1BE5E53509ADF824CF"));
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

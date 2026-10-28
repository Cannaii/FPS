using System;
using System.Collections.Generic;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.Sessions;
using AFPS.Simulation.World;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class SessionResumeCredentialTests
    {
        private static readonly SimulationWorldIdentity Identity = new SimulationWorldIdentity(
            21, 0x0807060504030201UL, 0x1817161514131211UL);

        [Test]
        public void Grant_HasStableLittleEndianLayoutAndHonorsArraySlice()
        {
            SessionResumeCredential credential = KnownCredential();
            byte[] bytes = Filled(SessionResumeCodec.GrantPacketSize + 6);
            Assert.That(SessionResumeCodec.TryWriteGrant(
                0x04030201, credential, 0x14131211,
                new ArraySegment<byte>(bytes, 3, SessionResumeCodec.GrantPacketSize), out int length), Is.True);
            Assert.That(length, Is.EqualTo(48));
            CollectionAssert.AreEqual(new byte[]
            {
                0x41, 0x46, 0x50, 0x53, 24, 16, 36, 0, 0x11, 0x12, 0x13, 0x14,
                1, 2, 3, 4
            }, new ArraySegment<byte>(bytes, 3, 16));
            for (int i = 0; i < 32; i++)
            {
                Assert.That(bytes[3 + PacketHeader.Size + 4 + i], Is.EqualTo(i + 1));
            }
            AssertGuards(bytes);
            Assert.That(SessionResumeCodec.TryReadGrant(
                new ArraySegment<byte>(bytes, 3, length), out uint attempt, out uint entity, out var decoded), Is.True);
            Assert.That(attempt, Is.EqualTo(0x14131211));
            Assert.That(entity, Is.EqualTo(0x04030201));
            Assert.That(decoded.Equals(credential), Is.True);
        }

        [Test]
        public void Request_CarriesIdentityAndCredentialWithoutClientSelectedEntityId()
        {
            var credential = KnownCredential();
            byte[] bytes = Filled(SessionResumeCodec.RequestPacketSize + 6);
            Assert.That(SessionResumeCodec.TryWriteRequest(Identity, credential, uint.MaxValue,
                new ArraySegment<byte>(bytes, 3, SessionResumeCodec.RequestPacketSize), out int length), Is.True);
            Assert.That(length, Is.EqualTo(64));
            CollectionAssert.AreEqual(new byte[]
            {
                21, 0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8,
                0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18
            }, new ArraySegment<byte>(bytes, 3 + PacketHeader.Size, 20));
            AssertGuards(bytes);
            Assert.That(SessionResumeCodec.TryReadRequest(new ArraySegment<byte>(bytes, 3, length),
                out uint attempt, out var identity, out var decoded), Is.True);
            Assert.That(attempt, Is.EqualTo(uint.MaxValue));
            Assert.That(identity.Equals(Identity), Is.True);
            Assert.That(decoded.Equals(credential), Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MalformedPackets_RejectEveryTruncationAndTrailingBytes(bool request)
        {
            byte[] valid = Packet(request);
            for (int length = 0; length < valid.Length; length++)
            {
                AssertRejected(new ArraySegment<byte>(valid, 0, length), request);
            }
            byte[] extra = new byte[valid.Length + 1];
            Array.Copy(valid, extra, valid.Length);
            AssertRejected(new ArraySegment<byte>(extra), request);
            // 即使包头长度也改成包含尾随字节，固定消息负载仍必须严格匹配。
            extra[6]++;
            AssertRejected(new ArraySegment<byte>(extra), request);
            AssertRejected(default, request);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void InvalidHeaderOrPayload_DoesNotPublishPartialDecodedIdentity(bool request)
        {
            byte[] valid = Packet(request);
            int[] corruptOffsets = { 0, 4, 5, 6 };
            foreach (int offset in corruptOffsets)
            {
                byte[] corrupted = (byte[])valid.Clone();
                corrupted[offset] ^= 0x80;
                AssertRejected(new ArraySegment<byte>(corrupted), request);
            }
            byte[] wrongType = (byte[])valid.Clone();
            wrongType[5] = (byte)(request
                ? NetworkMessageType.SessionResumeGrant : NetworkMessageType.SessionResumeRequest);
            AssertRejected(new ArraySegment<byte>(wrongType), request);

            byte[] oldVersion = (byte[])valid.Clone();
            oldVersion[4] = 21;
            AssertRejected(new ArraySegment<byte>(oldVersion), request);

            byte[] zeroAttempt = (byte[])valid.Clone();
            Array.Clear(zeroAttempt, 8, 4);
            AssertRejected(new ArraySegment<byte>(zeroAttempt), request);

            byte[] zeroIdentity = (byte[])valid.Clone();
            Array.Clear(zeroIdentity, PacketHeader.Size, 4);
            AssertRejected(new ArraySegment<byte>(zeroIdentity), request);

            byte[] zeroCredential = (byte[])valid.Clone();
            Array.Clear(zeroCredential, zeroCredential.Length - SessionResumeCodec.CredentialSize, 32);
            AssertRejected(new ArraySegment<byte>(zeroCredential), request);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void InvalidWrite_RejectsZeroAttemptCredentialIdentityAndShortBuffers(bool request)
        {
            byte[] bytes = new byte[64];
            var credential = KnownCredential();
            if (request)
            {
                Assert.That(SessionResumeCodec.TryWriteRequest(Identity, credential, 0,
                    new ArraySegment<byte>(bytes), out int count), Is.False);
                Assert.That(count, Is.Zero);
                Assert.That(SessionResumeCodec.TryWriteRequest(default, credential, 1,
                    new ArraySegment<byte>(bytes), out count), Is.False);
                Assert.That(SessionResumeCodec.TryWriteRequest(Identity, default, 1,
                    new ArraySegment<byte>(bytes), out count), Is.False);
                Assert.That(SessionResumeCodec.TryWriteRequest(Identity, credential, 1,
                    new ArraySegment<byte>(bytes, 0, 63), out count), Is.False);
                Assert.That(SessionResumeCodec.TryWriteRequest(Identity, credential, 1, default, out count), Is.False);
                Assert.That(count, Is.Zero);
            }
            else
            {
                Assert.That(SessionResumeCodec.TryWriteGrant(1, credential, 0,
                    new ArraySegment<byte>(bytes), out int count), Is.False);
                Assert.That(count, Is.Zero);
                Assert.That(SessionResumeCodec.TryWriteGrant(0, credential, 1,
                    new ArraySegment<byte>(bytes), out count), Is.False);
                Assert.That(SessionResumeCodec.TryWriteGrant(1, default, 1,
                    new ArraySegment<byte>(bytes), out count), Is.False);
                Assert.That(SessionResumeCodec.TryWriteGrant(1, credential, 1,
                    new ArraySegment<byte>(bytes, 0, 47), out count), Is.False);
                Assert.That(SessionResumeCodec.TryWriteGrant(1, credential, 1, default, out count), Is.False);
                Assert.That(count, Is.Zero);
            }
            Assert.That(bytes, Is.All.Zero);
        }

        [Test]
        public void Store_BindsOnlyServerIssuedCredentialsAndComparesAllCredentialBytes()
        {
            var store = new ServerSessionCredentialStore();
            Assert.That(store.TryIssue(7, out var credential), Is.True);
            Assert.That(store.TryIssue(9, out var other), Is.True);
            Assert.That(store.TryResolve(credential, out uint entity), Is.True);
            Assert.That(entity, Is.EqualTo(7));
            Assert.That(store.TryResolve(other, out entity), Is.True);
            Assert.That(entity, Is.EqualTo(9));
            byte[] grant = new byte[SessionResumeCodec.GrantPacketSize];
            Assert.That(SessionResumeCodec.TryWriteGrant(7, credential, 1,
                new ArraySegment<byte>(grant), out _), Is.True);
            for (int i = 0; i < 32; i++)
            {
                byte[] mutated = (byte[])grant.Clone();
                mutated[PacketHeader.Size + 4 + i] ^= 1;
                Assert.That(SessionResumeCodec.TryReadGrant(new ArraySegment<byte>(mutated),
                    out _, out _, out var forged), Is.True);
                Assert.That(forged.Equals(credential), Is.False);
                Assert.That(store.TryResolve(forged, out entity), Is.False);
                Assert.That(entity, Is.Zero);
            }
            Assert.That(store.TryResolve(default, out entity), Is.False);
            Assert.That(entity, Is.Zero);
            Assert.That(store.Count, Is.EqualTo(2));
        }

        [Test]
        public void Store_FullDuplicateAndInvalidEntityDoNotEvictOrRotateCredentials()
        {
            var store = new ServerSessionCredentialStore(1);
            Assert.That(store.TryIssue(0, out var empty), Is.False);
            Assert.That(empty.IsValid, Is.False);
            Assert.That(store.TryIssue(1, out var original), Is.True);
            Assert.That(store.TryIssue(1, out empty), Is.False);
            Assert.That(empty.IsValid, Is.False);
            Assert.That(store.TryIssue(2, out empty), Is.False);
            Assert.That(empty.IsValid, Is.False);
            Assert.That(store.TryResolve(original, out uint entity), Is.True);
            Assert.That(entity, Is.EqualTo(1));
            Assert.That(store.TryGetIssued(1, out var retry), Is.True);
            Assert.That(retry.Equals(original), Is.True);
            Assert.That(store.TryGetIssued(2, out var missing), Is.False);
            Assert.That(missing.IsValid, Is.False);
            Assert.That(store.Count, Is.EqualTo(1));
        }

        [Test]
        public void Store_RevokeClearAndReissueInvalidateOldCredentials()
        {
            var store = new ServerSessionCredentialStore(2);
            Assert.That(store.TryIssue(1, out var first), Is.True);
            Assert.That(store.TryIssue(2, out var second), Is.True);
            Assert.That(store.Revoke(99), Is.False);
            Assert.That(store.Revoke(1), Is.True);
            Assert.That(store.Revoke(1), Is.False);
            Assert.That(store.TryResolve(first, out _), Is.False);
            Assert.That(store.TryGetIssued(1, out var missing), Is.False);
            Assert.That(missing.IsValid, Is.False);
            Assert.That(store.TryResolve(second, out uint entity), Is.True);
            Assert.That(entity, Is.EqualTo(2));
            Assert.That(store.TryIssue(1, out var replacement), Is.True);
            Assert.That(replacement.Equals(first), Is.False);
            Assert.That(store.TryResolve(first, out _), Is.False);
            store.Clear();
            Assert.That(store.Count, Is.Zero);
            Assert.That(store.TryResolve(second, out _), Is.False);
            Assert.That(store.TryResolve(replacement, out _), Is.False);
            Assert.That(store.TryIssue(3, out _), Is.True);
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(65)]
        public void Store_InvalidCapacityThrows(int capacity)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ServerSessionCredentialStore(capacity));
        }

        [Test]
        public void Store_MaximumCapacityAndServerInstanceIsolation()
        {
            var first = new ServerSessionCredentialStore(64);
            var second = new ServerSessionCredentialStore();
            var issued = new List<SessionResumeCredential>();
            for (uint entity = 1; entity <= 64; entity++)
            {
                Assert.That(first.TryIssue(entity, out var credential), Is.True);
                issued.Add(credential);
                Assert.That(second.TryResolve(credential, out _), Is.False);
            }
            Assert.That(first.TryIssue(65, out _), Is.False);
            Assert.That(first.Count, Is.EqualTo(64));
            for (int i = 0; i < issued.Count; i++)
            {
                Assert.That(first.TryResolve(issued[i], out uint entity), Is.True);
                Assert.That(entity, Is.EqualTo(i + 1));
            }
        }

        [Test]
        public void ValidCredential_DoesNotImplyWorldCompatibilityOrConsumeIdentity()
        {
            var store = new ServerSessionCredentialStore();
            Assert.That(store.TryIssue(7, out var credential), Is.True);
            var foreign = new SimulationWorldIdentity(21, Identity.WorldHash + 1, Identity.ConfigurationHash);
            byte[] packet = new byte[SessionResumeCodec.RequestPacketSize];
            Assert.That(SessionResumeCodec.TryWriteRequest(foreign, credential, 1,
                new ArraySegment<byte>(packet), out _), Is.True);
            Assert.That(SessionResumeCodec.TryReadRequest(new ArraySegment<byte>(packet),
                out _, out var decodedIdentity, out var decodedCredential), Is.True);
            Assert.That(SimulationHandshakeCodec.Compare(Identity, decodedIdentity),
                Is.EqualTo(SimulationCompatibility.WorldMismatch));
            Assert.That(store.TryResolve(decodedCredential, out uint entity), Is.True);
            Assert.That(entity, Is.EqualTo(7));
            // 校验是只读查询；失败/丢包重试不能自行消耗、迁移或撤销身份。
            Assert.That(store.TryResolve(decodedCredential, out entity), Is.True);
            Assert.That(store.Count, Is.EqualTo(1));
        }

        [Test]
        public void RandomCredentials_AreNonzeroDistinctAndRedactedInLogs()
        {
            var seen = new HashSet<SessionResumeCredential>();
            for (int i = 0; i < 64; i++)
            {
                var credential = SessionResumeCredential.Create();
                Assert.That(credential.IsValid, Is.True);
                Assert.That(seen.Add(credential), Is.True);
                Assert.That(credential.ToString(), Is.EqualTo("[会话恢复凭据]"));
                Assert.That(credential.Equals((object)credential), Is.True);
                Assert.That(credential.Equals(null), Is.False);
            }
            Assert.That(default(SessionResumeCredential).IsValid, Is.False);
        }

        private static SessionResumeCredential KnownCredential()
        {
            byte[] bytes = new byte[SessionResumeCodec.GrantPacketSize];
            Assert.That(PacketHeaderCodec.TryWrite(new PacketHeader(
                NetworkMessageType.SessionResumeGrant, SessionResumeCodec.GrantPayloadSize, 1),
                new ArraySegment<byte>(bytes)), Is.True);
            bytes[PacketHeader.Size] = 1;
            for (int i = 0; i < 32; i++)
            {
                bytes[PacketHeader.Size + 4 + i] = (byte)(i + 1);
            }
            Assert.That(SessionResumeCodec.TryReadGrant(new ArraySegment<byte>(bytes),
                out _, out _, out var credential), Is.True);
            return credential;
        }

        private static byte[] Packet(bool request)
        {
            byte[] bytes = new byte[request ? SessionResumeCodec.RequestPacketSize : SessionResumeCodec.GrantPacketSize];
            if (request)
            {
                Assert.That(SessionResumeCodec.TryWriteRequest(Identity, KnownCredential(), 1,
                    new ArraySegment<byte>(bytes), out _), Is.True);
            }
            else
            {
                Assert.That(SessionResumeCodec.TryWriteGrant(1, KnownCredential(), 1,
                    new ArraySegment<byte>(bytes), out _), Is.True);
            }
            return bytes;
        }

        private static void AssertRejected(ArraySegment<byte> packet, bool request)
        {
            if (request)
            {
                Assert.That(SessionResumeCodec.TryReadRequest(packet, out uint attempt,
                    out var identity, out var credential), Is.False);
                Assert.That(attempt, Is.Zero);
                Assert.That(identity.AlgorithmVersion, Is.Zero);
                Assert.That(credential.IsValid, Is.False);
            }
            else
            {
                Assert.That(SessionResumeCodec.TryReadGrant(packet, out uint attempt,
                    out uint entity, out var credential), Is.False);
                Assert.That(attempt, Is.Zero);
                Assert.That(entity, Is.Zero);
                Assert.That(credential.IsValid, Is.False);
            }
        }

        private static byte[] Filled(int length)
        {
            byte[] bytes = new byte[length];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = 0xCC;
            }
            return bytes;
        }

        private static void AssertGuards(byte[] bytes)
        {
            for (int i = 0; i < 3; i++)
            {
                Assert.That(bytes[i], Is.EqualTo(0xCC));
                Assert.That(bytes[bytes.Length - 1 - i], Is.EqualTo(0xCC));
            }
        }
    }
}

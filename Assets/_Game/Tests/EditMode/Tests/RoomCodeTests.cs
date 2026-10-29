using System;
using AFPS.NetCode.Runtime;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class RoomCodeTests
    {
        [TestCase("192.168.1.20", 7777, "AF1-R2M0-250Y-C4PRA")]
        [TestCase("127.0.0.1", 1, "AF1-FW00-0080-06MSW")]
        [TestCase("223.255.255.255", 65535, "AF1-VZZZ-ZZZZ-ZYPCE")]
        public void EndpointHasStableGoldenCodeAndRoundTrips(string address, int port, string golden)
        {
            Assert.That(RoomCode.TryEncode(address, (ushort)port, out string code, out string error), Is.True, error);
            Assert.That(code, Is.EqualTo(golden));
            Assert.That(RoomCode.TryDecode(" " + code.ToLowerInvariant() + " ", out string decoded,
                out ushort decodedPort, out error), Is.True, error);
            Assert.That(decoded, Is.EqualTo(address));
            Assert.That(decodedPort, Is.EqualTo(port));
        }

        [TestCase(null, 7777)]
        [TestCase("::1", 7777)]
        [TestCase("localhost", 7777)]
        [TestCase("0.0.0.0", 7777)]
        [TestCase("224.0.0.1", 7777)]
        [TestCase("255.255.255.255", 7777)]
        [TestCase("192.168.1.20", 0)]
        public void InvalidEndpointDoesNotProduceCode(string address, int port)
        {
            Assert.That(RoomCode.TryEncode(address, (ushort)port, out string code, out string error), Is.False);
            Assert.That(code, Is.Null);
            Assert.That(error, Is.Not.Empty);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("AF2-R2M0-250Y-C4PRA")]
        [TestCase("AF1R2M0-250Y-C4PRA")]
        [TestCase("AF1-R2M0_250Y-C4PRA")]
        [TestCase("AF1-R2MO-250Y-C4PRA")]
        [TestCase("AF1-R2M0-250Y-C4PRB")]
        [TestCase("AF1-R2M0-250Y-C4PRC")]
        [TestCase("AF1-R2M0-250Y-C4PRA-extra")]
        [TestCase("AF1-0000-000Y-C59EP")]
        [TestCase("AF1-W000-008Y-C7QE6")]
        [TestCase("AF1-R2M0-2500-01RQW")]
        public void InvalidCodeNeverReturnsPartialEndpoint(string code)
        {
            Assert.That(RoomCode.TryDecode(code, out string address, out ushort port, out string error), Is.False);
            Assert.That(address, Is.Null);
            Assert.That(port, Is.Zero);
            Assert.That(error, Is.Not.Empty);
        }

        [Test]
        public void EverySinglePayloadSymbolMutationAndOversizedInputAreRejected()
        {
            const string golden = "AF1-R2M0-250Y-C4PRA";
            const string alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
            for (int index = 4; index < golden.Length; index++)
            {
                if (golden[index] == '-')
                {
                    continue;
                }
                foreach (char symbol in alphabet)
                {
                    if (symbol == golden[index])
                    {
                        continue;
                    }
                    char[] mutation = golden.ToCharArray();
                    mutation[index] = symbol;
                    Assert.That(RoomCode.TryDecode(new string(mutation), out _, out _, out _), Is.False);
                }
            }
            Assert.That(RoomCode.TryDecode(new string(' ', 10000) + golden, out _, out _, out _), Is.False);
        }

        [Test]
        public void ConnectionInputUsesCodePortAndPreservesHostPracticeAndIpv4Paths()
        {
            Assert.That(NetworkConnectionInput.TryCreateOptions(NetworkLaunchMode.Client,
                "AF1-R2M0-250Y-C4PRA", "invalid", 32, out var options, out _), Is.True);
            Assert.That(options.ServerAddress, Is.EqualTo("192.168.1.20"));
            Assert.That(options.Port, Is.EqualTo(7777));
            Assert.That(NetworkConnectionInput.TryCreateOptions(NetworkLaunchMode.Client,
                "AF1-R2M0-250Y-C4PRA", "1", 0, out _, out _), Is.False);
            Assert.That(NetworkConnectionInput.TryCreateOptions(NetworkLaunchMode.Client,
                "AF1-R2M0-250Y-C4PRC", "7777", 32, out _, out _), Is.False);
            Assert.That(NetworkConnectionInput.TryCreateOptions(NetworkLaunchMode.Host,
                "AF1-bad", "8888", 32, out options, out _), Is.True);
            Assert.That(options.ServerAddress, Is.EqualTo("127.0.0.1"));
            Assert.That(options.Port, Is.EqualTo(8888));
            Assert.That(NetworkConnectionInput.TryCreateOptions(NetworkLaunchMode.Practice,
                "AF1-bad", "invalid", 0, out options, out _), Is.True);
            Assert.That(options.Port, Is.Zero);
        }
    }
}

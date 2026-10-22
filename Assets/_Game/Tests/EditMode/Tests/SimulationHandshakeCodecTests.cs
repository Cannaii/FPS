using System;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Protocol;
using AFPS.Simulation.World;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class SimulationHandshakeCodecTests
    {
        [TestCase(NetworkMessageType.SimulationHello, 32)]
        [TestCase(NetworkMessageType.SimulationWelcome, 33)]
        [TestCase(NetworkMessageType.SimulationReady, 32)]
        public void IdentityIsLosslessLittleEndianAndSupportsOffset(NetworkMessageType type, int size)
        {
            var identity = new SimulationWorldIdentity(0x01020304, 0x0807060504030201UL, ulong.MaxValue);
            byte[] envelope = new byte[SimulationHandshakeCodec.MaximumPacketSize + 7];
            Assert.That(SimulationHandshakeCodec.TrySerialize(type, uint.MaxValue, identity,
                SimulationCompatibility.Compatible, new ArraySegment<byte>(envelope, 3, size), out int count), Is.True);
            Assert.That(count, Is.EqualTo(size));
            byte[] golden = { 4, 3, 2, 1, 1, 2, 3, 4, 5, 6, 7, 8, 255, 255, 255, 255, 255, 255, 255, 255 };
            for (int i = 0; i < golden.Length; i++)
            {
                Assert.That(envelope[3 + PacketHeader.Size + i], Is.EqualTo(golden[i]));
            }

            Assert.That(SimulationHandshakeCodec.TryDeserialize(new ArraySegment<byte>(envelope, 3, count),
                out PacketHeader header, out SimulationWorldIdentity actual, out SimulationCompatibility result), Is.True);
            Assert.That(actual, Is.EqualTo(identity));
            Assert.That(header.Sequence, Is.EqualTo(uint.MaxValue));
            Assert.That(result, Is.EqualTo(SimulationCompatibility.Compatible));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void InvalidHeaderOrIdentityCannotEnterHandshake(int corruption)
        {
            byte[] bytes = new byte[SimulationHandshakeCodec.MaximumPacketSize];
            var identity = new SimulationWorldIdentity(1, 2, 3);
            SimulationHandshakeCodec.TrySerialize(NetworkMessageType.SimulationWelcome, 1, identity,
                SimulationCompatibility.Compatible, new ArraySegment<byte>(bytes), out _);
            switch (corruption)
            {
                case 0:
                    bytes[4] = 127; // 通用协议版本不兼容。
                    break;
                case 1:
                    bytes[5] = 255; // 非握手消息。
                    break;
                case 2:
                    bytes[6] = 0; // 声明长度与实际负载不符。
                    break;
                case 3:
                    Array.Clear(bytes, 8, 4); // 零尝试序号保留。
                    break;
                case 4:
                    Array.Clear(bytes, PacketHeader.Size, 4); // 未初始化的算法版本。
                    break;
                case 5:
                    bytes[bytes.Length - 1] = 255; // 未定义的拒绝原因。
                    break;
            }

            Assert.That(SimulationHandshakeCodec.TryDeserialize(new ArraySegment<byte>(bytes),
                out _, out SimulationWorldIdentity decoded, out _), Is.False);
            Assert.That(decoded, Is.EqualTo(default(SimulationWorldIdentity)));
        }

        [Test]
        public void InvalidSerializationAndTruncationAreRejected()
        {
            var identity = new SimulationWorldIdentity(1, 2, 3);
            byte[] bytes = new byte[SimulationHandshakeCodec.MaximumPacketSize];
            Assert.That(SimulationHandshakeCodec.TrySerialize(NetworkMessageType.SimulationHello, 0, identity,
                SimulationCompatibility.Compatible, new ArraySegment<byte>(bytes), out _), Is.False);
            Assert.That(SimulationHandshakeCodec.TrySerialize(NetworkMessageType.SimulationReady, 1, identity,
                SimulationCompatibility.WorldMismatch, new ArraySegment<byte>(bytes), out _), Is.False);
            Assert.That(SimulationHandshakeCodec.TrySerialize(NetworkMessageType.SimulationHello, 1, default,
                SimulationCompatibility.Compatible, new ArraySegment<byte>(bytes), out _), Is.False);
            Assert.That(SimulationHandshakeCodec.TrySerialize(NetworkMessageType.SimulationHello, 1, identity,
                SimulationCompatibility.Compatible, default, out _), Is.False);
            var valid = SimulationAdmissionTests.Handshake(NetworkMessageType.SimulationHello, 1, identity);
            Assert.That(SimulationHandshakeCodec.TryDeserialize(
                new ArraySegment<byte>(valid.Array, valid.Offset, valid.Count - 1), out _, out _, out _), Is.False);
        }

        [Test]
        public void MultipleMismatchesUseStablePriority()
        {
            var expected = new SimulationWorldIdentity(1, 2, 3);
            Assert.That(SimulationHandshakeCodec.Compare(expected, new SimulationWorldIdentity(2, 4, 5)),
                Is.EqualTo(SimulationCompatibility.AlgorithmMismatch));
            Assert.That(SimulationHandshakeCodec.Compare(expected, new SimulationWorldIdentity(1, 4, 5)),
                Is.EqualTo(SimulationCompatibility.WorldMismatch));
            Assert.That(SimulationHandshakeCodec.Compare(expected, new SimulationWorldIdentity(1, 2, 5)),
                Is.EqualTo(SimulationCompatibility.ConfigurationMismatch));
        }
    }
}

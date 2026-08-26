using System;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Protocol;
using AFPS.Simulation.Characters;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class InputCommandBatchCodecTests
    {
        private const float AxisQuantizationTolerance = 1f / 127f + 0.00001f;

        [Test]
        public void Batch_RoundTripsTicksInputsButtonsAndSequence()
        {
            PlayerInputCommand[] source =
            {
                new PlayerInputCommand { Tick = 100, MoveX = 1f, MoveY = -1f, JumpPressed = true },
                new PlayerInputCommand { Tick = 101, MoveX = 0.25f, MoveY = -0.5f, JumpPressed = false },
                new PlayerInputCommand { Tick = 102, MoveX = 0f, MoveY = 0.75f, JumpPressed = true }
            };

            InputCommandBatch sourceBatch = new InputCommandBatch(new ArraySegment<PlayerInputCommand>(source));
            byte[] packet = new byte[InputCommandBatchCodec.GetPacketSize(source.Length)];
            Assert.That(InputCommandBatchCodec.TrySerialize(sourceBatch, 55, new ArraySegment<byte>(packet), out int bytesWritten), Is.True);
            Assert.That(bytesWritten, Is.EqualTo(packet.Length));

            PlayerInputCommand[] decoded = new PlayerInputCommand[5];
            Assert.That(InputCommandBatchCodec.TryDeserialize(new ArraySegment<byte>(packet), decoded, 1, out PacketHeader header, out InputCommandBatch decodedBatch), Is.True);
            Assert.That(header.Sequence, Is.EqualTo(55));
            Assert.That(decodedBatch.FirstTick, Is.EqualTo(100));
            Assert.That(decodedBatch.LastTick, Is.EqualTo(102));
            Assert.That(decodedBatch.CommandCount, Is.EqualTo(3));

            for (int i = 0; i < source.Length; i++)
            {
                PlayerInputCommand actual = decoded[i + 1];
                Assert.That(actual.Tick, Is.EqualTo(source[i].Tick));
                Assert.That(actual.MoveX, Is.EqualTo(source[i].MoveX).Within(AxisQuantizationTolerance));
                Assert.That(actual.MoveY, Is.EqualTo(source[i].MoveY).Within(AxisQuantizationTolerance));
                Assert.That(actual.JumpPressed, Is.EqualTo(source[i].JumpPressed));
            }
        }

        [Test]
        public void Batch_UsesDocumentedLittleEndianCompactLayout()
        {
            PlayerInputCommand[] commands = { new PlayerInputCommand { Tick = 0x01020304, MoveX = 1f, MoveY = -1f, JumpPressed = true } };
            byte[] packet = new byte[InputCommandBatchCodec.GetPacketSize(1)];
            Assert.That(InputCommandBatchCodec.TrySerialize(new InputCommandBatch(new ArraySegment<PlayerInputCommand>(commands)), 0x11223344, new ArraySegment<byte>(packet), out _), Is.True);

            Assert.That(packet[0], Is.EqualTo((byte)'A'));
            Assert.That(packet[1], Is.EqualTo((byte)'F'));
            Assert.That(packet[2], Is.EqualTo((byte)'P'));
            Assert.That(packet[3], Is.EqualTo((byte)'S'));
            Assert.That(packet[8], Is.EqualTo(0x44));
            Assert.That(packet[9], Is.EqualTo(0x33));
            Assert.That(packet[10], Is.EqualTo(0x22));
            Assert.That(packet[11], Is.EqualTo(0x11));
            Assert.That(packet[12], Is.EqualTo(0x04));
            Assert.That(packet[13], Is.EqualTo(0x03));
            Assert.That(packet[14], Is.EqualTo(0x02));
            Assert.That(packet[15], Is.EqualTo(0x01));
            Assert.That(packet[16], Is.EqualTo(1));
            Assert.That(packet[17], Is.EqualTo(127));
            Assert.That(packet[18], Is.EqualTo(129));
            Assert.That(packet[19], Is.EqualTo(1));
        }

        [Test]
        public void Serialize_RejectsNonConsecutiveTicksAndSmallDestination()
        {
            PlayerInputCommand[] commands =
            {
                new PlayerInputCommand { Tick = 10 },
                new PlayerInputCommand { Tick = 12 }
            };
            InputCommandBatch batch = new InputCommandBatch(new ArraySegment<PlayerInputCommand>(commands));

            byte[] validSize = new byte[InputCommandBatchCodec.GetPacketSize(commands.Length)];
            Assert.That(InputCommandBatchCodec.TrySerialize(batch, 1, new ArraySegment<byte>(validSize), out _), Is.False);

            commands[1].Tick = 11;
            byte[] tooSmall = new byte[validSize.Length - 1];
            Assert.That(InputCommandBatchCodec.TrySerialize(batch, 1, new ArraySegment<byte>(tooSmall), out _), Is.False);
        }

        [Test]
        public void Deserialize_RejectsZeroCountAndSmallCommandBuffer()
        {
            PlayerInputCommand[] source = { new PlayerInputCommand { Tick = 8 }, new PlayerInputCommand { Tick = 9 } };
            byte[] packet = new byte[InputCommandBatchCodec.GetPacketSize(source.Length)];
            Assert.That(InputCommandBatchCodec.TrySerialize(new InputCommandBatch(new ArraySegment<PlayerInputCommand>(source)), 1, new ArraySegment<byte>(packet), out _), Is.True);

            PlayerInputCommand[] tooSmall = new PlayerInputCommand[1];
            Assert.That(InputCommandBatchCodec.TryDeserialize(new ArraySegment<byte>(packet), tooSmall, 0, out _, out _), Is.False);

            packet[PacketHeader.Size + 4] = 0;
            Assert.That(InputCommandBatchCodec.TryDeserialize(new ArraySegment<byte>(packet), new PlayerInputCommand[2], 0, out _, out _), Is.False);
        }

        [Test]
        public void Batch_ClampsInvalidOrOutOfRangeAxes()
        {
            PlayerInputCommand[] source = { new PlayerInputCommand { Tick = 1, MoveX = float.NaN, MoveY = 5f } };
            byte[] packet = new byte[InputCommandBatchCodec.GetPacketSize(1)];
            Assert.That(InputCommandBatchCodec.TrySerialize(new InputCommandBatch(new ArraySegment<PlayerInputCommand>(source)), 1, new ArraySegment<byte>(packet), out _), Is.True);

            PlayerInputCommand[] decoded = new PlayerInputCommand[1];
            Assert.That(InputCommandBatchCodec.TryDeserialize(new ArraySegment<byte>(packet), decoded, 0, out _, out _), Is.True);
            Assert.That(decoded[0].MoveX, Is.EqualTo(0f));
            Assert.That(decoded[0].MoveY, Is.EqualTo(1f));
        }

        [Test]
        public void Canonicalize_MatchesValueReceivedByServer()
        {
            PlayerInputCommand source = new PlayerInputCommand { Tick = 50, MoveX = 0.1234f, MoveY = -0.6789f, JumpPressed = true };
            PlayerInputCommand canonical = InputCommandBatchCodec.Canonicalize(source);
            PlayerInputCommand[] sourceArray = { source };
            byte[] packet = new byte[InputCommandBatchCodec.GetPacketSize(1)];
            Assert.That(InputCommandBatchCodec.TrySerialize(new InputCommandBatch(new ArraySegment<PlayerInputCommand>(sourceArray)), 1, new ArraySegment<byte>(packet), out _), Is.True);

            PlayerInputCommand[] decoded = new PlayerInputCommand[1];
            Assert.That(InputCommandBatchCodec.TryDeserialize(new ArraySegment<byte>(packet), decoded, 0, out _, out _), Is.True);
            Assert.That(decoded[0].MoveX, Is.EqualTo(canonical.MoveX));
            Assert.That(decoded[0].MoveY, Is.EqualTo(canonical.MoveY));
            Assert.That(decoded[0].JumpPressed, Is.EqualTo(canonical.JumpPressed));
        }
    }
}

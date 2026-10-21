using System;
using System.Reflection;
using AFPS.Math;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Protocol;
using AFPS.Simulation.Characters;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class InputGridTests
    {
        [TestCase(-127, -65536)]
        [TestCase(-64, -33026)]
        [TestCase(-1, -516)]
        [TestCase(0, 0)]
        [TestCase(1, 516)]
        [TestCase(64, 33026)]
        [TestCase(127, 65536)]
        public void AxisGrid_HasGoldenRawAndExactEndpoints(int grid, int expected) => Assert.That(InputAxisGrid.ToFix(grid).Raw, Is.EqualTo(expected));

        [Test]
        public void EveryAxisGrid_RoundTripsThroughFixAndWireExactly()
        {
            for (int grid = -127; grid <= 127; grid++)
            {
                Assert.That(InputAxisGrid.FromFix(InputAxisGrid.ToFix(grid)), Is.EqualTo(grid));
                var command = new PlayerInputCommand { Tick = uint.MaxValue };
                Assert.That(command.TrySetEncodedControls((sbyte)grid, (sbyte)-grid, 35999, -8990), Is.True);
                var decoded = RoundTrip(command);
                Assert.That(decoded.MoveXFixed.Raw, Is.EqualTo(command.MoveXFixed.Raw));
                Assert.That(decoded.MoveYFixed.Raw, Is.EqualTo(command.MoveYFixed.Raw));
                Assert.That(decoded.LookYawFixedDegrees.Raw, Is.EqualTo(command.LookYawFixedDegrees.Raw));
                Assert.That(decoded.LookPitchFixedDegrees.Raw, Is.EqualTo(command.LookPitchFixedDegrees.Raw));
            }
        }

        [Test]
        public void AxisGrid_ClampsAndRoundsTiesAwayFromZero()
        {
            Assert.That(InputAxisGrid.FromFix(Fix.Half), Is.EqualTo(64));
            Assert.That(InputAxisGrid.FromFix(-Fix.Half), Is.EqualTo(-64));
            Assert.That(InputAxisGrid.FromFix(Fix.MinValue), Is.EqualTo(-127));
            Assert.That(InputAxisGrid.FromFix(Fix.MaxValue), Is.EqualTo(127));
            Assert.That(InputAxisGrid.ToFix(int.MinValue), Is.EqualTo(-Fix.One));
            Assert.That(InputAxisGrid.ToFix(int.MaxValue), Is.EqualTo(Fix.One));
            Assert.That(InputAxisGrid.FromFloat(float.NaN), Is.Zero);
            Assert.That(InputAxisGrid.FromFloat(float.PositiveInfinity), Is.EqualTo(127));
            Assert.That(InputAxisGrid.FromFloat(float.NegativeInfinity), Is.EqualTo(-127));
            Assert.That(InputAxisGrid.FromFloat(0.5f), Is.EqualTo(64));
            Assert.That(InputAxisGrid.FromFloat(-0.5f), Is.EqualTo(-64));
        }

        [Test]
        public void AngleGrid_AllCentidegreesRoundTripExactly()
        {
            for (int yaw = 0; yaw < InputAngleGrid.YawSteps; yaw++)
            {
                Assert.That(InputAngleGrid.YawFromFix(InputAngleGrid.YawToFix((ushort)yaw)), Is.EqualTo(yaw));
                Assert.That(new PlayerInputCommand { LookYaw = InputAngleGrid.YawToFix((ushort)yaw).ToFloat() }.YawCentidegrees, Is.EqualTo(yaw));
            }
            for (int pitch = -8990; pitch <= 8990; pitch++)
            {
                Assert.That(InputAngleGrid.PitchFromFix(InputAngleGrid.PitchToFix((short)pitch)), Is.EqualTo(pitch));
                Assert.That(new PlayerInputCommand { LookPitch = InputAngleGrid.PitchToFix((short)pitch).ToFloat() }.PitchCentidegrees, Is.EqualTo(pitch));
            }
            Assert.That(InputAngleGrid.YawToFix(1).Raw, Is.EqualTo(655));
            Assert.That(InputAngleGrid.PitchToFix(-1).Raw, Is.EqualTo(-655));
            Assert.That(InputAngleGrid.YawFromFix(Fix.FromInt(-10)), Is.EqualTo(35000));
            Assert.That(InputAngleGrid.YawFromFix(Fix.FromInt(360)), Is.Zero);
            Assert.That(InputAngleGrid.PitchFromFix(Fix.MaxValue), Is.EqualTo(8990));
            Assert.That(InputAngleGrid.PitchFromFix(Fix.MinValue), Is.EqualTo(-8990));
        }

        [Test]
        public void FloatBoundary_HandlesAngleTiesWrapAndInvalidValues()
        {
            Assert.That(InputAngleGrid.YawFromFloat(45.125f), Is.EqualTo(4513));
            Assert.That(InputAngleGrid.PitchFromFloat(-45.125f), Is.EqualTo(-4513));
            Assert.That(InputAngleGrid.YawFromFloat(359.999f), Is.Zero);
            Assert.That(InputAngleGrid.YawFromFloat(-0.001f), Is.Zero);
            Assert.That(InputAngleGrid.YawFromFloat(float.NaN), Is.Zero);
            Assert.That(InputAngleGrid.YawFromFloat(float.PositiveInfinity), Is.Zero);
            Assert.That(InputAngleGrid.PitchFromFloat(float.NegativeInfinity), Is.Zero);
            Assert.That(InputAngleGrid.PitchFromFloat(1000), Is.EqualTo(8990));
            Assert.Throws<ArgumentOutOfRangeException>(() => InputAngleGrid.YawToFix(36000));
            Assert.Throws<ArgumentOutOfRangeException>(() => InputAngleGrid.PitchToFix(8991));
        }

        [Test]
        public void InputCommand_StoresOnlyIntegerAndEventFields()
        {
            foreach (FieldInfo field in typeof(PlayerInputCommand).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                Assert.That(field.FieldType != typeof(float) && field.FieldType != typeof(double), Is.True, field.Name);
            var command = new PlayerInputCommand { MoveX = 0.1234f, MoveY = -0.6789f, LookYaw = 123.456f, LookPitch = -42.424f };
            Assert.That(command.MoveXGrid, Is.EqualTo(16));
            Assert.That(command.MoveYGrid, Is.EqualTo(-86));
            Assert.That(command.YawCentidegrees, Is.EqualTo(12346));
            Assert.That(command.PitchCentidegrees, Is.EqualTo(-4242));
        }

        [Test]
        public void FixedControls_AndLegacyBoundaryHaveSameEncodedValues()
        {
            var fixedCommand = new PlayerInputCommand { Tick = 1 };
            fixedCommand.SetFixedControls(Fix.Half, -Fix.One, Fix.FromInt(350), Fix.FromInt(-42));
            var legacyCommand = new PlayerInputCommand { Tick = 1, MoveX = 0.5f, MoveY = -1f, LookYaw = -10f, LookPitch = -42f };
            Assert.That(fixedCommand, Is.EqualTo(legacyCommand));
            Assert.That(RoundTrip(fixedCommand), Is.EqualTo(fixedCommand));
        }

        [Test]
        public void InvalidEncodedControls_DoNotChangeExistingCommand()
        {
            var command = new PlayerInputCommand { Tick = 9, MoveY = 1, LookYaw = 90 };
            PlayerInputCommand before = command;
            Assert.That(command.TrySetEncodedControls(-128, 0, 0, 0), Is.False);
            Assert.That(command.TrySetEncodedControls(0, -128, 0, 0), Is.False);
            Assert.That(command.TrySetEncodedControls(0, 0, 36000, 0), Is.False);
            Assert.That(command.TrySetEncodedControls(0, 0, 0, -8991), Is.False);
            Assert.That(command, Is.EqualTo(before));
        }

        [Test]
        public void MissingInputFallback_CopiesRawControlsAndClearsOneShotEvents()
        {
            for (int grid = -127; grid <= 127; grid++)
            {
                var source = new PlayerInputCommand { Tick = uint.MaxValue, JumpPressed = true, FirePressed = true, ShotSequence = uint.MaxValue, ShotServerTick = 456 };
                Assert.That(source.TrySetEncodedControls((sbyte)grid, (sbyte)-grid, 35999, -8990), Is.True);
                PlayerInputCommand repeated = source.CreateFallback(0, true), neutral = source.CreateFallback(1, false);
                Assert.That(repeated.Tick, Is.Zero);
                Assert.That(repeated.MoveXFixed, Is.EqualTo(source.MoveXFixed));
                Assert.That(repeated.MoveYFixed, Is.EqualTo(source.MoveYFixed));
                Assert.That(repeated.YawCentidegrees, Is.EqualTo(source.YawCentidegrees));
                Assert.That(neutral.PitchCentidegrees, Is.EqualTo(source.PitchCentidegrees));
                Assert.That(neutral.MoveXFixed.Raw, Is.Zero);
                Assert.That(neutral.MoveYFixed.Raw, Is.Zero);
                Assert.That(repeated.JumpPressed || repeated.FirePressed || neutral.JumpPressed || neutral.FirePressed, Is.False);
                Assert.That(repeated.ShotSequence | repeated.ShotServerTick | neutral.ShotSequence | neutral.ShotServerTick, Is.Zero);
                Assert.That(RoundTrip(repeated), Is.EqualTo(repeated));
                Assert.That(source.FirePressed && source.JumpPressed, Is.True);
            }
        }

        [TestCase(0, 128)]
        [TestCase(1, 128)]
        [TestCase(2, 128)]
        [TestCase(11, 1)]
        public void InvalidLaterCommand_DoesNotWriteDecodedPrefix(int fieldOffset, int value)
        {
            PlayerInputCommand[] source = { new PlayerInputCommand { Tick = 10, MoveY = 1 }, new PlayerInputCommand { Tick = 11, MoveY = -1 } };
            byte[] packet = Packet(source);
            packet[PacketHeader.Size + InputCommandBatchCodec.PayloadHeaderSize + InputCommandBatchCodec.BytesPerCommand + fieldOffset] = (byte)value;
            PlayerInputCommand[] output = { new PlayerInputCommand { Tick = 777 }, new PlayerInputCommand { Tick = 888 } };
            var before = (PlayerInputCommand[])output.Clone();
            Assert.That(InputCommandBatchCodec.TryDeserialize(new ArraySegment<byte>(packet), output, 0, out _, out InputCommandBatch batch), Is.False);
            CollectionAssert.AreEqual(before, output);
            Assert.That(batch.CommandCount, Is.Zero);
        }

        [TestCase(36000, 0)]
        [TestCase(65535, 0)]
        [TestCase(0, 8991)]
        [TestCase(0, -8991)]
        public void Wire_RejectsNonCanonicalAngleDomains(int yaw, int pitch)
        {
            byte[] packet = Packet(new[] { new PlayerInputCommand { Tick = 1 } });
            int start = PacketHeader.Size + InputCommandBatchCodec.PayloadHeaderSize;
            packet[start + 3] = unchecked((byte)yaw);
            packet[start + 4] = unchecked((byte)(yaw >> 8));
            packet[start + 5] = unchecked((byte)pitch);
            packet[start + 6] = unchecked((byte)(pitch >> 8));
            Assert.That(InputCommandBatchCodec.TryDeserialize(new ArraySegment<byte>(packet), new PlayerInputCommand[1], 0, out _, out _), Is.False);
        }

        [Test]
        public void Batch_MaximumCountWithTickWrapAndBufferOffsetRoundTrips()
        {
            var source = new PlayerInputCommand[InputCommandBatchCodec.MaxCommandCount];
            for (int i = 0; i < source.Length; i++) source[i] = new PlayerInputCommand { Tick = unchecked(uint.MaxValue - 15 + (uint)i), MoveX = i % 2 == 0 ? -1 : 1, FirePressed = true, ShotSequence = (uint)i + 1, ShotServerTick = uint.MaxValue };
            byte[] packet = Packet(source);
            var offsetPacket = new byte[packet.Length + 6];
            Array.Copy(packet, 0, offsetPacket, 3, packet.Length);
            var destination = new PlayerInputCommand[source.Length + 2];
            Assert.That(InputCommandBatchCodec.TryDeserialize(new ArraySegment<byte>(offsetPacket, 3, packet.Length), destination, 1, out _, out InputCommandBatch batch), Is.True);
            Assert.That(batch.FirstTick, Is.EqualTo(uint.MaxValue - 15));
            Assert.That(batch.LastTick, Is.EqualTo(15));
            for (int i = 0; i < source.Length; i++) Assert.That(destination[i + 1], Is.EqualTo(source[i]));
        }

        [Test]
        public void CompleteGridRawSequence_MatchesIndependentGoldenHash()
        {
            ulong hash = 14695981039346656037UL;
            for (int grid = -127; grid <= 127; grid++) AddRaw(ref hash, InputAxisGrid.ToFix(grid).Raw);
            for (int yaw = 0; yaw < 36000; yaw++) AddRaw(ref hash, InputAngleGrid.YawToFix((ushort)yaw).Raw);
            for (int pitch = -8990; pitch <= 8990; pitch++) AddRaw(ref hash, InputAngleGrid.PitchToFix((short)pitch).Raw);
            Assert.That(hash, Is.EqualTo(5458074785564823468UL));
        }

        private static void AddRaw(ref ulong hash, int raw)
        {
            uint bits = unchecked((uint)raw);
            for (int shift = 0; shift < 32; shift += 8) hash = unchecked((hash ^ ((bits >> shift) & 255u)) * 1099511628211UL);
        }

        private static byte[] Packet(PlayerInputCommand[] commands)
        {
            byte[] packet = new byte[InputCommandBatchCodec.GetPacketSize(commands.Length)];
            Assert.That(InputCommandBatchCodec.TrySerialize(new InputCommandBatch(new ArraySegment<PlayerInputCommand>(commands)), uint.MaxValue, new ArraySegment<byte>(packet), out int written), Is.True);
            Assert.That(written, Is.EqualTo(packet.Length));
            return packet;
        }

        private static PlayerInputCommand RoundTrip(PlayerInputCommand command)
        {
            byte[] packet = Packet(new[] { command });
            var output = new PlayerInputCommand[1];
            Assert.That(InputCommandBatchCodec.TryDeserialize(new ArraySegment<byte>(packet), output, 0, out _, out _), Is.True);
            return output[0];
        }
    }
}

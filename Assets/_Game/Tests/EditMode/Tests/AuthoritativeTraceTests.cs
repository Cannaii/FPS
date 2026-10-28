using System;
using System.IO;
using AFPS.Math;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Replay;
using AFPS.Simulation.Characters;
using AFPS.Simulation.World;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class AuthoritativeTraceTests
    {
        [Test]
        public void ContinuousSequence_UsesOneInitialSeedAndMatchesEveryOutput()
        {
            byte[] trace = CreateTrace(512);
            var result = Replay(trace);
            Assert.That(result.Frames, Is.EqualTo(512));
            Assert.That(result.Resets, Is.EqualTo(1));
            Assert.That(result.EntityId, Is.EqualTo(7));
            Assert.That(result.Identity.Equals(Context().Identity), Is.True);
        }

        [Test]
        public void ExternalPositionBoundary_IsExplicitAndDoesNotSeedEveryFrame()
        {
            var result = Replay(CreateTrace(5, true));
            Assert.That(result.Frames, Is.EqualTo(5));
            Assert.That(result.Resets, Is.EqualTo(2));
        }

        [Test]
        public void WrongExpectedHash_FailsAtFirstDivergentEntityAndInputTick()
        {
            byte[] trace = CreateTrace(3);
            int firstStep = BodyOffset(trace) + 1 + AuthoritativePlayerStateCodec.PacketSize;
            int frameBytes = 1 + 4 + InputCommandBatchCodec.GetPacketSize(1) + 8;
            int secondHash = firstStep + frameBytes + 1 + 4 + InputCommandBatchCodec.GetPacketSize(1);
            trace[secondHash] ^= 1;
            var error = Assert.Throws<FormatException>(() => Replay(trace));
            Assert.That(error.Message, Does.Contain("Entity=7"));
            Assert.That(error.Message, Does.Contain("InputTick=2"));
        }

        [Test]
        public void EveryTruncation_IsRejectedIncludingMissingCompletionFooter()
        {
            byte[] trace = CreateTrace(2);
            for (int length = 0; length < trace.Length; length++)
            {
                byte[] prefix = new byte[length];
                Array.Copy(trace, prefix, length);
                Assert.Throws<FormatException>(() => Replay(prefix), "长度=" + length);
            }
        }

        [Test]
        public void CorruptFooterAndTrailingData_AreNotAcceptedAsPartialSuccess()
        {
            byte[] trace = CreateTrace(2);
            byte[] extra = new byte[trace.Length + 1];
            Array.Copy(trace, extra, trace.Length);
            Assert.Throws<FormatException>(() => Replay(extra));
            trace[trace.Length - 24] ^= 1;
            Assert.Throws<FormatException>(() => Replay(trace));
        }

        [TestCase(0)]
        [TestCase(4)]
        [TestCase(8)]
        [TestCase(12)]
        [TestCase(16)]
        [TestCase(20)]
        public void HeaderAndEmbeddedWorldCorruption_IsRejected(int offset)
        {
            byte[] trace = CreateTrace(2);
            trace[offset] ^= 1;
            if (offset == 12)
            {
                // 非零实体编号都是合法身份；此处检验保留编号0，不能把7改成6当作损坏。
                Array.Clear(trace, 12, 4);
            }
            Assert.Throws<FormatException>(() => Replay(trace));
        }

        [Test]
        public void ResetCannotJumpInputTimeline_AndUnknownRecordTypesAreRejected()
        {
            byte[] trace = CreateTrace(3, true);
            int frameBytes = 1 + 4 + InputCommandBatchCodec.GetPacketSize(1) + 8;
            int secondReset = BodyOffset(trace) + 1 + AuthoritativePlayerStateCodec.PacketSize + frameBytes;
            // Owner包负载第4字节是已处理输入Tick；解码状态会采用这个Tick，不能跳过一条输入。
            trace[secondReset + 1 + 12 + 4]++;
            Assert.Throws<FormatException>(() => Replay(trace));
            trace = CreateTrace(2);
            trace[BodyOffset(trace)] = 99;
            Assert.Throws<FormatException>(() => Replay(trace));
        }

        [Test]
        public void InputAndWorldTickWrap_PreserveContinuousOrder()
        {
            var result = Replay(CreateTrace(3, false, uint.MaxValue - 1, uint.MaxValue));
            Assert.That(result.Frames, Is.EqualTo(3));
            Assert.That(result.Resets, Is.EqualTo(1));
        }

        [Test]
        public void InvalidAppend_DoesNotWriteHalfAFrame_AndDisposedWriterRejectsInput()
        {
            var context = Context();
            var before = Initial(0);
            var input = new PlayerInputCommand { Tick = 1 };
            var after = PlayerSimulation.Simulate(before, input, context.Configuration,
                context.TickDeltaTime, context.CollisionWorld);
            using (var stream = new MemoryStream())
            {
                var writer = new AuthoritativeTraceWriter(stream, context, 7, true);
                var step = new AuthoritativeSimulationStep(7, 100, before, input, after);
                writer.Append(step);
                Assert.Throws<ArgumentException>(() => writer.Append(step));
                writer.Dispose();
                writer.Dispose();
                Assert.Throws<ObjectDisposedException>(() => writer.Append(step));
                Assert.That(Replay(stream.ToArray()).Frames, Is.EqualTo(1));
            }
            Assert.Throws<ArgumentException>(() => new AuthoritativeTraceWriter(null, context, 7));
        }

        [Test]
        public void EmptyTrace_IsNotAnAcceptanceRun()
        {
            using (var stream = new MemoryStream())
            {
                using (new AuthoritativeTraceWriter(stream, Context(), 7, true))
                {
                }
                Assert.Throws<FormatException>(() => Replay(stream.ToArray()));
            }
        }

        private static byte[] CreateTrace(int frames, bool reset = false, uint initialTick = 0, uint worldTick = 100)
        {
            var context = Context();
            var state = Initial(initialTick);
            using (var stream = new MemoryStream())
            {
                using (var writer = new AuthoritativeTraceWriter(stream, context, 7, true))
                {
                    for (int frame = 0; frame < frames; frame++)
                    {
                        if (reset && frame == 1)
                        {
                            state.PositionFixed += FixVec3.Right;
                        }
                        var input = new PlayerInputCommand { Tick = unchecked(state.Tick + 1), MoveY = 1f };
                        var result = PlayerSimulation.Simulate(state, input, context.Configuration,
                            context.TickDeltaTime, context.CollisionWorld);
                        writer.Append(new AuthoritativeSimulationStep(7, unchecked(worldTick + (uint)frame), state, input, result));
                        state = result;
                    }
                }
                return stream.ToArray();
            }
        }

        private static AuthoritativeTraceResult Replay(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes, false))
            {
                return AuthoritativeTraceReplay.Run(stream);
            }
        }

        private static int BodyOffset(byte[] trace)
        {
            using (var reader = new BinaryReader(new MemoryStream(trace, false)))
            {
                reader.BaseStream.Position = 16;
                return 20 + reader.ReadInt32();
            }
        }

        private static PlayerState Initial(uint tick) => new PlayerState { Tick = tick, IsGrounded = true };

        private static SimulationWorldContext Context()
        {
            var world = new FixedWorld(null, new[]
            {
                new FixedWorldPlane(1, 0, FixedSurface.Solid | FixedSurface.Ground, FixedAxis.Y, Fix.Zero, true)
            });
            var configuration = new PlayerSimulationConfig(Fix.FromInt(6), Fix.FromInt(20), Fix.FromInt(20), Fix.FromInt(8));
            return new SimulationWorldContext(world, configuration, SimulationStep.Step50Hz);
        }
    }
}

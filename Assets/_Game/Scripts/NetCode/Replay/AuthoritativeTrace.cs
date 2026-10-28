using System;
using System.IO;
using System.Text;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Protocol;
using AFPS.Simulation.Characters;
using AFPS.Simulation.World;

namespace AFPS.NetCode.Replay
{
    public readonly struct AuthoritativeTraceResult
    {
        public readonly uint EntityId;
        public readonly uint Frames;
        public readonly uint Resets;
        public readonly ulong RollingHash;
        public readonly ulong FinalHash;
        public readonly SimulationWorldIdentity Identity;

        internal AuthoritativeTraceResult(uint entity, uint frames, uint resets, ulong rolling,
            ulong finalHash, SimulationWorldIdentity identity)
        {
            EntityId = entity;
            Frames = frames;
            Resets = resets;
            RollingHash = rolling;
            FinalHash = finalHash;
            Identity = identity;
        }
    }

    /// <summary>离线轨迹v1：世界包、显式状态边界、实际输入和逐步输出hash。记录失败不能改变权威模拟。</summary>
    public sealed class AuthoritativeTraceWriter : IDisposable
    {
        internal const uint Magic = 0x54504641; // ASCII AFPT，小端。
        internal const uint Version = 1;
        // 每实体最多记录20000次实际模拟推进；50Hz下约400秒，不把等待输入算成一帧。
        public const uint MaximumFrames = 20000;
        internal const byte ResetTag = 1;
        internal const byte StepTag = 2;
        internal const byte FooterTag = 255;
        internal static readonly int InputPacketSize = InputCommandBatchCodec.GetPacketSize(1);
        private readonly BinaryWriter writer;
        private readonly uint entityId;
        private readonly byte[] statePacket = new byte[AuthoritativePlayerStateCodec.PacketSize];
        private readonly byte[] inputPacket = new byte[InputPacketSize];
        private readonly PlayerInputCommand[] commands = new PlayerInputCommand[1];
        private PlayerState lastState;
        private uint lastWorldTick;
        private uint frames;
        private uint resets;
        private ulong rollingHash = 14695981039346656037UL;
        private bool disposed;

        public uint Frames => frames;

        public AuthoritativeTraceWriter(Stream destination, SimulationWorldContext context, uint entityId, bool leaveOpen = false)
        {
            if (destination == null || !destination.CanWrite || entityId == 0)
            {
                throw new ArgumentException("轨迹需要可写流和非零实体ID。");
            }
            byte[] package = SimulationWorldPackageCodec.Serialize(context);
            writer = new BinaryWriter(destination, Encoding.UTF8, leaveOpen);
            this.entityId = entityId;
            writer.Write(Magic);
            writer.Write(Version);
            writer.Write((uint)PacketHeader.CurrentProtocolVersion);
            writer.Write(entityId);
            writer.Write(package.Length);
            writer.Write(package);
        }

        public void Append(in AuthoritativeSimulationStep step)
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(AuthoritativeTraceWriter));
            }
            if (frames >= MaximumFrames || step.EntityId != entityId
                || step.Input.Tick != unchecked(step.Previous.Tick + 1) || step.Result.Tick != step.Input.Tick
                || (frames != 0 && (!SequenceMath.IsNewer(step.WorldTick, lastWorldTick)
                    || step.Previous.Tick != lastState.Tick)))
            {
                throw new ArgumentException("轨迹步骤的实体、输入或世界时钟不连续，或超过记录上限。");
            }
            commands[0] = step.Input;
            if (!InputCommandBatchCodec.TrySerialize(new InputCommandBatch(new ArraySegment<PlayerInputCommand>(commands)),
                step.WorldTick, new ArraySegment<byte>(inputPacket), out _))
            {
                throw new FormatException("权威输入不能编码为规范协议。");
            }
            bool reset = frames == 0 || !step.Previous.RawEquals(lastState);
            // 死亡/重生等发生在移动模拟之外，显式写边界；普通连续步骤只保存输入和输出hash。
            if (reset)
            {
                var initial = new AuthoritativePlayerState(step.WorldTick, step.Previous.Tick, step.Previous);
                if (!AuthoritativePlayerStateCodec.TrySerialize(initial, step.WorldTick,
                    new ArraySegment<byte>(statePacket), out _))
                {
                    throw new FormatException("权威状态边界不能编码为Raw协议。");
                }
                writer.Write(ResetTag);
                writer.Write(statePacket);
                resets++;
            }
            ulong hash = PlayerStateHash.Compute(step.Result);
            writer.Write(StepTag);
            writer.Write(step.WorldTick);
            writer.Write(inputPacket);
            writer.Write(hash);
            rollingHash = Fold(rollingHash, hash);
            lastState = step.Result;
            lastWorldTick = step.WorldTick;
            frames++;
        }

        internal static ulong Fold(ulong rolling, ulong hash) => unchecked((rolling ^ hash) * 1099511628211UL);

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }
            disposed = true;
            // 必须有完成页脚；被强制杀死的进程留下的半份文件不能误判为验收通过。
            writer.Write(FooterTag);
            writer.Write(frames);
            writer.Write(resets);
            writer.Write(rollingHash);
            writer.Write(PlayerStateHash.Compute(lastState));
            writer.Dispose();
        }
    }

    public static class AuthoritativeTraceReplay
    {
        // 上限包含最大世界包和每步最多一个状态边界；单位字节，读入世界包前即校验。
        private const long MaximumTraceBytes = 96L * 1024 * 1024;

        public static AuthoritativeTraceResult Run(Stream source)
        {
            if (source == null || !source.CanRead || !source.CanSeek || source.Position != 0
                || source.Length > MaximumTraceBytes)
            {
                throw new ArgumentException("轨迹必须是从起点读取的有界可查找流。");
            }
            try
            {
                using (var reader = new BinaryReader(source, Encoding.UTF8, true))
                {
                    return Read(reader);
                }
            }
            catch (EndOfStreamException exception)
            {
                throw new FormatException("轨迹截断，缺少完整记录或完成页脚。", exception);
            }
        }

        private static AuthoritativeTraceResult Read(BinaryReader reader)
        {
            if (reader.ReadUInt32() != AuthoritativeTraceWriter.Magic
                || reader.ReadUInt32() != AuthoritativeTraceWriter.Version
                || reader.ReadUInt32() != PacketHeader.CurrentProtocolVersion)
            {
                throw new FormatException("轨迹魔数、格式或协议版本不兼容。");
            }
            uint entity = reader.ReadUInt32();
            int packageLength = reader.ReadInt32();
            if (entity == 0 || packageLength < SimulationWorldPackageCodec.HeaderSize + SimulationWorldPackageCodec.ConfigurationSize
                || packageLength > SimulationWorldPackageCodec.MaximumWorldBytes
                    + SimulationWorldPackageCodec.HeaderSize + SimulationWorldPackageCodec.ConfigurationSize
                || packageLength > reader.BaseStream.Length - reader.BaseStream.Position)
            {
                throw new FormatException("轨迹实体或世界包大小无效。");
            }
            byte[] package = ReadExact(reader, packageLength);
            if (!SimulationWorldPackageCodec.TryDeserialize(new ArraySegment<byte>(package), out var context, out var error))
            {
                throw new FormatException("轨迹世界包无效：" + error);
            }
            PlayerState state = default;
            var commands = new PlayerInputCommand[1];
            uint frames = 0;
            uint resets = 0;
            uint lastWorldTick = 0;
            bool hasState = false;
            bool needsStep = false;
            ulong rolling = 14695981039346656037UL;
            while (true)
            {
                byte tag = reader.ReadByte();
                if (tag == AuthoritativeTraceWriter.ResetTag)
                {
                    byte[] bytes = ReadExact(reader, AuthoritativePlayerStateCodec.PacketSize);
                    if (needsStep || resets >= AuthoritativeTraceWriter.MaximumFrames
                        || !AuthoritativePlayerStateCodec.TryDeserialize(new ArraySegment<byte>(bytes), out _, out var initial)
                        || (hasState && initial.State.Tick != state.Tick))
                    {
                        throw new FormatException("轨迹状态边界不连续。");
                    }
                    state = initial.State;
                    hasState = true;
                    needsStep = true;
                    resets++;
                }
                else if (tag == AuthoritativeTraceWriter.StepTag)
                {
                    uint worldTick = reader.ReadUInt32();
                    byte[] packet = ReadExact(reader, AuthoritativeTraceWriter.InputPacketSize);
                    ulong expected = reader.ReadUInt64();
                    if (!hasState || frames >= AuthoritativeTraceWriter.MaximumFrames
                        || (frames != 0 && !SequenceMath.IsNewer(worldTick, lastWorldTick))
                        || !InputCommandBatchCodec.TryDeserialize(new ArraySegment<byte>(packet), commands, 0, out _, out var batch)
                        || batch.CommandCount != 1 || commands[0].Tick != unchecked(state.Tick + 1))
                    {
                        throw new FormatException("轨迹输入或世界Tick不连续。");
                    }
                    // 连续推进自己的状态，只在显式边界播种；绝不把每帧期望状态喂回模拟掩盖误差。
                    state = PlayerSimulation.Simulate(state, commands[0], context.Configuration,
                        context.TickDeltaTime, context.CollisionWorld);
                    ulong actual = PlayerStateHash.Compute(state);
                    if (actual != expected)
                    {
                        throw new FormatException($"Replay mismatch: Entity={entity}; InputTick={state.Tick};"
                            + $" WorldTick={worldTick}; Expected={expected}; Actual={actual}");
                    }
                    rolling = AuthoritativeTraceWriter.Fold(rolling, actual);
                    lastWorldTick = worldTick;
                    frames++;
                    needsStep = false;
                }
                else if (tag == AuthoritativeTraceWriter.FooterTag)
                {
                    uint recordedFrames = reader.ReadUInt32();
                    uint recordedResets = reader.ReadUInt32();
                    ulong recordedRolling = reader.ReadUInt64();
                    ulong recordedFinal = reader.ReadUInt64();
                    if (frames == 0 || needsStep || recordedFrames != frames || recordedResets != resets
                        || recordedRolling != rolling || recordedFinal != PlayerStateHash.Compute(state)
                        || reader.BaseStream.Position != reader.BaseStream.Length)
                    {
                        throw new FormatException("轨迹完成页脚、计数、累计哈希或尾随数据无效。");
                    }
                    return new AuthoritativeTraceResult(entity, frames, resets, rolling, recordedFinal, context.Identity);
                }
                else
                {
                    throw new FormatException("未知轨迹记录类型。");
                }
            }
        }

        private static byte[] ReadExact(BinaryReader reader, int count)
        {
            byte[] bytes = reader.ReadBytes(count);
            if (bytes.Length != count)
            {
                throw new EndOfStreamException();
            }
            return bytes;
        }
    }
}

using System;
using System.IO;
using System.Reflection;
using AFPS.NetCode.Messages;
using AFPS.Simulation.Characters;

internal static class WorldPackageProbe
{
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length != 1)
            {
                throw new ArgumentException("用法：WorldPackageProbe <世界包.afsp>");
            }
            long size = new FileInfo(args[0]).Length;
            if (size > SimulationWorldPackageCodec.MaximumWorldBytes + SimulationWorldPackageCodec.HeaderSize
                + SimulationWorldPackageCodec.ConfigurationSize)
            {
                throw new FormatException("世界包超过允许的大小。");
            }
            byte[] bytes = File.ReadAllBytes(args[0]);
            if (!SimulationWorldPackageCodec.TryDeserialize(new ArraySegment<byte>(bytes), out var context, out var error))
            {
                throw new FormatException("独立世界包加载失败：" + error);
            }
            foreach (var reference in Assembly.GetExecutingAssembly().GetReferencedAssemblies())
            {
                if (reference.Name.StartsWith("Unity", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("引擎外装配产物不能引用Unity程序集。");
                }
            }
            var state = new PlayerState { PositionFixed = context.SessionSpatial.SpawnOrigin };
            var commands = new PlayerInputCommand[1];
            var decoded = new PlayerInputCommand[1];
            byte[] packet = new byte[InputCommandBatchCodec.GetPacketSize(1)];
            ulong rollingHash = 14695981039346656037UL;
            for (uint tick = 1; tick <= 512; tick++)
            {
                var command = new PlayerInputCommand
                {
                    Tick = tick,
                    SprintHeld = tick % 128 < 64,
                    JumpPressed = tick % 80 == 1,
                    DashPressed = tick % 125 == 0,
                    CrouchHeld = tick % 100 >= 85,
                    WallBouncePressed = tick % 67 == 0,
                    GrappleHeld = tick % 150 < 25
                };
                command.TrySetEncodedControls(0, 127, (ushort)((tick / 128 * 9000) % 36000), 0);
                commands[0] = command;
                var batch = new InputCommandBatch(new ArraySegment<PlayerInputCommand>(commands));
                if (!InputCommandBatchCodec.TrySerialize(batch, tick, new ArraySegment<byte>(packet), out int count)
                    || !InputCommandBatchCodec.TryDeserialize(new ArraySegment<byte>(packet, 0, count),
                        decoded, 0, out _, out _))
                {
                    throw new FormatException("权威输入协议回读失败。");
                }
                state = PlayerSimulation.Simulate(state, decoded[0], context.Configuration,
                    context.TickDeltaTime, context.CollisionWorld);
                rollingHash = unchecked((rollingHash ^ PlayerStateHash.Compute(state)) * 1099511628211UL);
            }
            Console.WriteLine($"EngineFree=True;Algorithm={context.Identity.AlgorithmVersion};"
                + $"World={context.Identity.WorldHash};Configuration={context.Identity.ConfigurationHash}");
            Console.WriteLine($"Ticks=512;FinalRawHash={PlayerStateHash.Compute(state)};RollingRawHash={rollingHash}");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }
}

using System;
using AFPS.Math;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Protocol;
using AFPS.Simulation.Characters;

internal static class InputCommandReplay
{
    private static int Main()
    {
        var random = new FixRandom(20261021);
        var source = new PlayerInputCommand[1];
        var decoded = new PlayerInputCommand[1];
        int packetSize = InputCommandBatchCodec.GetPacketSize(1);
        byte[] bytes = new byte[packetSize + 10];
        var segment = new ArraySegment<byte>(bytes, 5, packetSize);
        ulong hash = 14695981039346656037UL;
        for (int i = 0; i < 8192; i++)
        {
            var command = new PlayerInputCommand { Tick = unchecked(uint.MaxValue - 4096u + (uint)i), JumpPressed = (i % 11) == 0, FirePressed = (i % 7) == 0 };
            if (!command.TrySetEncodedControls((sbyte)random.NextInt(-127, 128), (sbyte)random.NextInt(-127, 128), (ushort)random.NextInt(0, 36000), (short)random.NextInt(-8990, 8991))) throw new InvalidOperationException("Invalid fixture input.");
            command.ShotSequence = command.FirePressed ? (uint)i + 1u : 0u;
            command.ShotServerTick = command.FirePressed ? random.NextUInt() : 0u;
            source[0] = command;
            uint sequence = random.NextUInt();
            if (!InputCommandBatchCodec.TrySerialize(new InputCommandBatch(new ArraySegment<PlayerInputCommand>(source)), sequence, segment, out int written) || written != packetSize) throw new InvalidOperationException("Input serialization failed.");
            if (!InputCommandBatchCodec.TryDeserialize(segment, decoded, 0, out PacketHeader header, out _) || header.Sequence != sequence) throw new InvalidOperationException("Input decode failed.");
            PlayerInputCommand result = decoded[0];
            if (!command.Equals(result)) throw new InvalidOperationException("Input raw state diverged.");
            // 只锁定输入 payload 和 Fix 视图，不让后续权威状态协议版本升级污染本项黄金值。
            for (int index = segment.Offset + PacketHeader.Size; index < segment.Offset + packetSize; index++) hash = unchecked((hash ^ bytes[index]) * 1099511628211UL);
            Add(ref hash, result.MoveXFixed.Raw); Add(ref hash, result.MoveYFixed.Raw);
            Add(ref hash, result.LookYawFixedDegrees.Raw); Add(ref hash, result.LookPitchFixedDegrees.Raw);
        }
        Console.WriteLine($"InputCommandReplay={hash}; Commands=8192; BytesPerCommand={InputCommandBatchCodec.BytesPerCommand}");
        if (hash != 17190514579825240793UL) throw new InvalidOperationException("Input command golden hash changed.");
        return 0;
    }

    private static void Add(ref ulong hash, int value)
    {
        uint bits = unchecked((uint)value);
        for (int shift = 0; shift < 32; shift += 8) hash = unchecked((hash ^ ((bits >> shift) & 255u)) * 1099511628211UL);
    }
}

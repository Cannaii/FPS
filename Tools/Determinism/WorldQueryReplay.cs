using System;
using AFPS.Math;
using AFPS.Simulation.World;

/// <summary>引擎外世界查询重放；同一个产物分别交给 Mono 和 CoreCLR 验证。</summary>
internal static class WorldQueryReplay
{
    private static FixVec3 V(int x, int y, int z) => new FixVec3(Fix.FromInt(x), Fix.FromInt(y), Fix.FromInt(z));

    private static int Main()
    {
        var world = new FixedWorld(new[] {
            new FixedWorldBox(3, 2, FixedSurface.Solid | FixedSurface.Wall | FixedSurface.Ladder, new FixedAabb(V(3, 0, -1), V(4, 4, 1))),
            new FixedWorldBox(1, 0, FixedSurface.Solid | FixedSurface.Ground, new FixedAabb(V(-5, -2, -5), V(5, -1, 5))),
            new FixedWorldBox(2, 1, FixedSurface.Wall | FixedSurface.Grapple, new FixedAabb(V(-2, 0, 3), V(2, 4, 4)))
        }, new[] { new FixedWorldPlane(4, 0, FixedSurface.Ground, FixedAxis.Y, Fix.Zero) });
        if (!FixedWorldCodec.TryDeserialize(new ArraySegment<byte>(world.CreateSnapshot()), out FixedWorld restored, out FixedWorldReadError error))
            throw new InvalidOperationException("Snapshot read failed: " + error);
        ulong first = Replay(world), second = Replay(restored);
        Console.WriteLine($"WorldQueryReplay={first}; Samples=4096; WorldVersion={world.VersionHash}");
        if (first != second) throw new InvalidOperationException("Snapshot replay diverged.");
        if (first != 9579028200939387050UL) throw new InvalidOperationException("World query golden hash changed.");
        return 0;
    }

    private static ulong Replay(IWorldQuery world)
    {
        ulong hash = 14695981039346656037UL;
        Add(ref hash, unchecked((int)world.VersionHash));
        Add(ref hash, unchecked((int)(world.VersionHash >> 32)));
        var random = new FixRandom(20261021);
        for (int i = 0; i < 4096; i++)
        {
            FixVec3 origin = new FixVec3(Fix.FromRaw(random.NextInt(-500000, 500000)), Fix.FromRaw(random.NextInt(-500000, 500000)), Fix.FromRaw(random.NextInt(-500000, 500000)));
            FixVec3 direction = new FixVec3(Fix.FromRaw(random.NextInt(-65536, 65537)), Fix.FromRaw(random.NextInt(-65536, 65537)), Fix.FromRaw(random.NextInt(-65536, 65537)));
            AddHit(ref hash, world.Raycast(origin, direction, Fix.FromInt(20), ~0, (i & 1) == 0 ? FixedSurface.All : FixedSurface.Ladder | FixedSurface.Grapple));
            AddHit(ref hash, world.GroundCheck(origin, Fix.Half, Fix.One, Fix.FromInt(2)));
        }
        return hash;
    }

    private static void AddHit(ref ulong hash, in DeterministicHit hit)
    {
        Add(ref hash, hit.DidHit ? 1 : 0);
        Add(ref hash, unchecked((int)hit.ShapeId));
        Add(ref hash, hit.Layer);
        Add(ref hash, (int)hit.Surface);
        Add(ref hash, hit.Distance.Raw);
        Add(ref hash, hit.Point.X.Raw); Add(ref hash, hit.Point.Y.Raw); Add(ref hash, hit.Point.Z.Raw);
        Add(ref hash, hit.Normal.X.Raw); Add(ref hash, hit.Normal.Y.Raw); Add(ref hash, hit.Normal.Z.Raw);
        Add(ref hash, hit.StartedInside ? 1 : 0);
    }

    private static void Add(ref ulong hash, int value)
    {
        uint bits = unchecked((uint)value);
        for (int shift = 0; shift < 32; shift += 8) hash = unchecked((hash ^ ((bits >> shift) & 255u)) * 1099511628211UL);
    }
}

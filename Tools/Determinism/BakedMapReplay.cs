using System;
using AFPS.Simulation.World;
using AFPS.Tests.EditMode;

internal static class BakedMapReplay
{
    private static int Main(string[] args)
    {
        byte[] bytes=System.IO.File.ReadAllBytes(args.Length>0 ? args[0] : "Assets/_Game/Worlds/SampleScene.bytes");
        if(!FixedWorldCodec.TryDeserialize(new ArraySegment<byte>(bytes),out var world,out var error)) throw new Exception("World snapshot failed: "+error);
        ulong hash=DeterministicMapReplayScenario.Run(world,out int contacts);
        Console.WriteLine($"BakedMapReplay={hash}; Sweeps=4096; Ticks=8192; Contacts={contacts}; World={world.VersionHash}");
        if(hash!=7359184500043852174UL) throw new Exception("Regression fingerprint changed");
        return 0;
    }
}

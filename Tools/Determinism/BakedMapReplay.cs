using System;
using AFPS.Simulation.Characters;
using AFPS.Tests.EditMode;

internal static class BakedMapReplay
{
    private static int Main(string[] args)
    {
        byte[] bytes = System.IO.File.ReadAllBytes(args.Length > 0 ? args[0] : "Assets/_Game/Worlds/SampleScene.bytes");
        var context = SimulationWorldContext.Load(
            new ArraySegment<byte>(bytes), DeterministicMapReplayScenario.CreateConfiguration(), SimulationStep.Step50Hz);
        ulong hash = DeterministicMapReplayScenario.Run(context, out int contacts);
        Console.WriteLine($"BakedMapReplay={hash}; Sweeps=4096; Ticks=8192; Contacts={contacts}; World={context.Identity.WorldHash}");
        Console.WriteLine($"Algorithm={context.Identity.AlgorithmVersion}; Configuration={context.Identity.ConfigurationHash}");
        if (hash != 7359184500043852174UL)
        {
            throw new Exception("Regression fingerprint changed");
        }

        if (context.Identity.AlgorithmVersion != 3 || context.Identity.ConfigurationHash != 2351933728679746851UL)
        {
            throw new Exception("World compatibility fingerprint changed");
        }

        return 0;
    }
}

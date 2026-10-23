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
        ulong shotHash = DeterministicShotGenerationScenario.Run();
        Console.WriteLine($"AuthoritativeShots=4096; ShotRawHash={shotHash}");
        if (shotHash != DeterministicShotGenerationScenario.ExpectedHash)
        {
            throw new Exception("Shot generation fingerprint changed");
        }
        ulong combatHash = DeterministicCombatScenario.Run();
        Console.WriteLine($"CombatTicks=512; CombatRawHash={combatHash}");
        if (combatHash != DeterministicCombatScenario.ExpectedHash)
        {
            throw new Exception("Combat simulation fingerprint changed");
        }
        if (hash != 7359184500043852174UL)
        {
            throw new Exception("Regression fingerprint changed");
        }

        if (context.Identity.AlgorithmVersion != 11 || context.Identity.ConfigurationHash != 3605442539420278840UL)
        {
            throw new Exception("World compatibility fingerprint changed");
        }

        Console.WriteLine("ExplosionRawHash=" + AFPS.Tests.EditMode.DeterministicExplosionScenario.Run());
        return 0;
    }
}

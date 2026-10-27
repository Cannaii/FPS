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
        Console.WriteLine("JumpRawHash=" + DeterministicJumpScenario.Run());
        Console.WriteLine("WallContactRawHash=" + DeterministicContactScenario.Run());
        Console.WriteLine("WallAbilityRawHash=" + DeterministicWallScenario.Run());
        if (DeterministicWallScenario.Run() != DeterministicWallScenario.ExpectedHash)
        {
            throw new Exception("Wall ability fingerprint changed");
        }
        if (DeterministicContactScenario.Run() != DeterministicContactScenario.ExpectedHash)
        {
            throw new Exception("Wall contact fingerprint changed");
        }
        if (DeterministicJumpScenario.Run() != DeterministicJumpScenario.ExpectedHash)
        {
            throw new Exception("Jump simulation fingerprint changed");
        }
        if (combatHash != DeterministicCombatScenario.ExpectedHash)
        {
            throw new Exception("Combat simulation fingerprint changed");
        }
        if (hash != 5080428393001121162UL)
        {
            throw new Exception("Regression fingerprint changed");
        }

        if (context.Identity.AlgorithmVersion != 16 || context.Identity.ConfigurationHash != 5692065266917181766UL)
        {
            throw new Exception("World compatibility fingerprint changed");
        }

        Console.WriteLine("ExplosionRawHash=" + AFPS.Tests.EditMode.DeterministicExplosionScenario.Run());
        Console.WriteLine("SprintRawHash=" + AFPS.Tests.EditMode.DeterministicSprintScenario.Run());
        Console.WriteLine("DashRawHash=" + DeterministicDashScenario.Run());
        if (DeterministicDashScenario.Run() != DeterministicDashScenario.ExpectedHash)
        {
            throw new Exception("Dash simulation fingerprint changed");
        }
        if (DeterministicSprintScenario.Run() != DeterministicSprintScenario.ExpectedHash)
        {
            throw new Exception("Sprint simulation fingerprint changed");
        }
        return 0;
    }
}

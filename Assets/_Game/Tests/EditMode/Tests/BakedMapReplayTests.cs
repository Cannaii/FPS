using System;
using AFPS.Simulation.Characters;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class BakedMapReplayTests
    {
        [Test]
        public void ActualMap_EditorSimulationMatchesEngineFreeGoldenHash()
        {
            byte[] bytes = System.IO.File.ReadAllBytes("Assets/_Game/Worlds/SampleScene.bytes");
            var context = SimulationWorldContext.Load(
                new ArraySegment<byte>(bytes), DeterministicMapReplayScenario.CreateConfiguration(), SimulationStep.Step50Hz);
            ulong hash = DeterministicMapReplayScenario.Run(context, out int contacts);
            Assert.That(hash, Is.EqualTo(13933247832230064910UL));
            Assert.That(contacts, Is.GreaterThan(100));
        }
    }
}

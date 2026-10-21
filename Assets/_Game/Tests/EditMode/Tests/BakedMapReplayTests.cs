using System;
using AFPS.Simulation.World;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class BakedMapReplayTests
    {
        [Test]
        public void ActualMap_EditorSimulationMatchesEngineFreeGoldenHash()
        {
            byte[] bytes = System.IO.File.ReadAllBytes("Assets/_Game/Worlds/SampleScene.bytes");
            Assert.That(FixedWorldCodec.TryDeserialize(new ArraySegment<byte>(bytes), out var world, out _), Is.True);
            ulong hash = DeterministicMapReplayScenario.Run(world, out int contacts);
            Assert.That(hash, Is.EqualTo(7359184500043852174UL));
            Assert.That(contacts, Is.GreaterThan(100));
        }
    }
}

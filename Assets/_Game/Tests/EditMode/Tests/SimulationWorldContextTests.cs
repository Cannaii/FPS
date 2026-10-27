using System;
using AFPS.Math;
using AFPS.Simulation.Characters;
using AFPS.Simulation.Characters.Collision;
using AFPS.Simulation.World;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class SimulationWorldContextTests
    {
        public enum Setting
        {
            Speed,
            Acceleration,
            Gravity,
            JumpSpeed,
            Radius,
            Height,
            Skin,
            GroundProbe,
            StepHeight,
            Slope,
            SlideIterations,
            TickDeltaTime,
            LayerMask,
            SpawnX,
            SpawnY,
            SpawnZ,
            SpawnSpacing,
            InterestRadius
        }

        [TestCase(Setting.Speed)]
        [TestCase(Setting.Acceleration)]
        [TestCase(Setting.Gravity)]
        [TestCase(Setting.JumpSpeed)]
        [TestCase(Setting.Radius)]
        [TestCase(Setting.Height)]
        [TestCase(Setting.Skin)]
        [TestCase(Setting.GroundProbe)]
        [TestCase(Setting.StepHeight)]
        [TestCase(Setting.Slope)]
        [TestCase(Setting.SlideIterations)]
        [TestCase(Setting.TickDeltaTime)]
        [TestCase(Setting.LayerMask)]
        [TestCase(Setting.SpawnX)]
        [TestCase(Setting.SpawnY)]
        [TestCase(Setting.SpawnZ)]
        [TestCase(Setting.SpawnSpacing)]
        [TestCase(Setting.InterestRadius)]
        public void EverySimulationSettingChangesCompatibilityIdentity(Setting changed)
        {
            FixedWorld world = CreateWorld();
            var baseline = new SimulationWorldContext(world, CreateConfiguration(), SimulationStep.Step50Hz);
            Fix dt = changed == Setting.TickDeltaTime ? SimulationStep.Step50Hz + Fix.Epsilon : SimulationStep.Step50Hz;
            int mask = changed == Setting.LayerMask ? ~1 : ~0;
            var spatial = new SimulationSessionSpatialConfig(
                new FixVec3(Fix.FromRaw(changed == Setting.SpawnX ? 1 : 0),
                    Fix.FromRaw(changed == Setting.SpawnY ? 1 : 0),
                    Fix.FromRaw(changed == Setting.SpawnZ ? 1 : 0)),
                Fix.FromRaw(163840 + (changed == Setting.SpawnSpacing ? 1 : 0)),
                Fix.FromRaw(9830400 + (changed == Setting.InterestRadius ? 1 : 0)));
            var variant = new SimulationWorldContext(world, CreateConfiguration(changed), dt, mask, spatial);

            Assert.That(variant.Identity.WorldHash, Is.EqualTo(baseline.Identity.WorldHash));
            Assert.That(variant.Identity.ConfigurationHash, Is.Not.EqualTo(baseline.Identity.ConfigurationHash));
            Assert.That(variant.Identity.Equals(baseline.Identity), Is.False);
        }

        [Test]
        public void DefaultAndLayerEightMasksHaveAfcaGoldenFingerprints()
        {
            var configuration = new PlayerSimulationConfig(
                Fix.FromInt(6), Fix.FromInt(20), Fix.FromInt(20), Fix.FromInt(8));
            var defaultContext = new SimulationWorldContext(CreateWorld(), configuration, SimulationStep.Step50Hz);
            var sceneContext = new SimulationWorldContext(CreateWorld(), configuration, SimulationStep.Step50Hz, 256);
            Assert.That(defaultContext.Identity.AlgorithmVersion, Is.EqualTo(18));
            Assert.That(defaultContext.Identity.ConfigurationHash, Is.EqualTo(5429654801791142265UL));
            Assert.That(sceneContext.Identity.ConfigurationHash, Is.EqualTo(5836576493034833362UL));
        }

        [Test]
        public void SnapshotLoadRespectsSegmentAndOwnsItsGeometry()
        {
            FixedWorld world = CreateWorld();
            byte[] snapshot = world.CreateSnapshot();
            byte[] envelope = new byte[snapshot.Length + 7];
            Array.Copy(snapshot, 0, envelope, 3, snapshot.Length);
            var loaded = SimulationWorldContext.Load(
                new ArraySegment<byte>(envelope, 3, snapshot.Length), CreateConfiguration(), SimulationStep.Step50Hz);
            var direct = new SimulationWorldContext(world, CreateConfiguration(), SimulationStep.Step50Hz);

            Array.Clear(envelope, 0, envelope.Length);
            Assert.That(loaded.Identity, Is.EqualTo(direct.Identity));
            Assert.That(loaded.CollisionWorld.VersionHash, Is.EqualTo(world.VersionHash));
            Assert.That(loaded.CollisionWorld.CapsuleCast(
                new FixVec3(Fix.Zero, Fix.One, Fix.Zero), -FixVec3.Up, Fix.FromInt(2),
                loaded.Configuration.Collision, out FixedCharacterCollisionHit hit), Is.True);
            Assert.That(hit.Distance, Is.EqualTo(Fix.One));
        }

        [Test]
        public void GeometryAndAlgorithmChangesHaveSeparateCompatibilityFields()
        {
            var original = new SimulationWorldContext(CreateWorld(), CreateConfiguration(), SimulationStep.Step50Hz);
            var changed = new SimulationWorldContext(CreateWorld(Fix.One), CreateConfiguration(), SimulationStep.Step50Hz);
            var futureAlgorithm = new SimulationWorldIdentity(
                original.Identity.AlgorithmVersion + 1, original.Identity.WorldHash, original.Identity.ConfigurationHash);

            Assert.That(changed.Identity.WorldHash, Is.Not.EqualTo(original.Identity.WorldHash));
            Assert.That(changed.Identity.ConfigurationHash, Is.EqualTo(original.Identity.ConfigurationHash));
            Assert.That(changed.Identity.Equals(original.Identity), Is.False);
            Assert.That(futureAlgorithm.Equals(original.Identity), Is.False);
        }

        [Test]
        public void ExplicitEmptyLayerMaskDisablesCollisionWithoutChangingGeometry()
        {
            var context = new SimulationWorldContext(CreateWorld(), CreateConfiguration(), SimulationStep.Step50Hz, 0);
            Assert.That(context.CollisionWorld.CapsuleCast(
                new FixVec3(Fix.Zero, Fix.One, Fix.Zero), -FixVec3.Up, Fix.FromInt(2),
                context.Configuration.Collision, out _), Is.False);
            Assert.That(context.CollisionLayerMask, Is.Zero);
        }

        [Test]
        public void InvalidSnapshotFailsInsteadOfFallingBackToAnotherWorld()
        {
            Assert.Throws<FormatException>(() => SimulationWorldContext.Load(
                default, CreateConfiguration(), SimulationStep.Step50Hz));

            byte[] snapshot = CreateWorld().CreateSnapshot();
            snapshot[4] = 127; // 不认识的快照版本，不能按当前格式继续解释。
            Assert.Throws<FormatException>(() => SimulationWorldContext.Load(
                new ArraySegment<byte>(snapshot), CreateConfiguration(), SimulationStep.Step50Hz));
        }

        [Test]
        public void InvalidAssemblyParametersAreRejectedBeforeSimulation()
        {
            Assert.Throws<ArgumentNullException>(() => new SimulationWorldContext(
                null, CreateConfiguration(), SimulationStep.Step50Hz));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SimulationWorldContext(
                CreateWorld(), CreateConfiguration(), Fix.Zero));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SimulationWorldContext(
                CreateWorld(), default, SimulationStep.Step50Hz));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SimulationWorldIdentity(0, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SimulationWorldContext(
                CreateWorld(), CreateConfiguration(), SimulationStep.Step50Hz,
                sessionSpatial: default(SimulationSessionSpatialConfig)));
        }

        private static FixedWorld CreateWorld(Fix height = default)
        {
            return new FixedWorld(null, new[]
            {
                new FixedWorldPlane(1, 0, FixedSurface.Solid | FixedSurface.Ground, FixedAxis.Y, height, true)
            });
        }

        private static PlayerSimulationConfig CreateConfiguration(Setting? changed = null)
        {
            // 尺寸用米，速度用米/秒，加速度用米/秒²，坡度用度；每次只改变一项的最低 Raw。
            Fix Value(int raw, Setting setting) => Fix.FromRaw(raw + (changed == setting ? 1 : 0));
            var collision = new CharacterCollisionConfig(
                Value(32768, Setting.Radius),
                Value(131072, Setting.Height),
                Value(655, Setting.Skin),
                Value(6554, Setting.GroundProbe),
                Value(19661, Setting.StepHeight),
                Value(3276800, Setting.Slope),
                4 + (changed == Setting.SlideIterations ? 1 : 0));
            return new PlayerSimulationConfig(
                Value(393216, Setting.Speed),
                Value(1310720, Setting.Acceleration),
                Value(1310720, Setting.Gravity),
                Value(524288, Setting.JumpSpeed),
                collision);
        }
    }
}

using AFPS.Math;
using AFPS.NetCode.Weapons;
using AFPS.Simulation.Characters;
using AFPS.Simulation.Weapons;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class AuthoritativeShotGenerationTests
    {
        [TestCase(0, 0, 0, 1)]
        [TestCase(90, 1, 0, 0)]
        [TestCase(180, 0, 0, -1)]
        [TestCase(270, -1, 0, 0)]
        public void Aim_CardinalAxes(int yaw, int x, int y, int z)
        {
            Assert.That(WeaponAim.TryGetDirection(Fix.FromInt(yaw), Fix.Zero, out FixVec3 direction), Is.True);
            Assert.That(direction.X.Raw, Is.EqualTo(x * Fix.Scale).Within(2));
            Assert.That(direction.Y.Raw, Is.EqualTo(y * Fix.Scale).Within(2));
            Assert.That(direction.Z.Raw, Is.EqualTo(z * Fix.Scale).Within(2));
        }

        [TestCase(-1, 0)]
        [TestCase(23592960, 0)]
        [TestCase(0, 5891687)]
        [TestCase(0, -5891687)]
        public void Aim_RejectsNonCanonicalRawAngles(int yaw, int pitch)
        {
            Assert.That(WeaponAim.TryGetDirection(Fix.FromRaw(yaw), Fix.FromRaw(pitch), out FixVec3 direction), Is.False);
            Assert.That(direction, Is.EqualTo(FixVec3.Zero));
        }

        [TestCase(8990)]
        [TestCase(-8990)]
        public void Aim_IncludesPitchBoundaryAndUsesDownwardPositivePitch(int pitch)
        {
            Assert.That(WeaponAim.TryGetDirection(Fix.Zero, InputAngleGrid.PitchToFix((short)pitch), out FixVec3 direction), Is.True);
            Assert.That(System.Math.Sign(direction.Y.Raw), Is.EqualTo(-System.Math.Sign(pitch)));
            Assert.That(direction.Magnitude.Raw, Is.EqualTo(Fix.Scale).Within(2));
        }

        [Test]
        public void Aim_AgreesWithIndependentTrigonometryAcrossGrid()
        {
            // 双精度参考只用于测试方向误差，容差 6 Raw，覆盖 LUT 插值与定点乘除舍入。
            for (int index = 0; index < 4096; index++)
            {
                ushort yaw = (ushort)((index * 7919) % 36000);
                short pitch = (short)((index * 3571) % 17981 - 8990);
                Fix yawFixed = InputAngleGrid.YawToFix(yaw);
                Fix pitchFixed = InputAngleGrid.PitchToFix(pitch);
                Assert.That(WeaponAim.TryGetDirection(yawFixed, pitchFixed, out FixVec3 direction), Is.True);
                double yawRadians = yaw / 100d * System.Math.PI / 180d;
                double pitchRadians = pitch / 100d * System.Math.PI / 180d;
                Assert.That(direction.X.Raw, Is.EqualTo(System.Math.Sin(yawRadians) * System.Math.Cos(pitchRadians) * Fix.Scale).Within(6));
                Assert.That(direction.Y.Raw, Is.EqualTo(-System.Math.Sin(pitchRadians) * Fix.Scale).Within(6));
                Assert.That(direction.Z.Raw, Is.EqualTo(System.Math.Cos(yawRadians) * System.Math.Cos(pitchRadians) * Fix.Scale).Within(6));
                Assert.That(direction.Magnitude.Raw, Is.EqualTo(Fix.Scale).Within(2));
            }
        }

        [Test]
        public void Generation_PreservesLargeCoordinatesAndRuleRawWithoutFloatRoundTrip()
        {
            WeaponSimulationConfig config = new WeaponSimulationConfig(7, WeaponFireMode.Projectile,
                2, 5, Fix.FromRaw(1310721), Fix.MaxValue, Fix.FromRaw(655361), Fix.FromRaw(104858));
            PlayerState state = new PlayerState
            {
                PositionFixed = new FixVec3(Fix.FromRaw(655360001), Fix.FromRaw(655360003), Fix.MinValue),
                YawFixedDegrees = Fix.FromInt(90)
            };
            PlayerInputCommand command = Fire(42);
            command.ShotServerTick = 86;
            // 客户端请求朝向不参与裁决，只读取服务器当前状态。
            command.SetFixedControls(Fix.Zero, Fix.Zero, Fix.FromInt(180), Fix.FromInt(45));
            WeaponFireResult result = new ServerWeaponFireController(config).Process(100, 9, command, state);
            Assert.That(result.Accepted, Is.True);
            Assert.That(result.RemainingAmmo, Is.EqualTo(1));
            Assert.That(result.Shot.OriginFixed.X.Raw, Is.EqualTo(655360001));
            Assert.That(result.Shot.OriginFixed.Y.Raw, Is.EqualTo(655464861));
            Assert.That(result.Shot.OriginFixed.Z, Is.EqualTo(Fix.MinValue));
            Assert.That(result.Shot.DirectionFixed.X.Raw, Is.EqualTo(Fix.Scale).Within(2));
            Assert.That(result.Shot.DamageFixed, Is.EqualTo(config.DamageFixed));
            Assert.That(result.Shot.RangeFixed, Is.EqualTo(config.RangeFixed));
            Assert.That(result.Shot.ProjectileSpeedFixed, Is.EqualTo(config.ProjectileSpeedFixed));
            Assert.That(result.Shot.WeaponId, Is.EqualTo(7));
            Assert.That(result.Shot.FireMode, Is.EqualTo(WeaponFireMode.Projectile));
            Assert.That(result.Shot.RewindTick, Is.EqualTo(86));
        }

        [Test]
        public void Generation_RejectsOriginOverflowWithoutAmmoOrCooldownAndConsumesSequence()
        {
            ServerWeaponFireController controller = new ServerWeaponFireController(WeaponSimulationConfig.DefaultRifle);
            PlayerState state = new PlayerState { PositionFixed = new FixVec3(Fix.Zero, Fix.MaxValue, Fix.Zero) };
            Assert.That(controller.Process(10, 1, Fire(1), state).RejectReason, Is.EqualTo(WeaponFireRejectReason.InvalidOrigin));
            Assert.That(controller.Ammo, Is.EqualTo(30));
            state.PositionFixed = FixVec3.Zero;
            Assert.That(controller.Process(10, 1, Fire(1), state).RejectReason, Is.EqualTo(WeaponFireRejectReason.DuplicateOrOldSequence));
            Assert.That(controller.Process(10, 1, Fire(2), state).Accepted, Is.True);
            Assert.That(controller.Ammo, Is.EqualTo(29));
        }

        [Test]
        public void Generation_RejectsInvalidAimWithoutAmmoOrCooldown()
        {
            ServerWeaponFireController controller = new ServerWeaponFireController(WeaponSimulationConfig.DefaultRifle);
            PlayerState state = new PlayerState { PitchFixedDegrees = Fix.FromRaw(5891687) };
            WeaponFireResult rejected = controller.Process(10, 1, Fire(1), state);
            Assert.That(rejected.RejectReason, Is.EqualTo(WeaponFireRejectReason.InvalidAim));
            Assert.That(rejected.RemainingAmmo, Is.EqualTo(30));
            Assert.That(rejected.Shot.EntityId, Is.Zero);
            state.PitchFixedDegrees = Fix.Zero;
            Assert.That(controller.Process(10, 1, Fire(2), state).Accepted, Is.True);
        }

        [Test]
        public void Generation_IncludesMaximumOriginAndPreservesTickSequenceWrap()
        {
            WeaponSimulationConfig config = WeaponSimulationConfig.DefaultRifle;
            PlayerState state = new PlayerState
            {
                PositionFixed = new FixVec3(Fix.MinValue,
                    Fix.FromRaw(int.MaxValue - config.EyeHeightFixed.Raw), Fix.MaxValue)
            };
            ServerWeaponFireController controller = new ServerWeaponFireController(config);
            WeaponFireResult first = controller.Process(uint.MaxValue - 4, 1, Fire(uint.MaxValue), state);
            Assert.That(first.Accepted, Is.True);
            Assert.That(first.Shot.OriginFixed.Y, Is.EqualTo(Fix.MaxValue));
            Assert.That(first.Shot.RewindTick, Is.EqualTo(uint.MaxValue - 4));
            WeaponFireResult second = controller.Process(0, 1, Fire(1), state);
            Assert.That(second.Accepted, Is.True);
            Assert.That(second.Shot.RewindTick, Is.Zero);
        }

        [Test]
        public void Generation_GoldenRawDigest()
        {
            Assert.That(DeterministicShotGenerationScenario.Run(),
                Is.EqualTo(DeterministicShotGenerationScenario.ExpectedHash));
        }

        private static PlayerInputCommand Fire(uint sequence) => new PlayerInputCommand
        {
            FirePressed = true,
            ShotSequence = sequence
        };
    }
}

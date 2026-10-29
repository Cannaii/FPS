using System;
using AFPS.Math;
using AFPS.Simulation.World;
using AFPS.Simulation.Characters.Collision;
using AFPS.Simulation.Weapons;

namespace AFPS.Simulation.Characters
{
    /// <summary>绑定不可变世界、移动/空间/武器配置及碰撞后端，供预测、权威与引擎外重放共用。</summary>
    public sealed class SimulationWorldContext
    {
        public FixedWorld World { get; }
        public FixedCharacterCollisionWorld CollisionWorld { get; }
        /// <summary>移动与移动能力配置；武器规则由WeaponConfiguration独立提供。</summary>
        public PlayerMovementConfig MovementConfiguration { get; }
        public SimulationSessionSpatialConfig SessionSpatial { get; }
        public WeaponSimulationConfig WeaponConfiguration { get; }
        public WeaponSimulationConfig SecondaryWeaponConfiguration => WeaponCatalog.SecondaryFor(WeaponConfiguration);

        /// <summary>每个模拟 Tick 的秒数，Q16.16；50Hz 的规范值为 1311 Raw。</summary>
        public Fix TickDeltaTime { get; }

        /// <summary>移动碰撞层位掩码；零表示显式禁用碰撞，不会回退到其他后端。</summary>
        public int CollisionLayerMask { get; }

        public SimulationWorldIdentity Identity { get; }

        public SimulationWorldContext(
            FixedWorld world,
            in PlayerMovementConfig configuration,
            Fix tickDeltaTime,
            int collisionLayerMask = ~0,
            SimulationSessionSpatialConfig? sessionSpatial = null,
            WeaponSimulationConfig? weaponConfiguration = null)
        {
            World = world ?? throw new ArgumentNullException(nameof(world));
            if (tickDeltaTime.Raw <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tickDeltaTime));
            }

            // readonly struct 仍可通过 default 绕过构造校验；装配入口必须拒绝无效的胶囊配置。
            CharacterCollisionConfig collision = configuration.Collision;
            _ = new CharacterCollisionConfig(
                collision.RadiusFixed,
                collision.HeightFixed,
                collision.SkinWidthFixed,
                collision.GroundProbeDistanceFixed,
                collision.StepHeightFixed,
                collision.MaxSlopeAngleFixedDegrees,
                collision.MaxSlideIterations);

            MovementConfiguration = configuration;
            SessionSpatial = sessionSpatial ?? SimulationSessionSpatialConfig.Default;
            if (!SessionSpatial.IsValid)
            {
                throw new ArgumentOutOfRangeException(nameof(sessionSpatial));
            }
            WeaponConfiguration = weaponConfiguration ?? WeaponSimulationConfig.DefaultRifle;
            if (!WeaponConfiguration.IsValid)
            {
                throw new ArgumentException("武器配置无效。", nameof(weaponConfiguration));
            }
            TickDeltaTime = tickDeltaTime;
            CollisionLayerMask = collisionLayerMask;
            CollisionWorld = new FixedCharacterCollisionWorld(world, collisionLayerMask);
            Identity = new SimulationWorldIdentity(
                SimulationWorldIdentity.CurrentAlgorithmVersion,
                world.VersionHash,
                ComputeConfigurationHash(configuration, tickDeltaTime, collisionLayerMask,
                    SessionSpatial, WeaponConfiguration));
        }

        /// <summary>只接受完整规范快照；加载失败必须阻止装配，不能悄悄退回平地或 PhysX。</summary>
        public static SimulationWorldContext Load(
            ArraySegment<byte> snapshot,
            in PlayerMovementConfig configuration,
            Fix tickDeltaTime,
            int collisionLayerMask = ~0,
            SimulationSessionSpatialConfig? sessionSpatial = null,
            WeaponSimulationConfig? weaponConfiguration = null)
        {
            if (!FixedWorldCodec.TryDeserialize(snapshot, out FixedWorld world, out FixedWorldReadError error))
            {
                throw new FormatException("确定性世界快照加载失败：" + error);
            }

            return new SimulationWorldContext(world, configuration, tickDeltaTime,
                collisionLayerMask, sessionSpatial, weaponConfiguration);
        }

        private static ulong ComputeConfigurationHash(
            in PlayerMovementConfig configuration,
            Fix tickDeltaTime,
            int collisionLayerMask,
            in SimulationSessionSpatialConfig sessionSpatial,
            in WeaponSimulationConfig weapon)
        {
            // AFCD追加Combined钩索七项规则；固定顺序小端int32。
            // 新增会改变模拟结果的参数时必须纳入字段，并升级域版本及黄金值。
            ulong hash = 14695981039346656037UL;
            Add(ref hash, 0x44434641);
            Add(ref hash, tickDeltaTime.Raw);
            Add(ref hash, collisionLayerMask);
            Add(ref hash, configuration.MaxGroundSpeedFixed.Raw);
            Add(ref hash, configuration.GroundAccelerationFixed.Raw);
            Add(ref hash, configuration.GravityFixed.Raw);
            Add(ref hash, configuration.JumpSpeedFixed.Raw);
            Add(ref hash, configuration.Sprint.SpeedMultiplier.Raw);
            Add(ref hash, configuration.Sprint.DrainPerSecond.Raw);
            Add(ref hash, configuration.Sprint.RecoveryPerSecond.Raw);
            Add(ref hash, configuration.Sprint.RestartStamina.Raw);
            Add(ref hash, configuration.Dash.Speed.Raw);
            Add(ref hash, configuration.Dash.Duration.Raw);
            Add(ref hash, configuration.Dash.RechargeDuration.Raw);
            Add(ref hash, configuration.Dash.MaxCharges);
            Add(ref hash, configuration.Dash.StaminaCost.Raw);
            Add(ref hash, configuration.Jump.CoyoteDuration.Raw);
            Add(ref hash, configuration.Jump.ExtraAirJumps);
            Add(ref hash, configuration.Wall.PushSpeed.Raw);
            Add(ref hash, configuration.Wall.UpSpeed.Raw);
            Add(ref hash, configuration.Wall.MaximumBounceSpeed.Raw);
            Add(ref hash, configuration.Wall.Cooldown.Raw);
            Add(ref hash, configuration.Wall.StaminaCost.Raw);
            Add(ref hash, configuration.Stance.Height.Raw);
            Add(ref hash, configuration.Stance.SpeedMultiplier.Raw);
            Add(ref hash, configuration.Stance.SlideSpeed.Raw);
            Add(ref hash, configuration.Stance.SlideMinimumSpeed.Raw);
            Add(ref hash, configuration.Stance.SlideDuration.Raw);
            Add(ref hash, configuration.Stance.SlideDeceleration.Raw);
            Add(ref hash, configuration.Stance.SlideSteer.Raw);
            Add(ref hash, configuration.WallRun.Speed.Raw);
            Add(ref hash, configuration.WallRun.Acceleration.Raw);
            Add(ref hash, configuration.WallRun.Duration.Raw);
            Add(ref hash, configuration.WallRun.GravityMultiplier.Raw);
            Add(ref hash, configuration.WallRun.AdhesionSpeed.Raw);
            Add(ref hash, configuration.WallRun.MaximumFallSpeed.Raw);
            Add(ref hash, configuration.WallRun.DrainPerSecond.Raw);
            Add(ref hash, configuration.WallRun.ProbeDistance.Raw);
            Add(ref hash, configuration.WallRun.MinimumAlignment.Raw);
            Add(ref hash, configuration.Climb.Speed.Raw);
            Add(ref hash, configuration.Climb.Reach.Raw);
            Add(ref hash, configuration.Climb.ExitPushSpeed.Raw);
            Add(ref hash, configuration.Climb.ReattachDelay.Raw);
            Add(ref hash, configuration.Climb.FacingAlignment.Raw);
            Add(ref hash, configuration.Grapple.Range.Raw);
            Add(ref hash, configuration.Grapple.PullAcceleration.Raw);
            Add(ref hash, configuration.Grapple.ReelSpeed.Raw);
            Add(ref hash, configuration.Grapple.MaximumSpeed.Raw);
            Add(ref hash, configuration.Grapple.CameraInfluence.Raw);
            Add(ref hash, configuration.Grapple.BreakDistance.Raw);
            Add(ref hash, configuration.Grapple.OriginHeight.Raw);

            CharacterCollisionConfig collision = configuration.Collision;
            Add(ref hash, collision.RadiusFixed.Raw);
            Add(ref hash, collision.HeightFixed.Raw);
            Add(ref hash, collision.SkinWidthFixed.Raw);
            Add(ref hash, collision.GroundProbeDistanceFixed.Raw);
            Add(ref hash, collision.StepHeightFixed.Raw);
            Add(ref hash, collision.MaxSlopeAngleFixedDegrees.Raw);
            Add(ref hash, collision.MinimumGroundNormalYFixed.Raw);
            Add(ref hash, collision.MinimumMoveDistanceFixed.Raw);
            Add(ref hash, collision.MaxSlideIterations);
            Add(ref hash, sessionSpatial.SpawnOrigin.X.Raw);
            Add(ref hash, sessionSpatial.SpawnOrigin.Y.Raw);
            Add(ref hash, sessionSpatial.SpawnOrigin.Z.Raw);
            Add(ref hash, sessionSpatial.SpawnSpacing.Raw);
            Add(ref hash, sessionSpatial.InterestRadius.Raw);
            AddWeapon(ref hash, weapon);
            AddWeapon(ref hash, WeaponCatalog.SecondaryFor(weapon));
            Add(ref hash, unchecked((int)WeaponCatalog.EquipDurationTicks));
            Add(ref hash, WeaponCatalog.LauncherExplosionRadius.Raw);
            return hash;
        }

        private static void AddWeapon(ref ulong hash, in WeaponSimulationConfig weapon)
        {
            Add(ref hash, weapon.WeaponId);
            Add(ref hash, (int)weapon.FireMode);
            Add(ref hash, weapon.MagazineCapacity);
            Add(ref hash, unchecked((int)weapon.FireIntervalTicks));
            Add(ref hash, weapon.DamageFixed.Raw);
            Add(ref hash, weapon.RangeFixed.Raw);
            Add(ref hash, weapon.ProjectileSpeedFixed.Raw);
            Add(ref hash, weapon.EyeHeightFixed.Raw);
        }

        private static void Add(ref ulong hash, int value)
        {
            uint bits = unchecked((uint)value);
            for (int shift = 0; shift < 32; shift += 8)
            {
                hash = unchecked((hash ^ ((bits >> shift) & 255u)) * 1099511628211UL);
            }
        }
    }
}

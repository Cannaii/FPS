using System;
using AFPS.Math;
using AFPS.Simulation.World;
using AFPS.Simulation.Characters.Collision;

namespace AFPS.Simulation.Characters
{
    /// <summary>一次装配不可变世界、移动配置和碰撞后端，供预测、权威及引擎外重放共用。</summary>
    public sealed class SimulationWorldContext
    {
        public FixedWorld World { get; }
        public FixedCharacterCollisionWorld CollisionWorld { get; }
        public PlayerSimulationConfig Configuration { get; }
        public SimulationSessionSpatialConfig SessionSpatial { get; }

        /// <summary>每个模拟 Tick 的秒数，Q16.16；50Hz 的规范值为 1311 Raw。</summary>
        public Fix TickDeltaTime { get; }

        /// <summary>移动碰撞层位掩码；零表示显式禁用碰撞，不会回退到其他后端。</summary>
        public int CollisionLayerMask { get; }

        public SimulationWorldIdentity Identity { get; }

        public SimulationWorldContext(
            FixedWorld world,
            in PlayerSimulationConfig configuration,
            Fix tickDeltaTime,
            int collisionLayerMask = ~0,
            SimulationSessionSpatialConfig? sessionSpatial = null)
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

            Configuration = configuration;
            SessionSpatial = sessionSpatial ?? SimulationSessionSpatialConfig.Default;
            if (!SessionSpatial.IsValid)
            {
                throw new ArgumentOutOfRangeException(nameof(sessionSpatial));
            }
            TickDeltaTime = tickDeltaTime;
            CollisionLayerMask = collisionLayerMask;
            CollisionWorld = new FixedCharacterCollisionWorld(world, collisionLayerMask);
            Identity = new SimulationWorldIdentity(
                SimulationWorldIdentity.CurrentAlgorithmVersion,
                world.VersionHash,
                ComputeConfigurationHash(configuration, tickDeltaTime, collisionLayerMask, SessionSpatial));
        }

        /// <summary>只接受完整规范快照；加载失败必须阻止装配，不能悄悄退回平地或 PhysX。</summary>
        public static SimulationWorldContext Load(
            ArraySegment<byte> snapshot,
            in PlayerSimulationConfig configuration,
            Fix tickDeltaTime,
            int collisionLayerMask = ~0,
            SimulationSessionSpatialConfig? sessionSpatial = null)
        {
            if (!FixedWorldCodec.TryDeserialize(snapshot, out FixedWorld world, out FixedWorldReadError error))
            {
                throw new FormatException("确定性世界快照加载失败：" + error);
            }

            return new SimulationWorldContext(world, configuration, tickDeltaTime, collisionLayerMask, sessionSpatial);
        }

        private static ulong ComputeConfigurationHash(
            in PlayerSimulationConfig configuration,
            Fix tickDeltaTime,
            int collisionLayerMask,
            in SimulationSessionSpatialConfig sessionSpatial)
        {
            // AFC2 增加会话空间配置；固定顺序的小端 int32，不能使用浮点字符串或本机字节序。
            // 新增会改变模拟结果的参数时必须纳入字段，并升级域版本及黄金值。
            ulong hash = 14695981039346656037UL;
            Add(ref hash, 0x32434641);
            Add(ref hash, tickDeltaTime.Raw);
            Add(ref hash, collisionLayerMask);
            Add(ref hash, configuration.MaxGroundSpeedFixed.Raw);
            Add(ref hash, configuration.GroundAccelerationFixed.Raw);
            Add(ref hash, configuration.GravityFixed.Raw);
            Add(ref hash, configuration.JumpSpeedFixed.Raw);

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
            return hash;
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

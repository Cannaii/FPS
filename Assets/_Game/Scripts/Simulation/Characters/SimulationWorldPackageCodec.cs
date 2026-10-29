using System;
using System.IO;
using AFPS.Math;
using AFPS.Simulation.Characters.Collision;
using AFPS.Simulation.Weapons;
using AFPS.Simulation.World;

namespace AFPS.Simulation.Characters
{
    public enum SimulationWorldPackageError
    {
        None,
        InvalidLength,
        InvalidMagic,
        UnsupportedVersion,
        AlgorithmMismatch,
        ReservedBits,
        InvalidConfiguration,
        InvalidGeometry,
        IdentityMismatch
    }

    /// <summary>独立进程装配产物：规范FixedWorld字节与完整Raw构造配置，加载后重新计算身份，禁止退回默认配置。</summary>
    public static class SimulationWorldPackageCodec
    {
        public const int HeaderSize = 40;
        public const int ConfigurationWordCount = 70;
        public const int ConfigurationSize = ConfigurationWordCount * 4;
        public const int MaximumWorldBytes = 64 * 1024 * 1024;
        private const uint Magic = 0x50534641; // ASCII AFSP，小端。
        private const uint FormatVersion = 1;

        public static byte[] Serialize(SimulationWorldContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }
            byte[] geometry = context.World.CreateSnapshot();
            if (geometry.Length > MaximumWorldBytes)
            {
                throw new ArgumentOutOfRangeException(nameof(context), "独立世界包几何超过64MiB。");
            }
            int[] words = ConfigurationWords(context);
            if (words.Length != ConfigurationWordCount)
            {
                throw new InvalidOperationException("独立世界包配置字段数改变时必须升级格式版本。");
            }
            using (var stream = new MemoryStream(HeaderSize + ConfigurationSize + geometry.Length))
            using (var writer = new BinaryWriter(stream))
            {
                // BinaryWriter整数采用小端，不依赖主机字节序；Fix只写Q16.16 Raw，绝不转float。
                writer.Write(Magic);
                writer.Write(FormatVersion);
                writer.Write(context.Identity.AlgorithmVersion);
                writer.Write(context.Identity.WorldHash);
                writer.Write(context.Identity.ConfigurationHash);
                writer.Write((uint)ConfigurationSize);
                writer.Write((uint)geometry.Length);
                writer.Write(0u);
                foreach (int word in words)
                {
                    writer.Write(word);
                }
                writer.Write(geometry);
                return stream.ToArray();
            }
        }

        public static bool TryDeserialize(ArraySegment<byte> package, out SimulationWorldContext context,
            out SimulationWorldPackageError error)
        {
            context = null;
            error = SimulationWorldPackageError.InvalidLength;
            if (package.Array == null || package.Count < HeaderSize + ConfigurationSize + FixedWorldCodec.HeaderSize
                || package.Count > HeaderSize + ConfigurationSize + MaximumWorldBytes)
            {
                return false;
            }
            using (var stream = new MemoryStream(package.Array, package.Offset, package.Count, false))
            using (var reader = new BinaryReader(stream))
            {
                if (reader.ReadUInt32() != Magic)
                {
                    error = SimulationWorldPackageError.InvalidMagic;
                    return false;
                }
                if (reader.ReadUInt32() != FormatVersion)
                {
                    error = SimulationWorldPackageError.UnsupportedVersion;
                    return false;
                }
                uint algorithm = reader.ReadUInt32();
                if (algorithm != SimulationWorldIdentity.CurrentAlgorithmVersion)
                {
                    error = SimulationWorldPackageError.AlgorithmMismatch;
                    return false;
                }
                ulong worldHash = reader.ReadUInt64();
                ulong configurationHash = reader.ReadUInt64();
                uint configurationBytes = reader.ReadUInt32();
                uint worldBytes = reader.ReadUInt32();
                if (configurationBytes != ConfigurationSize || worldBytes < FixedWorldCodec.HeaderSize
                    || worldBytes > MaximumWorldBytes || worldBytes != package.Count - HeaderSize - ConfigurationSize)
                {
                    return false;
                }
                if (reader.ReadUInt32() != 0)
                {
                    error = SimulationWorldPackageError.ReservedBits;
                    return false;
                }
                if (!FixedWorldCodec.TryDeserialize(new ArraySegment<byte>(package.Array,
                        package.Offset + HeaderSize + ConfigurationSize, (int)worldBytes), out var world, out _))
                {
                    error = SimulationWorldPackageError.InvalidGeometry;
                    return false;
                }
                try
                {
                    var decoded = ReadConfiguration(reader, world);
                    if (decoded.Identity.WorldHash != worldHash || decoded.Identity.ConfigurationHash != configurationHash)
                    {
                        error = SimulationWorldPackageError.IdentityMismatch;
                        return false;
                    }
                    context = decoded;
                    error = SimulationWorldPackageError.None;
                    return true;
                }
                catch (ArgumentException)
                {
                    error = SimulationWorldPackageError.InvalidConfiguration;
                    return false;
                }
                catch (OverflowException)
                {
                    error = SimulationWorldPackageError.InvalidConfiguration;
                    return false;
                }
            }
        }

        private static SimulationWorldContext ReadConfiguration(BinaryReader reader, FixedWorld world)
        {
            // 顺序与ConfigurationWords一一对应；所有能力都显式传入，禁止漏字段后悄悄使用新默认值。
            var raw = new RawReader(reader);
            Fix step = raw.Fix();
            int layerMask = raw.Int();
            Fix speed = raw.Fix();
            Fix acceleration = raw.Fix();
            Fix gravity = raw.Fix();
            Fix jumpSpeed = raw.Fix();
            var sprint = new PlayerSprintConfig(raw.Fix(), raw.Fix(), raw.Fix(), raw.Fix());
            var dash = new PlayerDashConfig(raw.Fix(), raw.Fix(), raw.Fix(), raw.Int(), raw.Fix());
            var jump = new PlayerJumpConfig(raw.Fix(), raw.Int());
            var wall = new PlayerWallConfig(raw.Fix(), raw.Fix(), raw.Fix(), raw.Fix(), raw.Fix());
            var stance = new PlayerStanceConfig(raw.Fix(), raw.Fix(), raw.Fix(), raw.Fix(),
                raw.Fix(), raw.Fix(), raw.Fix());
            var wallRun = new PlayerWallRunConfig(raw.Fix(), raw.Fix(), raw.Fix(), raw.Fix(),
                raw.Fix(), raw.Fix(), raw.Fix(), raw.Fix(), raw.Fix());
            var climb = new PlayerClimbConfig(raw.Fix(), raw.Fix(), raw.Fix(), raw.Fix(), raw.Fix());
            var grapple = new PlayerGrappleConfig(raw.Fix(), raw.Fix(), raw.Fix(), raw.Fix(),
                raw.Fix(), raw.Fix(), raw.Fix());
            var collision = new CharacterCollisionConfig(raw.Fix(), raw.Fix(), raw.Fix(), raw.Fix(),
                raw.Fix(), raw.Fix(), raw.Int());
            var spatial = new SimulationSessionSpatialConfig(
                new FixVec3(raw.Fix(), raw.Fix(), raw.Fix()), raw.Fix(), raw.Fix());
            int weaponId = raw.Int();
            int fireMode = raw.Int();
            if (weaponId < 1 || weaponId > ushort.MaxValue
                || (fireMode != (int)WeaponFireMode.Hitscan && fireMode != (int)WeaponFireMode.Projectile))
            {
                throw new ArgumentOutOfRangeException(nameof(weaponId));
            }
            int magazine = raw.Int();
            uint interval = unchecked((uint)raw.Int());
            Fix damage = raw.Fix();
            Fix range = raw.Fix();
            Fix projectileSpeed = raw.Fix();
            Fix eyeHeight = raw.Fix();
            if (fireMode == (int)WeaponFireMode.Hitscan && projectileSpeed != AFPS.Math.Fix.Zero)
            {
                // 构造器会把Hitscan弹速归零；文件必须先拒绝非规范字节，不能让篡改被静默吞掉。
                throw new ArgumentOutOfRangeException(nameof(projectileSpeed));
            }
            var weapon = new WeaponSimulationConfig((ushort)weaponId, (WeaponFireMode)fireMode,
                magazine, interval, damage, range, projectileSpeed, eyeHeight);
            if (raw.Count != ConfigurationWordCount)
            {
                throw new InvalidOperationException("独立世界包读取字段数与版本不一致。");
            }
            var configuration = new PlayerMovementConfig(speed, acceleration, gravity, jumpSpeed,
                collision, sprint, dash, jump, wall, stance, wallRun, climb, grapple);
            // 坡度法线阈值、最小位移和副武器等派生值由当前算法重建，再用完整配置指纹校验。
            return new SimulationWorldContext(world, configuration, step, layerMask, spatial, weapon);
        }

        private sealed class RawReader
        {
            private readonly BinaryReader reader;
            internal int Count { get; private set; }

            internal RawReader(BinaryReader reader) => this.reader = reader;

            internal int Int()
            {
                Count++;
                return reader.ReadInt32();
            }

            internal Fix Fix() => AFPS.Math.Fix.FromRaw(Int());
        }

        private static int[] ConfigurationWords(SimulationWorldContext context)
        {
            var c = context.MovementConfiguration;
            var collision = c.Collision;
            var spatial = context.SessionSpatial;
            var weapon = context.WeaponConfiguration;
            return new[]
            {
                context.TickDeltaTime.Raw,
                context.CollisionLayerMask,
                c.MaxGroundSpeedFixed.Raw,
                c.GroundAccelerationFixed.Raw,
                c.GravityFixed.Raw,
                c.JumpSpeedFixed.Raw,
                c.Sprint.SpeedMultiplier.Raw,
                c.Sprint.DrainPerSecond.Raw,
                c.Sprint.RecoveryPerSecond.Raw,
                c.Sprint.RestartStamina.Raw,
                c.Dash.Speed.Raw,
                c.Dash.Duration.Raw,
                c.Dash.RechargeDuration.Raw,
                c.Dash.MaxCharges,
                c.Dash.StaminaCost.Raw,
                c.Jump.CoyoteDuration.Raw,
                c.Jump.ExtraAirJumps,
                c.Wall.PushSpeed.Raw,
                c.Wall.UpSpeed.Raw,
                c.Wall.MaximumBounceSpeed.Raw,
                c.Wall.Cooldown.Raw,
                c.Wall.StaminaCost.Raw,
                c.Stance.Height.Raw,
                c.Stance.SpeedMultiplier.Raw,
                c.Stance.SlideSpeed.Raw,
                c.Stance.SlideMinimumSpeed.Raw,
                c.Stance.SlideDuration.Raw,
                c.Stance.SlideDeceleration.Raw,
                c.Stance.SlideSteer.Raw,
                c.WallRun.Speed.Raw,
                c.WallRun.Acceleration.Raw,
                c.WallRun.Duration.Raw,
                c.WallRun.GravityMultiplier.Raw,
                c.WallRun.AdhesionSpeed.Raw,
                c.WallRun.MaximumFallSpeed.Raw,
                c.WallRun.DrainPerSecond.Raw,
                c.WallRun.ProbeDistance.Raw,
                c.WallRun.MinimumAlignment.Raw,
                c.Climb.Speed.Raw,
                c.Climb.Reach.Raw,
                c.Climb.ExitPushSpeed.Raw,
                c.Climb.ReattachDelay.Raw,
                c.Climb.FacingAlignment.Raw,
                c.Grapple.Range.Raw,
                c.Grapple.PullAcceleration.Raw,
                c.Grapple.ReelSpeed.Raw,
                c.Grapple.MaximumSpeed.Raw,
                c.Grapple.CameraInfluence.Raw,
                c.Grapple.BreakDistance.Raw,
                c.Grapple.OriginHeight.Raw,
                collision.RadiusFixed.Raw,
                collision.HeightFixed.Raw,
                collision.SkinWidthFixed.Raw,
                collision.GroundProbeDistanceFixed.Raw,
                collision.StepHeightFixed.Raw,
                collision.MaxSlopeAngleFixedDegrees.Raw,
                collision.MaxSlideIterations,
                spatial.SpawnOrigin.X.Raw,
                spatial.SpawnOrigin.Y.Raw,
                spatial.SpawnOrigin.Z.Raw,
                spatial.SpawnSpacing.Raw,
                spatial.InterestRadius.Raw,
                weapon.WeaponId,
                (int)weapon.FireMode,
                weapon.MagazineCapacity,
                unchecked((int)weapon.FireIntervalTicks),
                weapon.DamageFixed.Raw,
                weapon.RangeFixed.Raw,
                weapon.ProjectileSpeedFixed.Raw,
                weapon.EyeHeightFixed.Raw
            };
        }
    }
}

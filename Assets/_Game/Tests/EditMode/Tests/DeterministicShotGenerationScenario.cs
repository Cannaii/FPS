using AFPS.Math;
using AFPS.NetCode.Weapons;
using AFPS.Simulation.Characters;
using AFPS.Simulation.Weapons;

namespace AFPS.Tests.EditMode
{
    /// <summary>Unity、Mono、CoreCLR 共用的 4096 发射击重放；跨 Tick 回绕，按 Raw 累积黄金摘要。</summary>
    public static class DeterministicShotGenerationScenario
    {
        public const ulong ExpectedHash = 16074677897942787332UL;

        public static ulong Run()
        {
            ulong hash = 14695981039346656037UL;
            ServerWeaponFireController controller = new ServerWeaponFireController(WeaponSimulationConfig.DefaultRifle);
            for (int index = 0; index < 4096; index++)
            {
                PlayerState state = new PlayerState
                {
                    PositionFixed = new FixVec3(Fix.FromRaw(655360001 + index),
                        Fix.FromRaw(-index * 17), Fix.FromRaw(index * 8191)),
                    YawFixedDegrees = InputAngleGrid.YawToFix((ushort)((index * 7919) % 36000)),
                    PitchFixedDegrees = InputAngleGrid.PitchToFix((short)((index * 3571) % 17981 - 8990))
                };
                controller.Reload();
                uint tick = unchecked(uint.MaxValue - 100 + (uint)index * 5);
                WeaponFireResult result = controller.Process(tick, 7, Fire((uint)index + 1), state);
                if (!result.Accepted)
                {
                    throw new System.InvalidOperationException("Golden shot rejected: " + result.RejectReason);
                }
                AuthoritativeShot shot = result.Shot;
                // 固定小端 int32 顺序，包含元数据、坐标、方向和规则；不用平台 GetHashCode。
                int[] values =
                {
                    unchecked((int)shot.EntityId), unchecked((int)shot.ShotSequence),
                    unchecked((int)shot.ServerTick), unchecked((int)shot.RewindTick),
                    shot.WeaponId, (int)shot.FireMode, result.RemainingAmmo,
                    shot.OriginFixed.X.Raw, shot.OriginFixed.Y.Raw, shot.OriginFixed.Z.Raw,
                    shot.DirectionFixed.X.Raw, shot.DirectionFixed.Y.Raw, shot.DirectionFixed.Z.Raw,
                    shot.DamageFixed.Raw, shot.RangeFixed.Raw, shot.ProjectileSpeedFixed.Raw
                };
                foreach (int value in values)
                {
                    uint bits = unchecked((uint)value);
                    for (int shift = 0; shift < 32; shift += 8)
                    {
                        hash = unchecked((hash ^ (byte)(bits >> shift)) * 1099511628211UL);
                    }
                }
            }
            return hash;
        }

        private static PlayerInputCommand Fire(uint sequence) => new PlayerInputCommand
        {
            FirePressed = true,
            ShotSequence = sequence
        };
    }
}
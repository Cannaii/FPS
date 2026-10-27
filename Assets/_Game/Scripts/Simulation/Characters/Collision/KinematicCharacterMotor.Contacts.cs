using AFPS.Math;

namespace AFPS.Simulation.Characters.Collision
{
    public static partial class KinematicCharacterMotor
    {
        /// <summary>沿用原位移求解，再对最终位置收集接触；旧重载不额外查询几何。</summary>
        public static void Move(
            FixVec3 startPosition,
            FixVec3 startVelocity,
            Fix tickDeltaTime,
            in CharacterCollisionConfig config,
            IFixedCharacterCollisionWorld world,
            out FixVec3 position,
            out FixVec3 velocity,
            out bool isGrounded,
            out FixedCharacterContacts contacts)
        {
            Move(startPosition, startVelocity, tickDeltaTime, config, world,
                out position, out velocity, out isGrounded);
            contacts = CollectContacts(position, config, world);
        }

        public static FixedCharacterContacts CollectContacts(FixVec3 feetPosition,
            in CharacterCollisionConfig config, IFixedCharacterCollisionWorld world)
        {
            if (world == null)
            {
                throw new System.ArgumentNullException(nameof(world));
            }
            var result = default(FixedCharacterContacts);
            // 静止贴墙时没有扫掠阻挡，也必须保留触墙。查询范围复用已有皮肤厚度，
            // 零皮肤时至少查1 Raw，避免零长度查询退化。四向与最终位置只依赖规范整数数据。
            Fix range = FixMath.Max(Fix.Epsilon, config.SkinWidthFixed);
            ConsiderWall(feetPosition, FixVec3.Right, range, config, world, ref result);
            ConsiderWall(feetPosition, -FixVec3.Right, range, config, world, ref result);
            ConsiderWall(feetPosition, FixVec3.Forward, range, config, world, ref result);
            ConsiderWall(feetPosition, -FixVec3.Forward, range, config, world, ref result);
            return result;
        }

        private static void ConsiderWall(FixVec3 position, FixVec3 direction, Fix range,
            in CharacterCollisionConfig config, IFixedCharacterCollisionWorld world,
            ref FixedCharacterContacts current)
        {
            if (!world.CapsuleCast(position, direction, range, config, out var hit)
                || hit.Distance < Fix.Zero || hit.Distance > range
                || FixMath.Abs(hit.Normal.Y) >= config.MinimumGroundNormalYFixed
                || FixVec3.Dot(direction, hit.Normal) >= Fix.Zero)
            {
                return;
            }
            FixVec3 normal = new FixVec3(hit.Normal.X, Fix.Zero, hit.Normal.Z).Normalized();
            if (normal == FixVec3.Zero)
            {
                return;
            }
            // 地面、天花板和可行走坡面已排除；同距用法线XZ字典序裁决，
            // 不依赖遍历顺序或对象哈希，也不把已成功跨过的障碍当作当前墙。
            bool earlier = normal.X < current.WallNormal.X
                || (normal.X == current.WallNormal.X && normal.Z < current.WallNormal.Z);
            if (!current.HasWall || hit.Distance < current.WallDistance
                || (hit.Distance == current.WallDistance && earlier))
            {
                current = new FixedCharacterContacts(normal, hit.Distance);
            }
        }
    }
}

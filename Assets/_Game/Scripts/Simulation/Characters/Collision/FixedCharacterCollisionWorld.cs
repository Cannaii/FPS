using System;
using AFPS.Math;
using AFPS.Simulation.World;

namespace AFPS.Simulation.Characters.Collision
{
    /// <summary>预测、权威与重放共用的定点世界后端，不经过 Unity/PhysX 或浮点转换。</summary>
    public sealed class FixedCharacterCollisionWorld : IFixedCharacterCollisionWorld
    {
        private readonly FixedWorld world;
        private readonly int layerMask;
        public ulong VersionHash => world.VersionHash;

        public FixedCharacterCollisionWorld(FixedWorld world, int layerMask = ~0)
        {
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            this.layerMask = layerMask;
        }

        public bool CapsuleCast(
            FixVec3 feetPosition,
            FixVec3 direction,
            Fix distance,
            in CharacterCollisionConfig config,
            out FixedCharacterCollisionHit hit)
        {
            DeterministicHit result = world.CapsuleCastUnit(feetPosition, config.RadiusFixed, config.HeightFixed, direction, distance, layerMask);
            // 斜向位移的分量乘法会四舍五入；预留一 Raw，避免距离向下取整后又被分量舍入推进盒内。
            bool oblique = (direction.X.Raw != 0 && direction.Y.Raw != 0) || (direction.X.Raw != 0 && direction.Z.Raw != 0) || (direction.Y.Raw != 0 && direction.Z.Raw != 0);
            Fix safe = oblique ? FixMath.Max(Fix.Zero, result.Distance - Fix.Epsilon) : result.Distance;
            hit = result.DidHit ? new FixedCharacterCollisionHit(safe, result.Normal) : default;
            return result.DidHit;
        }
    }
}

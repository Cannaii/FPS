using System;
using AFPS.Math;
using AFPS.Simulation.World;

namespace AFPS.Simulation.Characters.Collision
{
    /// <summary>预测、权威与重放共用的定点世界后端，不经过 Unity/PhysX 或浮点转换。</summary>
    public sealed class FixedCharacterCollisionWorld : IFixedCharacterCollisionWorld, IWorldQuery
    {
        private readonly FixedWorld world;
        private readonly int layerMask;
        public ulong VersionHash => world.VersionHash;

        public FixedCharacterCollisionWorld(FixedWorld world, int layerMask = ~0)
        {
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            this.layerMask = layerMask;
        }

        // 能力查询复用碰撞绑定的同一快照，并与会话层掩码求交。
        // 不暴露未过滤的原始world，避免墙跑/梯子/钩索看见移动胶囊无法碰撞的层。
        public DeterministicHit Raycast(
            in FixVec3 origin,
            in FixVec3 direction,
            Fix maxDistance,
            int layerMask = ~0,
            FixedSurface surfaceMask = FixedSurface.All) =>
            world.Raycast(origin, direction, maxDistance, this.layerMask & layerMask, surfaceMask);

        public DeterministicHit CapsuleCast(
            in FixVec3 feet,
            Fix radius,
            Fix height,
            in FixVec3 direction,
            Fix maxDistance,
            int layerMask = ~0,
            FixedSurface surfaceMask = FixedSurface.Solid) =>
            world.CapsuleCast(feet, radius, height, direction, maxDistance, this.layerMask & layerMask, surfaceMask);

        public DeterministicHit GroundCheck(in FixVec3 center, Fix radius, Fix halfHeight,
            Fix probeDistance, int layerMask = ~0) =>
            world.GroundCheck(center, radius, halfHeight, probeDistance, this.layerMask & layerMask);

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

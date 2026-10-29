using AFPS.Math;

namespace AFPS.Simulation.Characters.Collision
{
    /// <summary>旧 PhysX 查询的显式适配：仅规范返回值，不承诺几何跨平台确定性。Phase 2C 替换。</summary>
    public sealed class LegacyCharacterCollisionWorldAdapter : IFixedCharacterCollisionWorld
    {
        private readonly ICharacterCollisionWorld world;
        private LegacyCharacterCollisionWorldAdapter(ICharacterCollisionWorld world) => this.world = world;
        public static IFixedCharacterCollisionWorld Adapt(ICharacterCollisionWorld world)
        {
            if (world == null)
            {
                throw new System.ArgumentNullException(nameof(world), "必须显式提供碰撞世界；平地仅供主动选择的测试/沙盒使用。");
            }
            return ReferenceEquals(world, FlatGroundCollisionWorld.Instance)
                ? FixedFlatGroundCollisionWorld.Instance : new LegacyCharacterCollisionWorldAdapter(world);
        }
        public bool CapsuleCast(
            FixVec3 feetPosition,
            FixVec3 direction,
            Fix distance,
            in CharacterCollisionConfig config,
            out FixedCharacterCollisionHit hit)
        {
            hit = default;
            if (!world.CapsuleCast(PlayerMovementState.ToVector(feetPosition), PlayerMovementState.ToVector(direction), distance.ToFloat(), config, out CharacterCollisionHit legacy))
            {
                return false;
            }

            hit = new FixedCharacterCollisionHit(Fix.FromFloat(legacy.Distance), PlayerMovementState.ToFixed(legacy.Normal).Normalized());
            return true;
        }
    }
}

using AFPS.Math;

namespace AFPS.Simulation.Characters.Collision
{
    /// <summary>纯定点 Y=0 平面，用于平地测试/回放；真实关卡不得静默使用。</summary>
    public sealed class FixedFlatGroundCollisionWorld : IFixedCharacterCollisionWorld
    {
        public static readonly FixedFlatGroundCollisionWorld Instance = new FixedFlatGroundCollisionWorld();
        private FixedFlatGroundCollisionWorld()
        {
        }

        public bool CapsuleCast(
            FixVec3 feetPosition,
            FixVec3 direction,
            Fix distance,
            in CharacterCollisionConfig config,
            out FixedCharacterCollisionHit hit)
        {
            hit = default;
            if (direction.Y >= -config.MinimumMoveDistanceFixed || feetPosition.Y.Raw < 0)
            {
                return false;
            }

            long candidate = (long)feetPosition.Y.Raw * Fix.Scale / -direction.Y.Raw;
            if (candidate < 0 || candidate > distance.Raw)
            {
                return false;
            }

            hit = new FixedCharacterCollisionHit(Fix.FromRaw((int)candidate), FixVec3.Up);
            return true;
        }
    }
}

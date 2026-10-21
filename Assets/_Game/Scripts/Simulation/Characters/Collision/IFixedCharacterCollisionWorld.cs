using AFPS.Math;

namespace AFPS.Simulation.Characters.Collision
{
    /// <summary>定点运动求解的胶囊查询边界，几何一致性由具体后端负责。</summary>
    public interface IFixedCharacterCollisionWorld
    {
        bool CapsuleCast(FixVec3 feetPosition, FixVec3 direction, Fix distance, in CharacterCollisionConfig config, out FixedCharacterCollisionHit hit);
    }
    public readonly struct FixedCharacterCollisionHit
    {
        public readonly Fix Distance;
        public readonly FixVec3 Normal;
        public FixedCharacterCollisionHit(Fix distance, FixVec3 normal) { Distance = distance; Normal = normal; }
    }
}

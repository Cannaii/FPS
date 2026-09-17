using UnityEngine;

namespace AFPS.Simulation.Characters.Collision
{
    /// <summary>
    /// 仅用于保持旧实验和纯移动测试兼容的 Y=0 平面查询实现。
    /// 真实运行时应注入场景胶囊查询实现。
    /// </summary>
    public sealed class FlatGroundCollisionWorld : ICharacterCollisionWorld
    {
        public static readonly FlatGroundCollisionWorld Instance = new FlatGroundCollisionWorld();

        private FlatGroundCollisionWorld()
        {
        }

        public bool CapsuleCast(Vector3 feetPosition, Vector3 direction, float distance, in CharacterCollisionConfig config, out CharacterCollisionHit hit)
        {
            hit = default;
            if (direction.y >= -config.MinimumMoveDistance || feetPosition.y < 0f)
            {
                return false;
            }

            float hitDistance = feetPosition.y / -direction.y;
            if (hitDistance < 0f || hitDistance > distance)
            {
                return false;
            }

            hit = new CharacterCollisionHit(hitDistance, Vector3.up);
            return true;
        }
    }
}

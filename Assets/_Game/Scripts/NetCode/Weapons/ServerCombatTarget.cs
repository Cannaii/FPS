using UnityEngine;

namespace AFPS.NetCode.Weapons
{
    /// <summary>服务器当前 Tick 中可被射击的简化胶囊 Hitbox。</summary>
    public readonly struct ServerCombatTarget
    {
        public readonly uint EntityId;
        public readonly Vector3 Position;
        public readonly float Radius;
        public readonly float Height;
        public readonly float Health;

        public ServerCombatTarget(uint entityId, Vector3 position, float radius, float height, float health)
        {
            EntityId = entityId;
            Position = position;
            Radius = radius;
            Height = height;
            Health = health;
        }
    }
}

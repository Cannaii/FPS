using AFPS.Math;
using UnityEngine;

namespace AFPS.NetCode.Weapons
{
    /// <summary>旧调用方的 Unity 边界；回溯历史和权威 Tick 不经此入口。</summary>
    public readonly partial struct ServerCombatTarget
    {
        public Vector3 Position => new Vector3(
            PositionFixed.X.ToFloat(), PositionFixed.Y.ToFloat(), PositionFixed.Z.ToFloat());
        public float Radius => RadiusFixed.ToFloat();
        public float Height => HeightFixed.ToFloat();
        public float Health => HealthFixed.ToFloat();

        public ServerCombatTarget(uint entityId, Vector3 position, float radius, float height, float health)
            : this(entityId,
                new FixVec3(Fix.FromFloat(position.x), Fix.FromFloat(position.y), Fix.FromFloat(position.z)),
                Fix.FromFloat(radius), Fix.FromFloat(height), Fix.FromFloat(health))
        {
        }
    }
}

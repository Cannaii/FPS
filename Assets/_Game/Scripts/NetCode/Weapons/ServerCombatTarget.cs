using AFPS.Math;

namespace AFPS.NetCode.Weapons
{
    /// <summary>服务器竖直胶囊 Hitbox：脚底世界坐标、半径/高度（米）和生命值均存 Q16.16。</summary>
    public readonly partial struct ServerCombatTarget
    {
        public readonly uint EntityId;
        public readonly FixVec3 PositionFixed;
        public readonly Fix RadiusFixed;
        public readonly Fix HeightFixed;
        public readonly Fix HealthFixed;

        public ServerCombatTarget(uint entityId, FixVec3 position, Fix radius, Fix height, Fix health)
        {
            EntityId = entityId;
            PositionFixed = position;
            RadiusFixed = radius;
            HeightFixed = height;
            HealthFixed = health;
        }
    }
}

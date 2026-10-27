using AFPS.Math;

namespace AFPS.Simulation.Characters.Collision
{
    /// <summary>当前运动Tick结束后的接触；default表示无墙，法线指向角色可离墙的世界水平方向。</summary>
    public readonly struct FixedCharacterContacts
    {
        public readonly bool HasWall;
        public readonly FixVec3 WallNormal; // 世界XZ平面单位向外法线，Y恒零。
        public readonly Fix WallDistance; // 胶囊到墙的扫掠距离，米，包含皮肤厚度边界。

        public FixedCharacterContacts(FixVec3 wallNormal, Fix wallDistance)
        {
            HasWall = true;
            WallNormal = wallNormal;
            WallDistance = wallDistance;
        }
    }
}

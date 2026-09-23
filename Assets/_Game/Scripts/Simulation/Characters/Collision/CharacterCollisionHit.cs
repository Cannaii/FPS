using UnityEngine;

namespace AFPS.Simulation.Characters.Collision
{
    /// <summary>
    /// 一次角色胶囊扫掠得到的最近阻挡接触。
    /// Distance 是沿查询方向可移动的距离，单位为米；Normal 是碰撞面的世界空间法线。
    /// </summary>
    public readonly struct CharacterCollisionHit
    {
        public readonly float Distance;
        public readonly Vector3 Normal;

        public CharacterCollisionHit(float distance, Vector3 normal)
        {
            Distance = distance;
            Normal = normal;
        }
    }
}

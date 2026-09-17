using UnityEngine;

namespace AFPS.Simulation.Characters.Collision
{
    /// <summary>
    /// 为可预测角色模拟提供无状态胶囊扫掠查询。
    /// 客户端预测、服务器权威模拟和客户端重放必须查询语义一致的碰撞场景。
    /// </summary>
    public interface ICharacterCollisionWorld
    {
        /// <summary>
        /// 从脚底世界位置沿单位方向扫掠竖直胶囊，只返回最近的阻挡接触。
        /// </summary>
        bool CapsuleCast(Vector3 feetPosition, Vector3 direction, float distance, in CharacterCollisionConfig config, out CharacterCollisionHit hit);
    }
}

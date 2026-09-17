using System;
using AFPS.Simulation.Characters.Collision;
using UnityEngine;

namespace AFPS.Bootstrap.Physics
{
    /// <summary>
    /// 将 Unity PhysicsScene 的静态场景查询适配为可预测角色胶囊扫掠。
    /// 客户端与服务器场景必须包含相同碰撞体、Layer Mask 和 Transform。
    /// </summary>
    public sealed class UnityPhysicsCharacterCollisionWorld : ICharacterCollisionWorld
    {
        private readonly PhysicsScene physicsScene;
        private readonly int layerMask;
        private readonly QueryTriggerInteraction queryTriggerInteraction;

        public UnityPhysicsCharacterCollisionWorld(PhysicsScene physicsScene, int layerMask, QueryTriggerInteraction queryTriggerInteraction)
        {
            if (!physicsScene.IsValid())
            {
                throw new ArgumentException("PhysicsScene 必须有效。", nameof(physicsScene));
            }

            this.physicsScene = physicsScene;
            this.layerMask = layerMask;
            this.queryTriggerInteraction = queryTriggerInteraction;
        }

        public bool CapsuleCast(Vector3 feetPosition, Vector3 direction, float distance, in CharacterCollisionConfig config, out CharacterCollisionHit hit)
        {
            hit = default;
            if (distance <= 0f || direction.sqrMagnitude <= 0f)
            {
                return false;
            }

            direction.Normalize();
            float queryRadius = Mathf.Max(config.MinimumMoveDistance, config.Radius - config.SkinWidth);
            Vector3 bottomSphereCenter = feetPosition + Vector3.up * config.Radius;
            Vector3 topSphereCenter = feetPosition + Vector3.up * (config.Height - config.Radius);
            if (!physicsScene.CapsuleCast(bottomSphereCenter, topSphereCenter, queryRadius, direction, out RaycastHit physicsHit, distance + config.SkinWidth, layerMask, queryTriggerInteraction))
            {
                return TryDownwardGroundRay(feetPosition, direction, distance, config, out hit);
            }

            float safeDistance = Mathf.Clamp(physicsHit.distance - config.SkinWidth, 0f, distance);
            hit = new CharacterCollisionHit(safeDistance, physicsHit.normal);
            return true;
        }

        private bool TryDownwardGroundRay(Vector3 feetPosition, Vector3 direction, float distance, in CharacterCollisionConfig config, out CharacterCollisionHit hit)
        {
            hit = default;
            if (direction.y > -0.999f || Mathf.Abs(direction.x) > 0.001f || Mathf.Abs(direction.z) > 0.001f)
            {
                return false;
            }

            // PhysX 的 Terrain 胶囊扫掠可能在精确落于地形网格顶点或接缝时漏检。
            // 从脚底稍上方补一条中心射线，避免单次漏检令预测角色进入地形内部后持续下落。
            float originOffset = Mathf.Max(config.SkinWidth + config.MinimumMoveDistance, 0.001f);
            Vector3 rayOrigin = feetPosition + Vector3.up * originOffset;
            if (!physicsScene.Raycast(rayOrigin, Vector3.down, out RaycastHit rayHit, distance + originOffset, layerMask, queryTriggerInteraction))
            {
                return false;
            }

            float safeDistance = Mathf.Clamp(rayHit.distance - originOffset, 0f, distance);
            hit = new CharacterCollisionHit(safeDistance, rayHit.normal);
            return true;
        }
    }
}

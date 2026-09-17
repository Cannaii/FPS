using System;
using UnityEngine;

namespace AFPS.Simulation.Characters.Collision
{
    /// <summary>
    /// 使用无状态胶囊扫掠实现移动、墙面滑动、斜坡、台阶、地面吸附和天花板阻挡。
    /// </summary>
    public static class KinematicCharacterMotor
    {
        public static void Move(Vector3 startPosition, Vector3 startVelocity, float tickDeltaTime, in CharacterCollisionConfig config, ICharacterCollisionWorld collisionWorld, out Vector3 position, out Vector3 velocity, out bool isGrounded)
        {
            if (collisionWorld == null)
            {
                throw new ArgumentNullException(nameof(collisionWorld));
            }

            position = startPosition;
            velocity = startVelocity;
            isGrounded = false;
            Vector3 remaining = velocity * tickDeltaTime;

            for (int iteration = 0; iteration < config.MaxSlideIterations && remaining.sqrMagnitude > config.MinimumMoveDistance * config.MinimumMoveDistance; iteration++)
            {
                float distance = remaining.magnitude;
                Vector3 direction = remaining / distance;
                if (!collisionWorld.CapsuleCast(position, direction, distance, config, out CharacterCollisionHit hit))
                {
                    position += remaining;
                    remaining = Vector3.zero;
                    break;
                }

                float safeDistance = Mathf.Clamp(hit.Distance, 0f, distance);
                position += direction * safeDistance;
                Vector3 untraveled = remaining - direction * safeDistance;
                bool walkable = hit.Normal.y >= config.MinimumGroundNormalY;
                bool horizontalObstacle = !walkable && Mathf.Abs(hit.Normal.y) < config.MinimumGroundNormalY && new Vector2(untraveled.x, untraveled.z).sqrMagnitude > config.MinimumMoveDistance * config.MinimumMoveDistance;

                if (horizontalObstacle && TryStep(position, untraveled, config, collisionWorld, out Vector3 steppedPosition))
                {
                    position = steppedPosition;
                    remaining = Vector3.zero;
                    isGrounded = true;
                    velocity = new Vector3(velocity.x, 0f, velocity.z);
                    break;
                }

                if (walkable && direction.y <= 0f)
                {
                    isGrounded = true;
                }

                Vector3 blockingNormal = GetBlockingNormal(hit.Normal, walkable);
                remaining = Vector3.ProjectOnPlane(untraveled, blockingNormal);
                if (Vector3.Dot(velocity, blockingNormal) < 0f)
                {
                    velocity = Vector3.ProjectOnPlane(velocity, blockingNormal);
                }
            }

            if (velocity.y <= 0f && TrySnapToGround(position, config, collisionWorld, out Vector3 groundedPosition))
            {
                position = groundedPosition;
                velocity = new Vector3(velocity.x, 0f, velocity.z);
                isGrounded = true;
            }
        }

        private static Vector3 GetBlockingNormal(Vector3 contactNormal, bool walkable)
        {
            if (walkable || contactNormal.y <= 0f)
            {
                return contactNormal;
            }

            Vector3 horizontalNormal = new Vector3(contactNormal.x, 0f, contactNormal.z);
            return horizontalNormal.sqrMagnitude > 0f ? horizontalNormal.normalized : contactNormal;
        }

        private static bool TryStep(Vector3 position, Vector3 remaining, in CharacterCollisionConfig config, ICharacterCollisionWorld collisionWorld, out Vector3 steppedPosition)
        {
            steppedPosition = default;
            if (config.StepHeight <= 0f)
            {
                return false;
            }

            Vector3 horizontal = new Vector3(remaining.x, 0f, remaining.z);
            float horizontalDistance = horizontal.magnitude;
            if (horizontalDistance <= config.MinimumMoveDistance || collisionWorld.CapsuleCast(position, Vector3.up, config.StepHeight, config, out _))
            {
                return false;
            }

            Vector3 raisedPosition = position + Vector3.up * config.StepHeight;
            Vector3 horizontalDirection = horizontal / horizontalDistance;
            if (collisionWorld.CapsuleCast(raisedPosition, horizontalDirection, horizontalDistance, config, out _))
            {
                return false;
            }

            Vector3 forwardPosition = raisedPosition + horizontal;
            float downwardDistance = config.StepHeight + config.GroundProbeDistance;
            if (!collisionWorld.CapsuleCast(forwardPosition, Vector3.down, downwardDistance, config, out CharacterCollisionHit groundHit) || groundHit.Normal.y < config.MinimumGroundNormalY)
            {
                return false;
            }

            steppedPosition = forwardPosition + Vector3.down * groundHit.Distance;
            return true;
        }

        private static bool TrySnapToGround(Vector3 position, in CharacterCollisionConfig config, ICharacterCollisionWorld collisionWorld, out Vector3 groundedPosition)
        {
            groundedPosition = default;
            if (!collisionWorld.CapsuleCast(position, Vector3.down, config.GroundProbeDistance, config, out CharacterCollisionHit hit) || hit.Normal.y < config.MinimumGroundNormalY)
            {
                return false;
            }

            groundedPosition = position + Vector3.down * Mathf.Max(0f, hit.Distance);
            return true;
        }
    }
}

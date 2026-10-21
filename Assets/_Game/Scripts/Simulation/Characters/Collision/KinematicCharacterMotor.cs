using System;
using AFPS.Math;

namespace AFPS.Simulation.Characters.Collision
{
    /// <summary>定点扫掠/滑动/台阶/地面吸附求解；不访问场景或渲染状态。</summary>
    public static partial class KinematicCharacterMotor
    {
        public static void Move(
            FixVec3 startPosition,
            FixVec3 startVelocity,
            Fix tickDeltaTime,
            in CharacterCollisionConfig config,
            IFixedCharacterCollisionWorld world,
            out FixVec3 position,
            out FixVec3 velocity,
            out bool isGrounded)
        {
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            if (tickDeltaTime.Raw <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tickDeltaTime));
            }

            position = startPosition;
            velocity = startVelocity;
            isGrounded = false;
            // 位置以脚底为原点，单位米；剩余位移随每次接触投影到可移动的切平面。
            FixVec3 remaining = velocity * tickDeltaTime;
            for (int i = 0; i < config.MaxSlideIterations && remaining.Magnitude > config.MinimumMoveDistanceFixed; i++)
            {
                Fix distance = remaining.Magnitude;
                FixVec3 direction = remaining.Normalized();
                if (!world.CapsuleCast(position, direction, distance, config, out FixedCharacterCollisionHit hit))
                {
                    position += remaining;
                    break;
                }

                Fix safe = FixMath.Clamp(hit.Distance, Fix.Zero, distance);
                position += direction * safe;
                FixVec3 untraveled = remaining - direction * safe;
                bool walkable = hit.Normal.Y >= config.MinimumGroundNormalYFixed;
                FixVec3 horizontal = new FixVec3(untraveled.X, Fix.Zero, untraveled.Z);
                bool obstacle = !walkable && FixMath.Abs(hit.Normal.Y) < config.MinimumGroundNormalYFixed && horizontal.Magnitude > config.MinimumMoveDistanceFixed;
                if (obstacle && TryStep(position, untraveled, config, world, out FixVec3 stepped))
                {
                    position = stepped;
                    isGrounded = true;
                    velocity = new FixVec3(velocity.X, Fix.Zero, velocity.Z);
                    break;
                }

                if (walkable && direction.Y.Raw <= 0)
                {
                    isGrounded = true;
                }

                FixVec3 blocking = GetBlockingNormal(hit.Normal, walkable);
                remaining = ClipMotion(untraveled, blocking);
                if (FixVec3.Dot(velocity, blocking).Raw < 0)
                {
                    velocity = ClipMotion(velocity, blocking);
                }
            }

            if (velocity.Y.Raw <= 0 && TrySnap(position, config, world, out FixVec3 grounded))
            {
                position = grounded;
                velocity = new FixVec3(velocity.X, Fix.Zero, velocity.Z);
                isGrounded = true;
            }
        }

        // 整数投影的舍入可能留下极小的向内分量；再次归一化会放大为零距离重复碰撞。
        // 沿法线最大分量留两 Raw 的有界余量，确保后续方向舍入仍在切平面外侧。
        private static FixVec3 ClipMotion(FixVec3 value, FixVec3 normal)
        {
            FixVec3 projected = FixVec3.ProjectOnPlane(value, normal);
            if (projected == FixVec3.Zero)
            {
                return projected;
            }

            long nx = normal.X.Raw,
                ny = normal.Y.Raw,
                nz = normal.Z.Raw;
            long ax = System.Math.Abs(nx),
                ay = System.Math.Abs(ny),
                az = System.Math.Abs(nz);
            long dot = (long)projected.X.Raw * nx + (long)projected.Y.Raw * ny + (long)projected.Z.Raw * nz;
            if ((ax == 0 && ay == 0) || (ax == 0 && az == 0) || (ay == 0 && az == 0))
            {
                return projected;
            }

            long margin = 2 * (ax + ay + az);
            if (dot >= margin)
            {
                return projected;
            }

            long largest = System.Math.Max(ax, System.Math.Max(ay, az));
            if (largest == 0)
            {
                return projected;
            }

            int adjustment = checked((int)((margin - dot + largest - 1) / largest));
            if (ax == largest)
            {
                return new FixVec3(Fix.FromRaw(checked(projected.X.Raw + (nx < 0 ? -adjustment : adjustment))), projected.Y, projected.Z);
            }

            if (ay == largest)
            {
                return new FixVec3(projected.X, Fix.FromRaw(checked(projected.Y.Raw + (ny < 0 ? -adjustment : adjustment))), projected.Z);
            }

            return new FixVec3(projected.X, projected.Y, Fix.FromRaw(checked(projected.Z.Raw + (nz < 0 ? -adjustment : adjustment))));
        }

        private static FixVec3 GetBlockingNormal(FixVec3 normal, bool walkable)
        {
            if (walkable || normal.Y.Raw <= 0)
            {
                return normal;
            }

            FixVec3 horizontal = new FixVec3(normal.X, Fix.Zero, normal.Z);
            return horizontal == FixVec3.Zero ? normal : horizontal.Normalized();
        }

        private static bool TryStep(
            FixVec3 position,
            FixVec3 remaining,
            in CharacterCollisionConfig config,
            IFixedCharacterCollisionWorld world,
            out FixVec3 stepped)
        {
            stepped = default;
            if (config.StepHeightFixed.Raw <= 0)
            {
                return false;
            }

            FixVec3 horizontal = new FixVec3(remaining.X, Fix.Zero, remaining.Z);
            Fix distance = horizontal.Magnitude;
            if (distance <= config.MinimumMoveDistanceFixed || world.CapsuleCast(position, FixVec3.Up, config.StepHeightFixed, config, out _))
            {
                return false;
            }

            // 跨阶必须依次通过上抬、水平前进、向下落地，不能直接传送穿过天花板或墙壁。
            FixVec3 raised = position + FixVec3.Up * config.StepHeightFixed;
            if (world.CapsuleCast(raised, horizontal.Normalized(), distance, config, out _))
            {
                return false;
            }

            FixVec3 forward = raised + horizontal;
            if (!world.CapsuleCast(forward, -FixVec3.Up, config.StepHeightFixed + config.GroundProbeDistanceFixed, config, out FixedCharacterCollisionHit hit)
                || hit.Normal.Y < config.MinimumGroundNormalYFixed)
            {
                return false;
            }

            stepped = forward - FixVec3.Up * hit.Distance;
            return true;
        }

        private static bool TrySnap(FixVec3 position, in CharacterCollisionConfig config, IFixedCharacterCollisionWorld world, out FixVec3 grounded)
        {
            grounded = default;
            if (!world.CapsuleCast(position, -FixVec3.Up, config.GroundProbeDistanceFixed, config, out FixedCharacterCollisionHit hit)
                || hit.Normal.Y < config.MinimumGroundNormalYFixed)
            {
                return false;
            }

            grounded = position - FixVec3.Up * FixMath.Max(Fix.Zero, hit.Distance);
            return true;
        }
    }
}

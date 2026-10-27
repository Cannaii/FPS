using AFPS.Math;
using AFPS.Simulation.Characters.Collision;
using AFPS.Simulation.Weapons;
using AFPS.Simulation.World;

namespace AFPS.Simulation.Characters
{
    /// <summary>Combined速度约束摆锤；不投影/瞬移位置，不使用Joint，唯一位移出口仍是运动器。</summary>
    public static class PlayerGrappleSimulation
    {
        // 包含发射眼高及碰撞后的少量绳长误差；同时限制坏状态的向量减法和长度计算。
        public static readonly Fix MaximumRopeLength = Fix.FromInt(256);

        public static bool TryOrigin(in PlayerState state, in PlayerSimulationConfig config, out FixVec3 origin)
        {
            Fix height = FixMath.Min(PlayerStanceSimulation.EyeHeight(state, config.Grapple.OriginHeight, config),
                PlayerStanceSimulation.Collision(state, config).HeightFixed);
            long y = (long)state.PositionFixed.Y.Raw + height.Raw;
            origin = default;
            if (y < int.MinValue || y > int.MaxValue)
            {
                return false;
            }
            origin = new FixVec3(state.PositionFixed.X, Fix.FromRaw((int)y), state.PositionFixed.Z);
            return true;
        }

        public static bool TryOffset(FixVec3 from, FixVec3 to, out FixVec3 offset, out Fix distance)
        {
            offset = default;
            distance = Fix.Zero;
            long x = (long)to.X.Raw - from.X.Raw;
            long y = (long)to.Y.Raw - from.Y.Raw;
            long z = (long)to.Z.Raw - from.Z.Raw;
            long limit = MaximumRopeLength.Raw;
            if (x < -limit || x > limit || y < -limit || y > limit || z < -limit || z > limit)
            {
                return false;
            }
            offset = new FixVec3(Fix.FromRaw((int)x), Fix.FromRaw((int)y), Fix.FromRaw((int)z));
            distance = offset.Magnitude;
            return distance <= MaximumRopeLength;
        }

        public static bool Begin(ref PlayerState next, in PlayerState previous, in PlayerInputCommand input,
            in PlayerSimulationConfig config, IFixedCharacterCollisionWorld world, bool interrupted)
        {
            if (!input.GrappleHeld)
            {
                Cancel(ref next);
                next.GrappleConsumed = false;
                return false;
            }
            bool pressed = !previous.GrappleConsumed;
            next.GrappleConsumed = true;
            if (interrupted || input.JumpPressed || !(world is IWorldQuery query)
                || !TryOrigin(next, config, out var origin)
                || !WeaponAim.TryGetDirection(next.YawFixedDegrees, next.PitchFixedDegrees, out var aim))
            {
                Cancel(ref next);
                return false;
            }
            if (!previous.IsGrappling)
            {
                Cancel(ref next);
                if (!pressed)
                {
                    return false;
                }
                // 先找最近实体，再检查Grapple；未标记的实体不能被射线穿透借用后方锚点。
                var hit = query.Raycast(origin, aim, config.Grapple.Range,
                    surfaceMask: FixedSurface.Solid | FixedSurface.Grapple);
                if (!hit.DidHit || hit.StartedInside || (hit.Surface & FixedSurface.Grapple) == 0
                    || !TryOffset(next.PositionFixed, hit.Point, out _, out var length)
                    || length <= config.Grapple.BreakDistance)
                {
                    return false;
                }
                next.IsGrappling = true;
                next.GrapplePointFixed = hit.Point;
                next.GrappleLengthFixed = length;
                next.GrappleShapeId = hit.ShapeId;
            }
            else
            {
                if (!TryOffset(origin, next.GrapplePointFixed, out var segment, out var distance) || distance == Fix.Zero)
                {
                    Cancel(ref next);
                    return false;
                }
                var blocker = query.Raycast(origin, segment, distance, surfaceMask: FixedSurface.Solid);
                if (blocker.DidHit && blocker.ShapeId != next.GrappleShapeId && blocker.Distance < distance - Fix.Epsilon)
                {
                    Cancel(ref next);
                    return false;
                }
            }
            return next.IsGrappling;
        }

        public static void Apply(ref PlayerState next, FixVec3 velocity, in PlayerSimulationConfig config, Fix deltaTime)
        {
            if (!TryOffset(next.PositionFixed, next.GrapplePointFixed, out var to, out var distance)
                || distance <= config.Grapple.BreakDistance)
            {
                Cancel(ref next);
                next.VelocityFixed = velocity;
                return;
            }
            var c = config.Grapple;
            // 卷绳与反馈用宽Raw饱和，禁止先在Fix除法中溢出再Clamp。
            long reel = ((long)c.ReelSpeed.Raw * deltaTime.Raw + Fix.Scale / 2) / Fix.Scale;
            next.GrappleLengthFixed = Fix.FromRaw((int)System.Math.Max(c.BreakDistance.Raw,
                (long)next.GrappleLengthFixed.Raw - reel));
            FixVec3 direction = to.Normalized();
            velocity = FixVec3.ClampMagnitude(velocity, c.MaximumSpeed);
            WeaponAim.TryGetDirection(next.YawFixedDegrees, next.PitchFixedDegrees, out var aim);
            var tangent = aim - direction * FixVec3.Dot(aim, direction);
            velocity += direction * (c.PullAcceleration * deltaTime) + tangent * (c.CameraInfluence * deltaTime);
            if (distance >= next.GrappleLengthFixed)
            {
                // 去除离心速度并补偿已经超出的绳长；切向分量保留，形成摆荡。
                // 反馈只改变速度，遇墙时运动器可以阻止收绳，绝不直接把角色拉穿墙。
                long requiredRaw = (long)(distance.Raw - next.GrappleLengthFixed.Raw) * Fix.Scale / deltaTime.Raw;
                Fix required = Fix.FromRaw((int)System.Math.Min(c.MaximumSpeed.Raw, requiredRaw));
                Fix inward = FixVec3.Dot(velocity, direction);
                if (inward < required)
                {
                    velocity += direction * (required - inward);
                }
            }
            next.VelocityFixed = FixVec3.ClampMagnitude(velocity, c.MaximumSpeed);
        }

        public static void Complete(ref PlayerState next, in PlayerGrappleConfig config)
        {
            if (next.IsGrappling && (!TryOffset(next.PositionFixed, next.GrapplePointFixed, out _, out var distance)
                || distance <= config.BreakDistance))
            {
                Cancel(ref next);
            }
        }

        public static void Cancel(ref PlayerState state)
        {
            state.IsGrappling = false;
            state.GrapplePointFixed = FixVec3.Zero;
            state.GrappleLengthFixed = Fix.Zero;
            state.GrappleShapeId = 0;
        }
    }
}

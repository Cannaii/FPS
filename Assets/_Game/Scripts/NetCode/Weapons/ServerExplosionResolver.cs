using System.Collections.Generic;
using AFPS.Math;
using AFPS.Simulation.World;
using BigInteger = System.Numerics.BigInteger;

namespace AFPS.NetCode.Weapons
{
    public readonly struct ExplosionDamage
    {
        public readonly uint EntityId;
        public readonly Fix Damage;
        public ExplosionDamage(uint entityId, Fix damage)
        {
            EntityId = entityId;
            Damage = damage;
        }
    }

    /// <summary>球形范围伤害，按玩家中心平方距离衰减；包含自己，排除已直接命中的目标。</summary>
    public static class ServerExplosionResolver
    {
        public static void Resolve(FixVec3 center, FixVec3 visibilityOrigin, Fix radius, Fix damage,
            uint directTarget, IReadOnlyList<ServerCombatTarget> targets, IWorldQuery world, int layerMask,
            List<ExplosionDamage> output)
        {
            output.Clear();
            if (radius.Raw <= 0 || damage.Raw <= 0 || targets == null)
            {
                return;
            }
            BigInteger radiusSquared = (BigInteger)radius.Raw * radius.Raw;
            foreach (var target in targets)
            {
                if (target.EntityId == 0 || target.EntityId == directTarget || target.HealthFixed.Raw <= 0)
                {
                    continue;
                }
                long targetY = (long)target.PositionFixed.Y.Raw + target.HeightFixed.Raw / 2;
                long x = (long)target.PositionFixed.X.Raw - center.X.Raw;
                long y = targetY - center.Y.Raw;
                long z = (long)target.PositionFixed.Z.Raw - center.Z.Raw;
                // 宽坐标/平方乘积不经过 float；半径边界伤害为零，向下取整不额外制造 1 Raw 伤害。
                BigInteger squared = (BigInteger)x * x + (BigInteger)y * y + (BigInteger)z * z;
                if (squared >= radiusSquared)
                {
                    continue;
                }
                int amount = (int)((BigInteger)damage.Raw * (radiusSquared - squared) / radiusSquared);
                if (amount == 0 || IsBlocked(visibilityOrigin, target.PositionFixed.X.Raw, targetY,
                    target.PositionFixed.Z.Raw, world, layerMask))
                {
                    continue;
                }
                output.Add(new ExplosionDamage(target.EntityId, Fix.FromRaw(amount)));
            }
            // 不能依赖字典枚举顺序决定达到击杀上限时先结算哪个目标。
            output.Sort((left, right) => left.EntityId.CompareTo(right.EntityId));
        }

        private static bool IsBlocked(FixVec3 origin, long targetX, long targetY, long targetZ,
            IWorldQuery world, int mask)
        {
            if (world == null)
            {
                return false;
            }
            long x = targetX - origin.X.Raw;
            long y = targetY - origin.Y.Raw;
            long z = targetZ - origin.Z.Raw;
            if (System.Math.Abs(x) > int.MaxValue || System.Math.Abs(y) > int.MaxValue || System.Math.Abs(z) > int.MaxValue)
            {
                return true; // 超出可表达的查询方向时保守遮挡；当前 3 米半径不会进入该分支。
            }
            var direction = new FixVec3(Fix.FromRaw((int)x), Fix.FromRaw((int)y), Fix.FromRaw((int)z));
            ulong squared = (ulong)((BigInteger)x * x + (BigInteger)y * y + (BigInteger)z * z);
            int distance = (int)System.Math.Min((ulong)int.MaxValue, FixMath.IntegerSqrt(squared));
            return distance > 0 && world.Raycast(origin, direction, Fix.FromRaw(distance), mask, FixedSurface.Solid).DidHit;
        }
    }
}

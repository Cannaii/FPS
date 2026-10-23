using System.Collections.Generic;
using AFPS.Math;
using AFPS.NetCode.Weapons;
using AFPS.Simulation.World;

namespace AFPS.Tests.EditMode
{
    /// <summary>不引用 NUnit/Unity 的爆炸 Raw 重放，覆盖衰减、排序、遮挡与大坐标低位。</summary>
    public static class DeterministicExplosionScenario
    {
        public static ulong Run()
        {
            ulong hash = 14695981039346656037UL;
            var output = new List<ExplosionDamage>();
            for (int tick = 0; tick < 256; tick++)
            {
                int origin = 655360000 + tick;
                var center = new FixVec3(Fix.FromRaw(origin), Fix.One, Fix.Zero);
                var targets = new[]
                {
                    Target(9, origin), Target(2, origin + 65536),
                    Target(1, origin - 1), Target(4, origin + 196608)
                };
                FixedWorld world = tick % 3 != 0 ? null : new FixedWorld(new[]
                {
                    new FixedWorldBox(1, 0, FixedSurface.Solid, new FixedAabb(
                        new FixVec3(Fix.FromRaw(origin + 32768), Fix.Zero, -Fix.One),
                        new FixVec3(Fix.FromRaw(origin + 98304), Fix.FromInt(2), Fix.One)))
                });
                ServerExplosionResolver.Resolve(center, center, Fix.FromRaw(196608 + tick % 3),
                    Fix.FromRaw(6553600 + tick), tick % 2 == 0 ? 9u : 0u, targets, world, ~0, output);
                Add(ref hash, (uint)output.Count);
                foreach (var result in output)
                {
                    Add(ref hash, result.EntityId);
                    Add(ref hash, (uint)result.Damage.Raw);
                }
            }
            return hash;
        }

        private static ServerCombatTarget Target(uint entity, int x) =>
            new ServerCombatTarget(entity, new FixVec3(Fix.FromRaw(x), Fix.Zero, Fix.Zero),
                Fix.Half, Fix.FromInt(2), Fix.FromInt(100));

        private static void Add(ref ulong hash, uint value)
        {
            for (int shift = 0; shift < 32; shift += 8)
            {
                hash = unchecked((hash ^ ((value >> shift) & 255u)) * 1099511628211UL);
            }
        }
    }
}

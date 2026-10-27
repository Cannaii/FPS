using AFPS.Math;
using AFPS.Simulation.Characters;
using AFPS.Simulation.Characters.Collision;
using AFPS.Simulation.World;

namespace AFPS.Tests.EditMode
{
    /// <summary>跨运行时验证位置、滑动及最终墙接触Raw；包含静止、离墙和墙角。</summary>
    public static class DeterministicContactScenario
    {
        public const ulong ExpectedHash = 6903272866268398094UL;

        public static ulong Run()
        {
            var boxes = new[]
            {
                new FixedWorldBox(1, 0, FixedSurface.Solid,
                    new FixedAabb(V(2, 0, -10), V(3, 10, 10))),
                new FixedWorldBox(2, 0, FixedSurface.Solid,
                    new FixedAabb(V(-10, 0, 2), V(10, 10, 3)))
            };
            var world = new FixedCharacterCollisionWorld(new FixedWorld(boxes));
            var random = new FixRandom(20261027);
            ulong hash = 14695981039346656037UL;
            for (int i = 0; i < 2048; i++)
            {
                var position = new FixVec3(Fix.FromRatio(3, 2) - Fix.FromRaw(random.NextInt(0, 1400)),
                    Fix.One, i % 2 == 0 ? Fix.Zero : Fix.FromRatio(3, 2));
                FixVec3 velocity = i % 3 == 0 ? FixVec3.Zero : FixVec3.Right * Fix.FromInt(i % 3 == 1 ? 6 : -6);
                KinematicCharacterMotor.Move(position, velocity, SimulationStep.Step50Hz,
                    CharacterCollisionConfig.Default, world, out var next, out var speed, out bool grounded, out var contacts);
                Add(ref hash, next.X.Raw);
                Add(ref hash, next.Y.Raw);
                Add(ref hash, next.Z.Raw);
                Add(ref hash, speed.X.Raw);
                Add(ref hash, speed.Y.Raw);
                Add(ref hash, speed.Z.Raw);
                Add(ref hash, grounded ? 1 : 0);
                Add(ref hash, contacts.HasWall ? 1 : 0);
                Add(ref hash, contacts.WallNormal.X.Raw);
                Add(ref hash, contacts.WallNormal.Y.Raw);
                Add(ref hash, contacts.WallNormal.Z.Raw);
                Add(ref hash, contacts.WallDistance.Raw);
            }
            return hash;
        }

        private static FixVec3 V(int x, int y, int z) =>
            new FixVec3(Fix.FromInt(x), Fix.FromInt(y), Fix.FromInt(z));

        private static void Add(ref ulong hash, int raw)
        {
            uint bits = unchecked((uint)raw);
            for (int shift = 0; shift < 32; shift += 8)
            {
                hash = unchecked((hash ^ ((bits >> shift) & 255u)) * 1099511628211UL);
            }
        }
    }
}

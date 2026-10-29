using System;
using AFPS.Math;
using AFPS.Core.Collections;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Prediction;
using AFPS.Simulation.Characters;
using AFPS.Simulation.Characters.Collision;
using AFPS.Simulation.World;

namespace AFPS.Tests.EditMode
{
    // Unity 测试和引擎外探针编译同一份场景，防止两个验证入口的输入或哈希字段悄悄分叉。
    internal static class DeterministicMapReplayScenario
    {
        private static Fix F(int x) => Fix.FromInt(x);
        private static FixVec3 V(int x, int y, int z) => new FixVec3(F(x), F(y), F(z));
        public static PlayerMovementConfig CreateConfiguration() => new PlayerMovementConfig(F(6), F(20), F(20), F(8));

        public static ulong Run(SimulationWorldContext context, out int contacts)
        {
            // 装配入口与后续运行时共用，重放不自行创建另一套配置、步长或碰撞世界。
            FixedWorld restored = context.World;
            FixedCharacterCollisionWorld backend = context.CollisionWorld;
            PlayerMovementConfig config = context.MovementConfiguration;
            var random = new FixRandom(20261025);
            ulong hash = 14695981039346656037UL;
            for (int i = 0; i < 4096; i++)
            {
                var feet = new FixVec3(
                    Fix.FromRaw(random.NextInt(-10 * 65536, 10 * 65536)),
                    Fix.FromRaw(random.NextInt(-2 * 65536, 6 * 65536)),
                    Fix.FromRaw(random.NextInt(-10 * 65536, 10 * 65536)));
                var direction = new FixVec3(Fix.FromRaw(random.NextInt(-65536, 65537)), Fix.FromRaw(random.NextInt(-65536, 65537)), Fix.FromRaw(random.NextInt(-65536, 65537)));
                var hit = restored.CapsuleCast(feet, Fix.Half, F(2), direction, F(20));
                Add(ref hash, unchecked((int)hit.ShapeId));
                Add(ref hash, hit.Distance.Raw);
                Add(ref hash, hit.Normal.X.Raw);
                Add(ref hash, hit.Normal.Y.Raw);
                Add(ref hash, hit.Normal.Z.Raw);
                Add(ref hash, hit.StartedInside ? 1 : 0);
            }

            var state = new PlayerMovementState
            {
                Tick = uint.MaxValue - 2048,
                PositionFixed = V(0, 2, 0),
                IsGrounded = false
            };
            var inputs = new TickBuffer<PlayerInputCommand>(128);
            var states = new TickBuffer<PlayerMovementState>(128);
            var packet = new byte[AuthoritativeMovementStateCodec.PacketSize];
            contacts = 0;
            for (int i = 0; i < 8192; i++)
            {
                var input = new PlayerInputCommand
                {
                    Tick = unchecked(state.Tick + 1),
                    JumpPressed = i % 71 == 0
                };
                // 连续一段移动方向，确保实际抵达墙、台阶、天花板，而非随机输入在原地抖动。
                input.TrySetEncodedControls((sbyte)(i % 400 < 200 ? 127 : -127), 127, (ushort)((i / 400 * 3700) % 36000), 0);
                inputs.Store(input.Tick, input);
                state = PlayerMovementSimulation.Simulate(state, input, config, context.TickDeltaTime, backend);
                states.Store(state.Tick, state);
                if (state.VelocityFixed.Y == Fix.Zero || state.VelocityFixed.X == Fix.Zero || state.VelocityFixed.Z == Fix.Zero)
                {
                    contacts++;
                }

                if (!AuthoritativeMovementStateCodec.TrySerialize(new AuthoritativeMovementState((uint)i, state.Tick, state), (uint)i, new ArraySegment<byte>(packet), out _))
                {
                    throw new Exception("Serialize failed");
                }

                if (!AuthoritativeMovementStateCodec.TryDeserialize(new ArraySegment<byte>(packet), out _, out var authority) || !state.RawEquals(authority.State))
                {
                    throw new Exception("Raw packet mismatch");
                }

                if (i > 8 && i % 43 == 0)
                {
                    uint ack = unchecked(state.Tick - 7);
                    states.TryGet(ack, out var anchor);
                    var corrupt = anchor;
                    corrupt.PositionFixed += new FixVec3(Fix.Epsilon, Fix.Zero, Fix.Zero);
                    states.Store(ack, corrupt);
                    var result = MovementPredictionReconciler.Reconcile(
                        new AuthoritativeMovementState((uint)i, ack, anchor),
                        state.Tick,
                        state,
                        inputs,
                        states,
                        config,
                        context.TickDeltaTime,
                        backend);
                    if (result.Status != MovementReconciliationStatus.Corrected || !result.State.RawEquals(state))
                    {
                        throw new Exception("Geometry rollback mismatch");
                    }
                }

                ulong raw = PlayerMovementStateHash.Compute(state);
                Add(ref hash, unchecked((int)raw));
                Add(ref hash, unchecked((int)(raw >> 32)));
            }

            if (contacts < 100)
            {
                throw new Exception("Fixture did not exercise sufficient collisions");
            }

            return hash;
        }

        private static void Add(ref ulong hash, int value)
        {
            uint bits = unchecked((uint)value);
            for (int shift = 0; shift < 32; shift += 8)
            {
                hash = unchecked((hash ^ ((bits >> shift) & 255u)) * 1099511628211UL);
            }
        }
    }
}

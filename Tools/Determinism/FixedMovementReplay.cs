using System;
using AFPS.Math;
using AFPS.Core.Collections;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Prediction;
using AFPS.Simulation.Characters;
using AFPS.Simulation.Characters.Collision;

internal static class FixedMovementReplay
{
    private static int Main()
    {
        var config = new PlayerSimulationConfig(Fix.FromInt(6), Fix.FromInt(20), Fix.FromInt(20), Fix.FromInt(8));
        var world = FixedFlatGroundCollisionWorld.Instance;
        var state = new PlayerState
        {
            Tick = uint.MaxValue - 2048,
            IsGrounded = true
        };
        var random = new FixRandom(20261022);
        var inputs = new TickBuffer<PlayerInputCommand>(128);
        var states = new TickBuffer<PlayerState>(128);
        var packet = new byte[AuthoritativePlayerStateCodec.PacketSize];
        ulong hash = 14695981039346656037UL;
        for (int i = 0; i < 8192; i++)
        {
            uint tick = unchecked(state.Tick + 1);
            var input = new PlayerInputCommand
            {
                Tick = tick,
                JumpPressed = i % 71 == 0
            };
            input.TrySetEncodedControls((sbyte)random.NextInt(-127, 128), (sbyte)random.NextInt(-127, 128), (ushort)random.NextInt(0, 36000), (short)random.NextInt(-8990, 8991));
            inputs.Store(tick, input);
            state = PlayerSimulation.Simulate(state, input, config, SimulationStep.Step50Hz, world);
            states.Store(tick, state);
            if (!AuthoritativePlayerStateCodec.TrySerialize(new AuthoritativePlayerState((uint)i, tick, state), (uint)i, new ArraySegment<byte>(packet), out _))
            {
                throw new Exception("Serialize failed");
            }

            if (!AuthoritativePlayerStateCodec.TryDeserialize(new ArraySegment<byte>(packet), out _, out var authority) || !state.RawEquals(authority.State))
            {
                throw new Exception("Raw roundtrip mismatch");
            }

            if (i > 8 && i % 43 == 0)
            {
                uint ack = unchecked(tick - 7);
                states.TryGet(ack, out var anchor);
                var corrupted = anchor;
                corrupted.PositionFixed += new FixVec3(Fix.Epsilon, Fix.Zero, Fix.Zero);
                states.Store(ack, corrupted);
                var result = ClientPredictionReconciler.Reconcile(new AuthoritativePlayerState((uint)i, ack, anchor), tick, state, inputs, states, config, SimulationStep.Step50Hz, world);
                if (result.Status != ReconciliationStatus.Corrected || result.ReplayedTickCount != 7 || !result.State.RawEquals(state))
                {
                    throw new Exception("Rollback replay mismatch");
                }
            }

            ulong raw = PlayerStateHash.Compute(state);
            for (int shift = 0; shift < 64; shift += 8)
            {
                hash = unchecked((hash ^ ((raw >> shift) & 255UL)) * 1099511628211UL);
            }
        }

        Console.WriteLine($"FixedMovementReplay={hash}; Ticks=8192; AuthorityBytes={packet.Length}; TickWrap=True; Rollback=True");
        if (hash != 2055156473884149906UL)
        {
            throw new Exception("Movement replay regression hash changed");
        }

        return 0;
    }
}

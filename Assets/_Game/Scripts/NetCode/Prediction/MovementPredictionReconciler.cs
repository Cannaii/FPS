using System;
using AFPS.Math;
using AFPS.Core.Collections;
using AFPS.NetCode.Messages;
using AFPS.Simulation.Characters;
using AFPS.Simulation.Characters.Collision;

namespace AFPS.NetCode.Prediction
{
    public static partial class MovementPredictionReconciler
    {
        public static MovementReconciliationResult Reconcile(
            in AuthoritativeMovementState authoritative,
            uint currentTick,
            in PlayerMovementState currentState,
            TickBuffer<PlayerInputCommand> inputs,
            TickBuffer<PlayerMovementState> states,
            in PlayerMovementConfig config,
            Fix dt,
            IFixedCharacterCollisionWorld world)
        {
            if (inputs == null)
            {
                throw new ArgumentNullException(nameof(inputs));
            }

            if (states == null)
            {
                throw new ArgumentNullException(nameof(states));
            }

            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            if (dt.Raw <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(dt));
            }

            if (currentState.Tick != currentTick || authoritative.State.Tick != authoritative.LastProcessedInputTick)
            {
                throw new ArgumentException("State Tick mismatch.");
            }

            // 用有符号差解释 uint Tick 回绕；历史窗口必须小于半个 uint 周期。
            int count = unchecked((int)(currentTick - authoritative.LastProcessedInputTick));
            if (count < 0)
            {
                throw new ArgumentException("Acknowledged Tick must not be newer than the client.");
            }

            uint acknowledged = authoritative.LastProcessedInputTick;
            if (!states.TryGet(acknowledged, out PlayerMovementState predicted))
            {
                return new MovementReconciliationResult(MovementReconciliationStatus.MissingPredictionHistory, authoritative.State, default, 0);
            }

            MovementPredictionError error = MovementPredictionComparer.Compare(predicted, authoritative);
            if (!error.RawMismatch)
            {
                return new MovementReconciliationResult(MovementReconciliationStatus.NoCorrection, currentState, error, 0);
            }

            // 先检查完整输入窗口，再写回任何历史，缺输入时避免留下半段重放状态。
            for (int i = 1; i <= count; i++)
            {
                if (!inputs.TryGet(unchecked(acknowledged + (uint)i), out _))
                {
                    return new MovementReconciliationResult(MovementReconciliationStatus.MissingInputHistory, authoritative.State, error, 0);
                }
            }

            // 权威状态已经包含确认 Tick，重放从下一 Tick 开始，不能重复执行确认输入。
            PlayerMovementState replay = authoritative.State;
            states.Store(acknowledged, replay);
            for (int i = 1; i <= count; i++)
            {
                uint tick = unchecked(acknowledged + (uint)i);
                inputs.TryGet(tick, out PlayerInputCommand input);
                replay = PlayerMovementSimulation.Simulate(replay, input, config, dt, world);
                states.Store(tick, replay);
            }

            return new MovementReconciliationResult(MovementReconciliationStatus.Corrected, replay, error, count);
        }
    }
}

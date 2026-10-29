using AFPS.Core.Collections;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Prediction;
using AFPS.Simulation.Characters;
using NUnit.Framework;
using UnityEngine;

namespace AFPS.Tests.EditMode
{
    /// <summary>
    /// 验证客户端预测校正、历史输入重放和历史缺失处理。
    /// </summary>
    public class MovementPredictionReconcilerTests
    {
        private const float TickDeltaTime = 0.02f;

        /// <summary>
        /// 验证同 Tick 状态一致时不会执行重放。
        /// </summary>
        [Test]
        public void Reconcile_NoMeaningfulError_KeepsCurrentState()
        {
            CreatePredictionHistory(3, out TickBuffer<PlayerInputCommand> inputs, out TickBuffer<PlayerMovementState> states, out PlayerMovementState currentState, out PlayerMovementConfig config);
            states.TryGet(1, out PlayerMovementState serverState);
            AuthoritativeMovementState authoritativeState = new AuthoritativeMovementState(10, 1, serverState);

            MovementReconciliationResult result = MovementPredictionReconciler.Reconcile(
                authoritativeState,
                3,
                currentState,
                inputs,
                states,
                config,
                TickDeltaTime,
                0.001f,
                0.001f,
                global::AFPS.Simulation.Characters.Collision.FlatGroundCollisionWorld.Instance
            );

            Assert.AreEqual(MovementReconciliationStatus.NoCorrection, result.Status);
            Assert.AreEqual(0, result.ReplayedTickCount);
            Assert.AreEqual(currentState.Position, result.State.Position);
        }

        /// <summary>
        /// 验证出现误差时会恢复权威状态并重放后续输入到当前 Tick。
        /// </summary>
        [Test]
        public void Reconcile_MeaningfulError_ReplaysInputsToCurrentTick()
        {
            CreatePredictionHistory(3, out TickBuffer<PlayerInputCommand> inputs, out TickBuffer<PlayerMovementState> states, out PlayerMovementState currentState, out PlayerMovementConfig config);
            states.TryGet(1, out PlayerMovementState predictedAtTickOne);

            PlayerMovementState serverState = predictedAtTickOne;
            serverState.Position += new Vector3(0.25f, 0f, 0f);
            AuthoritativeMovementState authoritativeState = new AuthoritativeMovementState(10, 1, serverState);

            inputs.TryGet(2, out PlayerInputCommand inputTwo);
            inputs.TryGet(3, out PlayerInputCommand inputThree);
            PlayerMovementState expectedState = PlayerMovementSimulation.Simulate(
                serverState,
                inputTwo,
                config,
                TickDeltaTime,
                global::AFPS.Simulation.Characters.Collision.FlatGroundCollisionWorld.Instance
            );
            expectedState = PlayerMovementSimulation.Simulate(
                expectedState,
                inputThree,
                config,
                TickDeltaTime,
                global::AFPS.Simulation.Characters.Collision.FlatGroundCollisionWorld.Instance
            );

            MovementReconciliationResult result = MovementPredictionReconciler.Reconcile(
                authoritativeState,
                3,
                currentState,
                inputs,
                states,
                config,
                TickDeltaTime,
                0.001f,
                0.001f,
                global::AFPS.Simulation.Characters.Collision.FlatGroundCollisionWorld.Instance
            );

            Assert.AreEqual(MovementReconciliationStatus.Corrected, result.Status);
            Assert.AreEqual(2, result.ReplayedTickCount);
            Assert.AreEqual(expectedState.Position, result.State.Position);
            Assert.AreEqual(expectedState.Velocity, result.State.Velocity);
            Assert.AreEqual(3u, result.State.Tick);
            Assert.IsTrue(states.TryGet(3, out PlayerMovementState rewrittenState));
            Assert.AreEqual(expectedState.Position, rewrittenState.Position);
        }

        /// <summary>
        /// 验证重放所需输入缺失时返回硬校正要求，不产生不完整的重放状态。
        /// </summary>
        [Test]
        public void Reconcile_MissingReplayInput_RequiresHardCorrection()
        {
            CreatePredictionHistory(3, out TickBuffer<PlayerInputCommand> inputs, out TickBuffer<PlayerMovementState> states, out PlayerMovementState currentState, out PlayerMovementConfig config);
            states.TryGet(1, out PlayerMovementState predictedAtTickOne);
            PlayerMovementState serverState = predictedAtTickOne;
            serverState.Position += Vector3.right;
            AuthoritativeMovementState authoritativeState = new AuthoritativeMovementState(10, 1, serverState);

            inputs.Clear();

            MovementReconciliationResult result = MovementPredictionReconciler.Reconcile(
                authoritativeState,
                3,
                currentState,
                inputs,
                states,
                config,
                TickDeltaTime,
                0.001f,
                0.001f,
                global::AFPS.Simulation.Characters.Collision.FlatGroundCollisionWorld.Instance
            );

            Assert.AreEqual(MovementReconciliationStatus.MissingInputHistory, result.Status);
            Assert.IsTrue(result.RequiresHardCorrection);
            Assert.AreEqual(serverState.Position, result.State.Position);
        }

        private static void CreatePredictionHistory(
            uint lastTick,
            out TickBuffer<PlayerInputCommand> inputs,
            out TickBuffer<PlayerMovementState> states,
            out PlayerMovementState currentState,
            out PlayerMovementConfig config)
        {
            inputs = new TickBuffer<PlayerInputCommand>(16);
            states = new TickBuffer<PlayerMovementState>(16);
            config = new PlayerMovementConfig(6f, 20f, 20f, 8f);
            currentState = new PlayerMovementState { Tick = 0, IsGrounded = true };
            states.Store(0, currentState);

            for (uint tick = 1; tick <= lastTick; tick++)
            {
                PlayerInputCommand input = new PlayerInputCommand { Tick = tick, MoveY = 1f };
                inputs.Store(tick, input);
                currentState = PlayerMovementSimulation.Simulate(
                    currentState,
                    input,
                    config,
                    TickDeltaTime,
                    global::AFPS.Simulation.Characters.Collision.FlatGroundCollisionWorld.Instance
                );
                states.Store(tick, currentState);
            }
        }
    }
}

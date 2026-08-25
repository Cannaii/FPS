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
    public class ClientPredictionReconcilerTests
    {
        private const float TickDeltaTime = 0.02f;

        /// <summary>
        /// 验证同 Tick 状态一致时不会执行重放。
        /// </summary>
        [Test]
        public void Reconcile_NoMeaningfulError_KeepsCurrentState()
        {
            CreatePredictionHistory(3, out TickBuffer<PlayerInputCommand> inputs, out TickBuffer<PlayerState> states, out PlayerState currentState, out PlayerSimulationConfig config);
            states.TryGet(1, out PlayerState serverState);
            AuthoritativePlayerState authoritativeState = new AuthoritativePlayerState(10, 1, serverState);

            ReconciliationResult result = ClientPredictionReconciler.Reconcile(
                authoritativeState,
                3,
                currentState,
                inputs,
                states,
                config,
                TickDeltaTime,
                0.001f,
                0.001f);

            Assert.AreEqual(ReconciliationStatus.NoCorrection, result.Status);
            Assert.AreEqual(0, result.ReplayedTickCount);
            Assert.AreEqual(currentState.Position, result.State.Position);
        }

        /// <summary>
        /// 验证出现误差时会恢复权威状态并重放后续输入到当前 Tick。
        /// </summary>
        [Test]
        public void Reconcile_MeaningfulError_ReplaysInputsToCurrentTick()
        {
            CreatePredictionHistory(3, out TickBuffer<PlayerInputCommand> inputs, out TickBuffer<PlayerState> states, out PlayerState currentState, out PlayerSimulationConfig config);
            states.TryGet(1, out PlayerState predictedAtTickOne);

            PlayerState serverState = predictedAtTickOne;
            serverState.Position += new Vector3(0.25f, 0f, 0f);
            AuthoritativePlayerState authoritativeState = new AuthoritativePlayerState(10, 1, serverState);

            inputs.TryGet(2, out PlayerInputCommand inputTwo);
            inputs.TryGet(3, out PlayerInputCommand inputThree);
            PlayerState expectedState = PlayerSimulation.Simulate(serverState, inputTwo, config, TickDeltaTime);
            expectedState = PlayerSimulation.Simulate(expectedState, inputThree, config, TickDeltaTime);

            ReconciliationResult result = ClientPredictionReconciler.Reconcile(
                authoritativeState,
                3,
                currentState,
                inputs,
                states,
                config,
                TickDeltaTime,
                0.001f,
                0.001f);

            Assert.AreEqual(ReconciliationStatus.Corrected, result.Status);
            Assert.AreEqual(2, result.ReplayedTickCount);
            Assert.AreEqual(expectedState.Position, result.State.Position);
            Assert.AreEqual(expectedState.Velocity, result.State.Velocity);
            Assert.AreEqual(3u, result.State.Tick);
            Assert.IsTrue(states.TryGet(3, out PlayerState rewrittenState));
            Assert.AreEqual(expectedState.Position, rewrittenState.Position);
        }

        /// <summary>
        /// 验证重放所需输入缺失时返回硬校正要求，不产生不完整的重放状态。
        /// </summary>
        [Test]
        public void Reconcile_MissingReplayInput_RequiresHardCorrection()
        {
            CreatePredictionHistory(3, out TickBuffer<PlayerInputCommand> inputs, out TickBuffer<PlayerState> states, out PlayerState currentState, out PlayerSimulationConfig config);
            states.TryGet(1, out PlayerState predictedAtTickOne);
            PlayerState serverState = predictedAtTickOne;
            serverState.Position += Vector3.right;
            AuthoritativePlayerState authoritativeState = new AuthoritativePlayerState(10, 1, serverState);

            inputs.Clear();

            ReconciliationResult result = ClientPredictionReconciler.Reconcile(
                authoritativeState,
                3,
                currentState,
                inputs,
                states,
                config,
                TickDeltaTime,
                0.001f,
                0.001f);

            Assert.AreEqual(ReconciliationStatus.MissingInputHistory, result.Status);
            Assert.IsTrue(result.RequiresHardCorrection);
            Assert.AreEqual(serverState.Position, result.State.Position);
        }

        private static void CreatePredictionHistory(
            uint lastTick,
            out TickBuffer<PlayerInputCommand> inputs,
            out TickBuffer<PlayerState> states,
            out PlayerState currentState,
            out PlayerSimulationConfig config)
        {
            inputs = new TickBuffer<PlayerInputCommand>(16);
            states = new TickBuffer<PlayerState>(16);
            config = new PlayerSimulationConfig(6f, 20f, 20f, 8f);
            currentState = new PlayerState { Tick = 0, IsGrounded = true };
            states.Store(0, currentState);

            for (uint tick = 1; tick <= lastTick; tick++)
            {
                PlayerInputCommand input = new PlayerInputCommand { Tick = tick, MoveY = 1f };
                inputs.Store(tick, input);
                currentState = PlayerSimulation.Simulate(currentState, input, config, TickDeltaTime);
                states.Store(tick, currentState);
            }
        }
    }
}

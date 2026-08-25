using System;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Prediction;
using AFPS.Simulation.Characters;
using NUnit.Framework;
using UnityEngine;

namespace AFPS.Tests.EditMode
{
    /// <summary>
    /// 验证客户端只能使用相同输入 Tick 的历史状态计算预测误差。
    /// </summary>
    public class ClientPredictionComparerTests
    {
        /// <summary>
        /// 验证完全相同的同 Tick 状态不会产生预测误差。
        /// </summary>
        [Test]
        public void Compare_IdenticalStateAtSameTick_ReturnsZeroError()
        {
            PlayerState state = new PlayerState
            {
                Tick = 10,
                Position = new Vector3(1f, 0f, 2f),
                Velocity = new Vector3(0.5f, 0f, 1f),
                IsGrounded = true
            };

            AuthoritativePlayerState authoritativeState = new AuthoritativePlayerState(100, 10, state);
            PredictionError result = ClientPredictionComparer.Compare(state, authoritativeState);

            Assert.AreEqual(10u, result.InputTick);
            Assert.AreEqual(0f, result.Position);
            Assert.AreEqual(0f, result.Velocity);
            Assert.IsFalse(result.GroundedMismatch);
            Assert.IsFalse(result.Exceeds(0.001f, 0.001f));
        }

        /// <summary>
        /// 验证位置、速度和落地状态差异能够被正确计算。
        /// </summary>
        [Test]
        public void Compare_DifferentStateAtSameTick_ReturnsMeasuredError()
        {
            PlayerState predictedState = new PlayerState
            {
                Tick = 20,
                Position = Vector3.zero,
                Velocity = Vector3.zero,
                IsGrounded = true
            };

            PlayerState serverState = new PlayerState
            {
                Tick = 20,
                Position = new Vector3(0.3f, 0f, 0.4f),
                Velocity = new Vector3(0f, 0f, 2f),
                IsGrounded = false
            };

            AuthoritativePlayerState authoritativeState = new AuthoritativePlayerState(200, 20, serverState);
            PredictionError result = ClientPredictionComparer.Compare(predictedState, authoritativeState);

            Assert.AreEqual(0.5f, result.Position, 0.0001f);
            Assert.AreEqual(2f, result.Velocity, 0.0001f);
            Assert.IsTrue(result.GroundedMismatch);
            Assert.IsTrue(result.Exceeds(0.01f, 0.01f));
        }

        /// <summary>
        /// 验证不同输入 Tick 的状态不能被直接比较。
        /// </summary>
        [Test]
        public void Compare_DifferentTicks_ThrowsArgumentException()
        {
            PlayerState predictedState = new PlayerState { Tick = 11 };
            PlayerState serverState = new PlayerState { Tick = 10 };
            AuthoritativePlayerState authoritativeState = new AuthoritativePlayerState(100, 10, serverState);

            Assert.Throws<ArgumentException>(() => ClientPredictionComparer.Compare(predictedState, authoritativeState));
        }
    }
}

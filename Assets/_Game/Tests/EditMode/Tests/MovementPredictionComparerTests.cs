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
    public class MovementPredictionComparerTests
    {
        /// <summary>
        /// 验证完全相同的同 Tick 状态不会产生预测误差。
        /// </summary>
        [Test]
        public void Compare_IdenticalStateAtSameTick_ReturnsZeroError()
        {
            PlayerMovementState state = new PlayerMovementState
            {
                Tick = 10,
                Position = new Vector3(1f, 0f, 2f),
                Velocity = new Vector3(0.5f, 0f, 1f),
                IsGrounded = true
            };

            AuthoritativeMovementState authoritativeState = new AuthoritativeMovementState(100, 10, state);
            MovementPredictionError result = MovementPredictionComparer.Compare(state, authoritativeState);

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
            PlayerMovementState predictedState = new PlayerMovementState
            {
                Tick = 20,
                Position = Vector3.zero,
                Velocity = Vector3.zero,
                Yaw = 359f,
                IsGrounded = true
            };

            PlayerMovementState serverState = new PlayerMovementState
            {
                Tick = 20,
                Position = new Vector3(0.3f, 0f, 0.4f),
                Velocity = new Vector3(0f, 0f, 2f),
                Yaw = 1f,
                IsGrounded = false
            };

            AuthoritativeMovementState authoritativeState = new AuthoritativeMovementState(200, 20, serverState);
            MovementPredictionError result = MovementPredictionComparer.Compare(predictedState, authoritativeState);

            Assert.AreEqual(0.5f, result.Position, 0.0001f);
            Assert.AreEqual(2f, result.Velocity, 0.0001f);
            Assert.AreEqual(2f, result.LookAngle, 0.0001f);
            Assert.IsTrue(result.GroundedMismatch);
            Assert.IsTrue(result.Exceeds(0.01f, 0.01f));
        }

        /// <summary>
        /// 验证不同输入 Tick 的状态不能被直接比较。
        /// </summary>
        [Test]
        public void Compare_DifferentTicks_ThrowsArgumentException()
        {
            PlayerMovementState predictedState = new PlayerMovementState { Tick = 11 };
            PlayerMovementState serverState = new PlayerMovementState { Tick = 10 };
            AuthoritativeMovementState authoritativeState = new AuthoritativeMovementState(100, 10, serverState);

            Assert.Throws<ArgumentException>(() => MovementPredictionComparer.Compare(predictedState, authoritativeState));
        }
    }
}

using AFPS.NetCode.Messages;
using AFPS.NetCode.Simulation;
using AFPS.Simulation.Characters;
using NUnit.Framework;
using UnityEngine;

namespace AFPS.Tests.EditMode
{
    /// <summary>
    /// 验证单进程模拟服务器的独立模拟和固定 Tick 延迟行为。
    /// </summary>
    public class SimulatedAuthoritativeServerTests
    {
        /// <summary>
        /// 验证输入经过上行延迟后才被服务器处理，
        /// 权威状态再经过下行延迟后才返回客户端。
        /// </summary>
        [Test]
        public void DelayedInput_ProducesAuthoritativeStateAfterRoundTripDelay()
        {
            PlayerState initialState = new PlayerState
            {
                Tick = 0,
                Position = Vector3.zero,
                Velocity = Vector3.zero,
                IsGrounded = true
            };

            PlayerSimulationConfig config = new PlayerSimulationConfig(6f, 20f, 20f, 8f);
            SimulatedAuthoritativeServer server = new SimulatedAuthoritativeServer(initialState, config, 0.02f, 2, 2);

            PlayerInputCommand command = new PlayerInputCommand
            {
                Tick = 1,
                MoveX = 0f,
                MoveY = 1f,
                JumpPressed = false
            };

            server.SendInput(1, command);

            server.Advance(1);
            Assert.IsFalse(server.TryReceiveState(1, out _));
            Assert.AreEqual(0u, server.CurrentState.Tick);

            server.Advance(2);
            Assert.IsFalse(server.TryReceiveState(2, out _));
            Assert.AreEqual(0u, server.CurrentState.Tick);

            server.Advance(3);
            Assert.IsFalse(server.TryReceiveState(3, out _));
            Assert.AreEqual(1u, server.CurrentState.Tick);

            server.Advance(4);
            Assert.IsFalse(server.TryReceiveState(4, out _));

            server.Advance(5);
            Assert.IsTrue(server.TryReceiveState(5, out AuthoritativePlayerState result));
            Assert.AreEqual(3u, result.ServerTick);
            Assert.AreEqual(1u, result.LastProcessedInputTick);
            Assert.AreEqual(1u, result.State.Tick);
            Assert.AreEqual(0.4f, result.State.Velocity.z, 0.0001f);
            Assert.AreEqual(0.008f, result.State.Position.z, 0.0001f);
        }

        /// <summary>
        /// 验证零延迟配置下，输入可以在当前网络 Tick 被处理并立即返回。
        /// </summary>
        [Test]
        public void ZeroDelay_ReturnsAuthoritativeStateImmediately()
        {
            PlayerState initialState = new PlayerState
            {
                Tick = 0,
                Position = Vector3.zero,
                Velocity = Vector3.zero,
                IsGrounded = true
            };

            PlayerSimulationConfig config = new PlayerSimulationConfig(6f, 20f, 20f, 8f);
            SimulatedAuthoritativeServer server = new SimulatedAuthoritativeServer(initialState, config, 0.02f, 0, 0);
            PlayerInputCommand command = new PlayerInputCommand { Tick = 1, MoveY = 1f };

            server.SendInput(1, command);
            server.Advance(1);

            Assert.IsTrue(server.TryReceiveState(1, out AuthoritativePlayerState result));
            Assert.AreEqual(1u, result.ServerTick);
            Assert.AreEqual(1u, result.LastProcessedInputTick);
            Assert.AreEqual(1u, result.State.Tick);
        }
    }
}

using AFPS.Simulation.Characters;
using NUnit.Framework;
using UnityEngine;

namespace AFPS.Tests.EditMode
{
    /// <summary>
    /// 验证玩家移动模拟在固定输入和固定 Tick 时长下的计算结果。
    /// </summary>
    public class PlayerSimulationTests
    {
        /// <summary>
        /// 验证玩家向前输入一个 Tick 后，
        /// 速度会根据地面加速度增加，位置会根据新速度发生变化。
        /// </summary>
        [Test]
        public void Simulate_ForwardInput_AcceleratesAndMovesPlayerForward()
        {
            var previousState = new PlayerState
            {
                Tick = 0,
                Position = Vector3.zero,
                Velocity = Vector3.zero,
                IsGrounded = true
            };

            var input = new PlayerInputCommand
            {
                Tick = 1,
                MoveX = 0f,
                MoveY = 1f,
                JumpPressed = false
            };

            var config = new PlayerSimulationConfig(
                maxGroundSpeed: 6f,
                groundAcceleration: 20f,
                gravity: 20f,
                jumpSpeed: 8f);

            var tickDeltaTime = 0.02f;

            var result = PlayerSimulation.Simulate(
                previousState,
                input,
                config,
                tickDeltaTime);

            Assert.AreEqual(1u, result.Tick);

            // 速度变化 = 加速度 × 时间 = 20 × 0.02 = 0.4
            Assert.AreEqual(
                0.4f,
                result.Velocity.z,
                0.0001f);

            // 位移 = 速度 × 时间 = 0.4 × 0.02 = 0.008
            Assert.AreEqual(
                0.008f,
                result.Position.z,
                0.0001f);

            Assert.AreEqual(
                0f,
                result.Position.x,
                0.0001f);

            Assert.AreEqual(
                0f,
                result.Position.y,
                0.0001f);
        }

        /// <summary>
        /// 验证斜向输入经过长度限制后，
        /// 不会产生比单方向输入更大的水平速度。
        /// </summary>
        [Test]
        public void Simulate_DiagonalInput_DoesNotExceedStraightInputSpeed()
        {
            var initialState = new PlayerState
            {
                Tick = 0,
                Position = Vector3.zero,
                Velocity = Vector3.zero,
                IsGrounded = true
            };

            var config = new PlayerSimulationConfig(
                maxGroundSpeed: 6f,
                groundAcceleration: 20f,
                gravity: 20f,
                jumpSpeed: 8f);

            var forwardInput = new PlayerInputCommand
            {
                Tick = 1,
                MoveX = 0f,
                MoveY = 1f,
                JumpPressed = false
            };

            var diagonalInput = new PlayerInputCommand
            {
                Tick = 1,
                MoveX = 1f,
                MoveY = 1f,
                JumpPressed = false
            };

            var tickDeltaTime = 0.02f;

            var forwardResult = PlayerSimulation.Simulate(
                initialState,
                forwardInput,
                config,
                tickDeltaTime);

            var diagonalResult = PlayerSimulation.Simulate(
                initialState,
                diagonalInput,
                config,
                tickDeltaTime);

            var forwardVelocity = new Vector2(
                forwardResult.Velocity.x,
                forwardResult.Velocity.z);

            var diagonalVelocity = new Vector2(
                diagonalResult.Velocity.x,
                diagonalResult.Velocity.z);

            Assert.AreEqual(
                forwardVelocity.magnitude,
                diagonalVelocity.magnitude,
                0.0001f);
        }

        /// <summary>
        /// 验证从相同初始状态出发，使用相同配置和相同输入序列进行两次模拟，
        /// 最终会得到一致的 Tick、位置和速度。
        /// </summary>
        [Test]
        public void Simulate_SameInputSequence_ProducesSameResult()
        {
            PlayerState initialState = new PlayerState
            {
                Tick = 0,
                Position = Vector3.zero,
                Velocity = Vector3.zero,
                IsGrounded = true
            };

            PlayerSimulationConfig config = new PlayerSimulationConfig(
                maxGroundSpeed: 6f,
                groundAcceleration: 20f,
                gravity: 20f,
                jumpSpeed: 8f);

            PlayerInputCommand[] inputs =
            {
                new PlayerInputCommand
                {
                    Tick = 1,
                    MoveX = 0f,
                    MoveY = 1f,
                    JumpPressed = false
                },
                new PlayerInputCommand
                {
                    Tick = 2,
                    MoveX = 1f,
                    MoveY = 1f,
                    JumpPressed = false
                },
                new PlayerInputCommand
                {
                    Tick = 3,
                    MoveX = -1f,
                    MoveY = 0f,
                    JumpPressed = false
                },
                new PlayerInputCommand
                {
                    Tick = 4,
                    MoveX = 0f,
                    MoveY = 0f,
                    JumpPressed = false
                }
            };

            const float tickDeltaTime = 0.02f;

            PlayerState firstResult = initialState;
            PlayerState secondResult = initialState;

            // 第一次播放输入序列。
            foreach (PlayerInputCommand input in inputs)
            {
                firstResult = PlayerSimulation.Simulate(
                    firstResult,
                    input,
                    config,
                    tickDeltaTime);
            }

            // 从相同初始状态重新播放同一组输入。
            foreach (PlayerInputCommand input in inputs)
            {
                secondResult = PlayerSimulation.Simulate(
                    secondResult,
                    input,
                    config,
                    tickDeltaTime);
            }

            Assert.AreEqual(firstResult.Tick, secondResult.Tick);

            Assert.AreEqual(
                firstResult.Position.x,
                secondResult.Position.x,
                0.0001f);

            Assert.AreEqual(
                firstResult.Position.y,
                secondResult.Position.y,
                0.0001f);

            Assert.AreEqual(
                firstResult.Position.z,
                secondResult.Position.z,
                0.0001f);

            Assert.AreEqual(
                firstResult.Velocity.x,
                secondResult.Velocity.x,
                0.0001f);

            Assert.AreEqual(
                firstResult.Velocity.y,
                secondResult.Velocity.y,
                0.0001f);

            Assert.AreEqual(
                firstResult.Velocity.z,
                secondResult.Velocity.z,
                0.0001f);

            Assert.AreEqual(
                firstResult.IsGrounded,
                secondResult.IsGrounded);
        }

        /// <summary>
        /// 验证站在地面上的玩家按下跳跃后会离开地面，
        /// 并获得经过一个 Tick 重力修正后的向上速度。
        /// </summary>
        [Test]
        public void Simulate_JumpFromGround_GainsUpwardVelocity()
        {
            PlayerState previousState = new PlayerState
            {
                Tick = 0,
                Position = Vector3.zero,
                Velocity = Vector3.zero,
                IsGrounded = true
            };

            PlayerInputCommand input = new PlayerInputCommand
            {
                Tick = 1,
                MoveX = 0f,
                MoveY = 0f,
                JumpPressed = true
            };

            PlayerSimulationConfig config = new PlayerSimulationConfig(
                maxGroundSpeed: 6f,
                groundAcceleration: 20f,
                gravity: 20f,
                jumpSpeed: 8f);

            const float tickDeltaTime = 0.02f;

            PlayerState result = PlayerSimulation.Simulate(
                previousState,
                input,
                config,
                tickDeltaTime);

            Assert.AreEqual(1u, result.Tick);
            Assert.IsFalse(result.IsGrounded);

            // 跳跃速度 - 一个 Tick 的重力
            // 8 - 20 × 0.02 = 7.6 米/秒
            Assert.AreEqual(
                7.6f,
                result.Velocity.y,
                0.0001f);

            // 位移 = 新速度 × Tick 时长
            // 7.6 × 0.02 = 0.152 米
            Assert.AreEqual(
                0.152f,
                result.Position.y,
                0.0001f);
        }

        /// <summary>
        /// 验证玩家处于空中时，再次按下跳跃键不会重置向上速度。
        /// </summary>
        [Test]
        public void Simulate_JumpPressedWhileAirborne_DoesNotJumpAgain()
        {
            PlayerState previousState = new PlayerState
            {
                Tick = 10,
                Position = new Vector3(0f, 2f, 0f),
                Velocity = new Vector3(0f, 3f, 0f),
                IsGrounded = false
            };

            PlayerInputCommand input = new PlayerInputCommand
            {
                Tick = 11,
                MoveX = 0f,
                MoveY = 0f,
                JumpPressed = true
            };

            PlayerSimulationConfig config = new PlayerSimulationConfig(
                maxGroundSpeed: 6f,
                groundAcceleration: 20f,
                gravity: 20f,
                jumpSpeed: 8f);

            const float tickDeltaTime = 0.02f;

            PlayerState result = PlayerSimulation.Simulate(
                previousState,
                input,
                config,
                tickDeltaTime);

            // 空中跳跃输入应被忽略，只应用重力：
            // 3 - 20 × 0.02 = 2.6 米/秒
            Assert.AreEqual(
                2.6f,
                result.Velocity.y,
                0.0001f);

            Assert.IsFalse(result.IsGrounded);
        }

        /// <summary>
        /// 验证玩家跳跃后经过足够数量的 Tick，
        /// 最终会回到 Y=0 的平地并清空垂直速度。
        /// </summary>
        [Test]
        public void Simulate_JumpSequence_EventuallyLandsOnGround()
        {
            PlayerState state = new PlayerState
            {
                Tick = 0,
                Position = Vector3.zero,
                Velocity = Vector3.zero,
                IsGrounded = true
            };

            PlayerSimulationConfig config = new PlayerSimulationConfig(
                maxGroundSpeed: 6f,
                groundAcceleration: 20f,
                gravity: 20f,
                jumpSpeed: 8f);

            const float tickDeltaTime = 0.02f;

            // 模拟 100 个 Tick，即两秒。
            for (uint tick = 1; tick <= 100; tick++)
            {
                PlayerInputCommand input = new PlayerInputCommand
                {
                    Tick = tick,
                    MoveX = 0f,
                    MoveY = 0f,

                    // 只在第一个 Tick 请求跳跃。
                    JumpPressed = tick == 1
                };

                state = PlayerSimulation.Simulate(
                    state,
                    input,
                    config,
                    tickDeltaTime);
            }

            Assert.AreEqual(100u, state.Tick);
            Assert.IsTrue(state.IsGrounded);

            Assert.AreEqual(
                0f,
                state.Position.y,
                0.0001f);

            Assert.AreEqual(
                0f,
                state.Velocity.y,
                0.0001f);
        }
    }
}

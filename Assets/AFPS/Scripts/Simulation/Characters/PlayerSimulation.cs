
using UnityEngine;
using AFPS.Simulation.Characters.Collision;

namespace AFPS.Simulation.Characters
{
    /// <summary>
    /// 玩家模拟
    /// </summary>
    public static class PlayerSimulation
    {
        
        /// <summary>
        /// 根据上一 Tick 的玩家状态、当前输入和固定配置，
        /// 计算完成当前 Tick 后的新玩家状态。
        /// </summary>
        /// <param name="previousState">上一 Tick 完成后的玩家状态。</param>
        /// <param name="input">当前 Tick 需要处理的玩家输入。</param>
        /// <param name="config">客户端与服务器共同使用的移动参数。</param>
        /// <param name="tickDeltaTime">单个模拟 Tick 的持续时间，单位为秒。</param>
        /// <returns>完成当前 Tick 模拟后得到的新玩家状态。</returns>
        public static PlayerState Simulate(
            in PlayerState previousState,
            in PlayerInputCommand input,
            in PlayerSimulationConfig config,
            float tickDeltaTime)
        {
            return Simulate(previousState, input, config, tickDeltaTime, FlatGroundCollisionWorld.Instance);
        }

        /// <summary>
        /// 使用调用方提供的碰撞场景执行可预测角色模拟。
        /// </summary>
        public static PlayerState Simulate(
            in PlayerState previousState,
            in PlayerInputCommand input,
            in PlayerSimulationConfig config,
            float tickDeltaTime,
            ICharacterCollisionWorld collisionWorld)
        {
            var nextState = previousState;
            nextState.Tick = input.Tick;
            nextState.Yaw = NormalizeYaw(input.LookYaw);
            nextState.Pitch = Mathf.Clamp(input.LookPitch, -89.9f, 89.9f);
            var moveInput = new Vector2(input.MoveX, input.MoveY);
            moveInput = Vector2.ClampMagnitude(moveInput, 1f);

            float yawRadians = nextState.Yaw * Mathf.Deg2Rad;
            Vector3 right = new Vector3(Mathf.Cos(yawRadians), 0f, -Mathf.Sin(yawRadians));
            Vector3 forward = new Vector3(Mathf.Sin(yawRadians), 0f, Mathf.Cos(yawRadians));
            var targetHorizontalVelocity = (right * moveInput.x + forward * moveInput.y) * config.MaxGroundSpeed;
            var currentHorizontalVelocity = new Vector3(previousState.Velocity.x, 0f, previousState.Velocity.z);

            // 计算当前 Tick 内允许改变的最大速度。
            // 加速度单位是米/秒²，乘以 Tick 时长后得到本 Tick 的速度变化量。
            var maxVelocityChange = config.GroundAcceleration * tickDeltaTime;

            // 从上一 Tick 状态中读取当前垂直速度。
            var verticalVelocity =
                previousState.Velocity.y;

            // 只有站在地面上时，跳跃输入才会生效。
            if (input.JumpPressed && previousState.IsGrounded)
            {
                verticalVelocity = config.JumpSpeed;
                nextState.IsGrounded = false;
            }

            // 玩家离开地面后，每个 Tick 都受到向下的重力加速度。
            if (!nextState.IsGrounded)
            {
                verticalVelocity -= config.Gravity * tickDeltaTime;
            }

            // 让当前水平速度逐渐接近目标水平速度。
            // 这样角色会产生加速和减速过程，而不是瞬间达到最大速度。
            var newHorizontalVelocity = Vector3.MoveTowards(currentHorizontalVelocity, targetHorizontalVelocity, maxVelocityChange);

            // 写入新的水平速度和经过跳跃、重力计算后的垂直速度。
            nextState.Velocity = new Vector3(newHorizontalVelocity.x, verticalVelocity, newHorizontalVelocity.z);

            // 胶囊扫掠使用半隐式欧拉得到的速度计算本 Tick 位移，再解析地面、墙壁、斜坡、台阶和天花板接触。
            KinematicCharacterMotor.Move(previousState.Position, nextState.Velocity, tickDeltaTime, config.Collision, collisionWorld, out nextState.Position, out nextState.Velocity, out nextState.IsGrounded);

            return nextState;
        }

        private static float NormalizeYaw(float yaw)
        {
            if (float.IsNaN(yaw) || float.IsInfinity(yaw))
            {
                return 0f;
            }

            yaw %= 360f;
            return yaw < 0f ? yaw + 360f : yaw;
        }

    }
}


using UnityEngine;

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
            var nextState = previousState;
            nextState.Tick = input.Tick;
            var moveInput = new Vector2(input.MoveX, input.MoveY);
            moveInput = Vector2.ClampMagnitude(moveInput, 1f);

            var targetHorizontalVelocity = new Vector3(moveInput.x * config.MaxGroundSpeed, 0f , moveInput.y * config.MaxGroundSpeed);
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

            // 使用包含水平和垂直分量的速度更新玩家位置。
            // 当前使用半隐式欧拉积分：先更新速度，再更新位置。
            nextState.Position = previousState.Position + nextState.Velocity * tickDeltaTime;

            // 当前实验暂时将世界坐标 Y = 0 视为无限平地。
            // 将来接入碰撞系统后，这部分会替换为实际地面检测。
            if (nextState.Position.y <= 0f)
            {
                nextState.Position = new Vector3(nextState.Position.x, 0f, nextState.Position.z);
                nextState.Velocity = new Vector3(nextState.Velocity.x, 0f, nextState.Velocity.z);
                nextState.IsGrounded = true;
            }

            return nextState;
        }

    }
}

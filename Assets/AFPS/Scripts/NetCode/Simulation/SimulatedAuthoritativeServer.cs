using System;
using System.Collections.Generic;
using AFPS.NetCode.Messages;
using AFPS.Simulation.Characters;

namespace AFPS.NetCode.Simulation
{
    /// <summary>
    /// 在单个进程中模拟独立的服务器权威玩家状态和固定 Tick 网络延迟。
    /// 该类型不使用真实网络，仅用于验证客户端预测与服务器校正流程。
    /// </summary>
    public sealed class SimulatedAuthoritativeServer
    {
        /// <summary>
        /// 等待到达服务器的客户端输入。
        /// </summary>
        private readonly struct ScheduledInput
        {
            public readonly uint DeliveryTick;
            public readonly PlayerInputCommand Command;

            public ScheduledInput(uint deliveryTick, in PlayerInputCommand command)
            {
                DeliveryTick = deliveryTick;
                Command = command;
            }
        }

        /// <summary>
        /// 等待返回客户端的服务器权威状态。
        /// </summary>
        private readonly struct ScheduledState
        {
            public readonly uint DeliveryTick;
            public readonly AuthoritativePlayerState State;

            public ScheduledState(uint deliveryTick, in AuthoritativePlayerState state)
            {
                DeliveryTick = deliveryTick;
                State = state;
            }
        }

        private readonly Queue<ScheduledInput> pendingInputs = new Queue<ScheduledInput>();
        private readonly Queue<ScheduledState> pendingStates = new Queue<ScheduledState>();
        private readonly PlayerSimulationConfig simulationConfig;
        private readonly float tickDeltaTime;
        private readonly uint inputDelayTicks;
        private readonly uint stateDelayTicks;
        private PlayerState currentState;

        /// <summary>
        /// 获取服务器当前已经推进完成的世界 Tick。
        /// </summary>
        public uint ServerWorldTick { get; private set; }

        /// <summary>
        /// 获取服务器当前持有的玩家权威状态。
        /// </summary>
        public PlayerState CurrentState => currentState;

        /// <summary>
        /// 创建一个具有独立玩家状态和固定网络延迟的模拟服务器。
        /// </summary>
        /// <param name="initialState">服务器开始运行时的玩家权威状态。</param>
        /// <param name="config">服务器移动模拟使用的固定配置。</param>
        /// <param name="tickDeltaTime">单个服务器模拟 Tick 的持续时间，单位为秒。</param>
        /// <param name="inputDelayTicks">客户端输入到达服务器前等待的 Tick 数。</param>
        /// <param name="stateDelayTicks">服务器状态返回客户端前等待的 Tick 数。</param>
        public SimulatedAuthoritativeServer(
            in PlayerState initialState,
            in PlayerSimulationConfig config,
            float tickDeltaTime,
            int inputDelayTicks,
            int stateDelayTicks)
        {
            if (tickDeltaTime <= 0f || float.IsNaN(tickDeltaTime) || float.IsInfinity(tickDeltaTime))
            {
                throw new ArgumentOutOfRangeException(nameof(tickDeltaTime), "Tick 时长必须是有限的正数。");
            }

            if (inputDelayTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(inputDelayTicks), "输入延迟不能小于零。");
            }

            if (stateDelayTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(stateDelayTicks), "状态延迟不能小于零。");
            }

            currentState = initialState;
            simulationConfig = config;
            this.tickDeltaTime = tickDeltaTime;
            this.inputDelayTicks = (uint)inputDelayTicks;
            this.stateDelayTicks = (uint)stateDelayTicks;
        }

        /// <summary>
        /// 将客户端输入放入上行延迟队列。
        /// </summary>
        /// <param name="networkTick">发送输入时使用的本地网络时间 Tick。</param>
        /// <param name="command">需要发送给服务器的玩家输入命令。</param>
        public void SendInput(uint networkTick, in PlayerInputCommand command)
        {
            uint deliveryTick = unchecked(networkTick + inputDelayTicks);
            pendingInputs.Enqueue(new ScheduledInput(deliveryTick, command));
        }

        /// <summary>
        /// 推进一个服务器世界 Tick，并处理已经到达服务器的所有输入。
        /// 每条已处理输入都会生成一条权威状态确认并进入下行延迟队列。
        /// </summary>
        /// <param name="networkTick">当前用于判断延迟消息是否到期的网络时间 Tick。</param>
        public void Advance(uint networkTick)
        {
            ServerWorldTick++;

            while (pendingInputs.Count > 0 && IsDue(pendingInputs.Peek().DeliveryTick, networkTick))
            {
                ScheduledInput scheduledInput = pendingInputs.Dequeue();
                currentState = PlayerSimulation.Simulate(currentState, scheduledInput.Command, simulationConfig, tickDeltaTime);

                AuthoritativePlayerState authoritativeState = new AuthoritativePlayerState(
                    ServerWorldTick,
                    scheduledInput.Command.Tick,
                    currentState);

                uint deliveryTick = unchecked(networkTick + stateDelayTicks);
                pendingStates.Enqueue(new ScheduledState(deliveryTick, authoritativeState));
            }
        }

        /// <summary>
        /// 尝试接收已经完成下行延迟的服务器权威状态。
        /// </summary>
        /// <param name="networkTick">当前用于判断消息是否到期的网络时间 Tick。</param>
        /// <param name="state">成功时返回最早到达的一条服务器权威状态。</param>
        /// <returns>当前是否存在一条可以交付给客户端的权威状态。</returns>
        public bool TryReceiveState(uint networkTick, out AuthoritativePlayerState state)
        {
            if (pendingStates.Count == 0 || !IsDue(pendingStates.Peek().DeliveryTick, networkTick))
            {
                state = default;
                return false;
            }

            state = pendingStates.Dequeue().State;
            return true;
        }

        /// <summary>
        /// 判断计划交付 Tick 是否已经到达。
        /// 该比较允许 uint Tick 在运行足够久后发生回绕。
        /// </summary>
        private static bool IsDue(uint deliveryTick, uint currentTick)
        {
            return unchecked((int)(currentTick - deliveryTick)) >= 0;
        }
    }
}

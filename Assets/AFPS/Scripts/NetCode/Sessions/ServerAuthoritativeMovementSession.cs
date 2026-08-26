using System;
using AFPS.NetCode.InputReplication;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.StateReplication;
using AFPS.NetCode.Transport;
using AFPS.Simulation.Characters;

namespace AFPS.NetCode.Sessions
{
    /// <summary>
    /// 组装单个已连接玩家的输入接收窗口、服务器权威模拟和状态发送。
    /// 每次服务器世界 Tick 最多处理一条连续客户端输入。
    /// </summary>
    public sealed class ServerAuthoritativeMovementSession
    {
        private readonly ServerInputCommandReceiver inputReceiver;
        private readonly ServerAuthoritativeStateSender stateSender;
        private readonly PlayerSimulationConfig simulationConfig;
        private readonly float tickDeltaTime;
        private readonly int maxMissingInputWaitTicks;
        private readonly int maxRepeatedMovementTicks;
        private bool hasAdvancedServerTick;
        private bool hasLastReceivedInput;
        private uint lastServerTick;
        private int missingInputWaitTicks;
        private int consecutiveSubstitutedInputTicks;
        private PlayerInputCommand lastReceivedInput;

        /// <summary>
        /// 服务器处理完最近一条客户端输入后持有的未量化权威状态。
        /// </summary>
        public PlayerState CurrentState { get; private set; }

        /// <summary>
        /// 最近一次推进尝试使用真实输入、替代输入还是仍在等待输入。
        /// </summary>
        public ServerInputAdvanceStatus LastAdvanceStatus { get; private set; }

        /// <summary>
        /// 最近一次真正推进权威模拟时使用的输入。等待输入期间保持上一次的值。
        /// </summary>
        public PlayerInputCommand LastAppliedInput { get; private set; }

        /// <summary>
        /// 当前缺失输入已经等待的服务器世界 Tick 数；收到真实连续输入后重置为零。
        /// </summary>
        public int MissingInputWaitTicks => missingInputWaitTicks;

        /// <summary>
        /// 当前连续使用替代输入推进模拟的 Tick 数；收到真实连续输入后重置为零。
        /// </summary>
        public int ConsecutiveSubstitutedInputTicks => consecutiveSubstitutedInputTicks;

        public ServerAuthoritativeMovementSession(IGameTransport transport, TransportConnectionId clientConnectionId, in PlayerState initialState, in PlayerSimulationConfig simulationConfig, float tickDeltaTime, int inputWindowCapacity, int maxMissingInputWaitTicks = 2, int maxRepeatedMovementTicks = 2)
        {
            if (tickDeltaTime <= 0f || float.IsNaN(tickDeltaTime) || float.IsInfinity(tickDeltaTime))
            {
                throw new ArgumentOutOfRangeException(nameof(tickDeltaTime));
            }

            if (maxMissingInputWaitTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxMissingInputWaitTicks), "缺失输入等待 Tick 数不能小于零。");
            }

            if (maxRepeatedMovementTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxRepeatedMovementTicks), "持续移动复用 Tick 数不能小于零。");
            }

            this.simulationConfig = simulationConfig;
            this.tickDeltaTime = tickDeltaTime;
            this.maxMissingInputWaitTicks = maxMissingInputWaitTicks;
            this.maxRepeatedMovementTicks = maxRepeatedMovementTicks;
            CurrentState = initialState;
            inputReceiver = new ServerInputCommandReceiver(unchecked(initialState.Tick + 1), inputWindowCapacity);
            stateSender = new ServerAuthoritativeStateSender(transport, clientConnectionId);
        }

        /// <summary>
        /// 解码并保存客户端输入包。命令只进入接收窗口，不会在网络事件回调中直接推进世界模拟。
        /// </summary>
        public bool TryReceiveInputPacket(ArraySegment<byte> packet, out InputBatchReceiveResult result)
        {
            return inputReceiver.TryReceivePacket(packet, out result);
        }

        /// <summary>
        /// 在一个新的服务器世界 Tick 中处理一条真实或安全替代输入，并发送对应权威确认。
        /// 缺失输入尚未达到等待上限时返回 false，服务器状态不会推进。
        /// </summary>
        public bool TryAdvance(uint serverWorldTick, out AuthoritativePlayerState authoritativeState, out AuthoritativeStateSendResult sendResult)
        {
            authoritativeState = default;
            sendResult = default;
            if (hasAdvancedServerTick && !SequenceMath.IsNewer(serverWorldTick, lastServerTick))
            {
                throw new ArgumentException("服务器世界 Tick 必须单调递增。", nameof(serverWorldTick));
            }

            hasAdvancedServerTick = true;
            lastServerTick = serverWorldTick;
            if (inputReceiver.TryDequeueNext(out PlayerInputCommand command))
            {
                hasLastReceivedInput = true;
                lastReceivedInput = command;
                missingInputWaitTicks = 0;
                consecutiveSubstitutedInputTicks = 0;
                LastAdvanceStatus = ServerInputAdvanceStatus.ReceivedInput;
                return SimulateAndSend(serverWorldTick, command, out authoritativeState, out sendResult);
            }

            if (missingInputWaitTicks < maxMissingInputWaitTicks)
            {
                missingInputWaitTicks++;
                LastAdvanceStatus = ServerInputAdvanceStatus.WaitingForInput;
                return false;
            }

            if (!inputReceiver.TryAdvancePastMissingCommand(out uint substitutedTick))
            {
                throw new InvalidOperationException("输入接收窗口状态在同一个服务器 Tick 内发生了意外变化。");
            }

            bool repeatContinuousInput = hasLastReceivedInput && consecutiveSubstitutedInputTicks < maxRepeatedMovementTicks;
            command = new PlayerInputCommand
            {
                Tick = substitutedTick,
                MoveX = repeatContinuousInput ? lastReceivedInput.MoveX : 0f,
                MoveY = repeatContinuousInput ? lastReceivedInput.MoveY : 0f,
                JumpPressed = false
            };
            consecutiveSubstitutedInputTicks++;
            LastAdvanceStatus = repeatContinuousInput ? ServerInputAdvanceStatus.RepeatedContinuousInput : ServerInputAdvanceStatus.NeutralFallback;
            return SimulateAndSend(serverWorldTick, command, out authoritativeState, out sendResult);
        }

        private bool SimulateAndSend(uint serverWorldTick, in PlayerInputCommand command, out AuthoritativePlayerState authoritativeState, out AuthoritativeStateSendResult sendResult)
        {
            LastAppliedInput = command;
            CurrentState = PlayerSimulation.Simulate(CurrentState, command, simulationConfig, tickDeltaTime);
            authoritativeState = new AuthoritativePlayerState(serverWorldTick, command.Tick, CurrentState);
            stateSender.TrySend(authoritativeState, out sendResult);
            return true;
        }
    }
}

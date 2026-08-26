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
        private bool hasAdvancedServerTick;
        private uint lastServerTick;

        /// <summary>
        /// 服务器处理完最近一条客户端输入后持有的未量化权威状态。
        /// </summary>
        public PlayerState CurrentState { get; private set; }

        public ServerAuthoritativeMovementSession(IGameTransport transport, TransportConnectionId clientConnectionId, in PlayerState initialState, in PlayerSimulationConfig simulationConfig, float tickDeltaTime, int inputWindowCapacity)
        {
            if (tickDeltaTime <= 0f || float.IsNaN(tickDeltaTime) || float.IsInfinity(tickDeltaTime))
            {
                throw new ArgumentOutOfRangeException(nameof(tickDeltaTime));
            }

            this.simulationConfig = simulationConfig;
            this.tickDeltaTime = tickDeltaTime;
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
        /// 在一个新的服务器世界 Tick 中最多处理一条连续输入，并发送对应权威确认。
        /// 返回 false 表示当前仍在等待 NextExpectedTick，服务器状态没有推进。
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
            if (!inputReceiver.TryDequeueNext(out PlayerInputCommand command))
            {
                return false;
            }

            CurrentState = PlayerSimulation.Simulate(CurrentState, command, simulationConfig, tickDeltaTime);
            authoritativeState = new AuthoritativePlayerState(serverWorldTick, command.Tick, CurrentState);
            stateSender.TrySend(authoritativeState, out sendResult);
            return true;
        }
    }
}

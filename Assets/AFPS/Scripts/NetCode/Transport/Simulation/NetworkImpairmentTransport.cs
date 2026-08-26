using System;
using System.Collections.Generic;

namespace AFPS.NetCode.Transport.Simulation
{
    /// <summary>
    /// 包装真实传输实例，为不可靠发送通道注入可复现的延迟、抖动、丢包和乱序。
    /// 可靠有序通道直接交给内部传输，避免在应用层伪造无法代表真实重传行为的可靠丢包。
    /// </summary>
    public sealed class NetworkImpairmentTransport : IGameTransport
    {
        private sealed class ScheduledPacket
        {
            /// <summary>
            /// 数据包需要发送到的 AFPS 连接。
            /// </summary>
            public TransportConnectionId ConnectionId;

            /// <summary>
            /// 数据包使用的不可靠传输语义。
            /// </summary>
            public TransportDelivery Delivery;

            /// <summary>
            /// 从调用方缓冲区复制的数据包内容，避免延迟期间原缓冲区被复用。
            /// </summary>
            public byte[] Payload;

            /// <summary>
            /// 使用调用方单调时钟表示的计划释放时间，单位为秒。
            /// </summary>
            public double ReleaseTimeSeconds;

            /// <summary>
            /// 数据包进入模拟队列的顺序，用于相同释放时间下保持稳定排序。
            /// </summary>
            public ulong EnqueueOrder;
        }

        /// <summary>
        /// 最终执行真实连接、收发和事件轮询的内部传输实例。
        /// </summary>
        private readonly IGameTransport innerTransport;

        /// <summary>
        /// 当前传输生命周期使用的只读网络劣化参数。
        /// </summary>
        private readonly NetworkImpairmentConfig config;

        /// <summary>
        /// 返回当前单调时间的函数，生产环境使用 Unity 实时时钟，测试使用手动时钟。
        /// </summary>
        private readonly Func<double> timeProvider;

        /// <summary>
        /// 已被上层接受、尚未到达计划释放时间的不可靠数据包。
        /// </summary>
        private readonly List<ScheduledPacket> scheduledPackets = new List<ScheduledPacket>();

        /// <summary>
        /// 当前生命周期用于丢包、抖动和乱序决策的可复现随机序列。
        /// </summary>
        private DeterministicRandom random;

        /// <summary>
        /// 下一个排队数据包获得的稳定次序，在释放时间相同时用于确定先后关系。
        /// </summary>
        private ulong nextEnqueueOrder;

        public bool IsRunning => innerTransport.IsRunning;
        public TransportRole Role => innerTransport.Role;

        /// <summary>
        /// 当前仍在等待计划释放时间的数据包数量。
        /// </summary>
        public int QueuedPacketCount => scheduledPackets.Count;

        /// <summary>
        /// 模拟器按配置主动丢弃的不可靠数据包累计数量。
        /// </summary>
        public ulong DroppedPacketCount { get; private set; }

        /// <summary>
        /// 到达计划时间并成功交给内部传输的数据包累计数量。
        /// </summary>
        public ulong ReleasedPacketCount { get; private set; }

        /// <summary>
        /// 到达计划时间后被内部传输拒绝的数据包累计数量。
        /// </summary>
        public ulong FailedReleaseCount { get; private set; }

        public NetworkImpairmentTransport(IGameTransport innerTransport, in NetworkImpairmentConfig config, Func<double> timeProvider)
        {
            this.innerTransport = innerTransport ?? throw new ArgumentNullException(nameof(innerTransport));
            this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
            this.config = config;
            random = new DeterministicRandom(config.RandomSeed);
        }

        public bool TryStartServer(ushort port, int maxConnections, out string error) => innerTransport.TryStartServer(port, maxConnections, out error);

        public bool TryStartClient(string address, ushort port, out string error) => innerTransport.TryStartClient(address, port, out error);

        public void Pump()
        {
            FlushDuePackets();
            innerTransport.Pump();
        }

        public bool TryPollEvent(ArraySegment<byte> receiveBuffer, out GameTransportEvent transportEvent) => innerTransport.TryPollEvent(receiveBuffer, out transportEvent);

        public TransportSendResult Send(TransportConnectionId connectionId, TransportDelivery delivery, ArraySegment<byte> payload)
        {
            if (delivery == TransportDelivery.ReliableSequenced)
            {
                return innerTransport.Send(connectionId, delivery, payload);
            }

            if (!innerTransport.IsRunning)
            {
                return TransportSendResult.NotRunning;
            }

            if (!connectionId.IsValid)
            {
                return TransportSendResult.InvalidConnection;
            }

            if (payload.Count > 0 && payload.Array == null)
            {
                return TransportSendResult.TransportError;
            }

            if (random.NextUnitDouble() < config.PacketLossProbability)
            {
                DroppedPacketCount++;
                return TransportSendResult.Success;
            }

            if (scheduledPackets.Count >= config.MaxQueuedPackets)
            {
                return TransportSendResult.TransportError;
            }

            double jitter = (random.NextUnitDouble() * 2d - 1d) * config.JitterSeconds;
            double delay = Math.Max(0d, config.BaseLatencySeconds + jitter);
            var packet = new ScheduledPacket
            {
                ConnectionId = connectionId,
                Delivery = delivery,
                Payload = CopyPayload(payload),
                ReleaseTimeSeconds = timeProvider() + delay,
                EnqueueOrder = nextEnqueueOrder++
            };

            int previousPacketIndex = FindPreviousReorderCandidate(packet);
            if (previousPacketIndex >= 0 && random.NextUnitDouble() < config.ReorderProbability)
            {
                ScheduledPacket previousPacket = scheduledPackets[previousPacketIndex];
                packet.ReleaseTimeSeconds = Math.Min(packet.ReleaseTimeSeconds, previousPacket.ReleaseTimeSeconds);
                previousPacket.ReleaseTimeSeconds = Math.Max(previousPacket.ReleaseTimeSeconds, packet.ReleaseTimeSeconds) + config.ReorderExtraDelaySeconds;
            }

            scheduledPackets.Add(packet);
            return TransportSendResult.Success;
        }

        public void Disconnect(TransportConnectionId connectionId)
        {
            RemoveQueuedPackets(connectionId);
            innerTransport.Disconnect(connectionId);
        }

        public void Stop()
        {
            ResetSimulationState();
            innerTransport.Stop();
        }

        public void Dispose()
        {
            scheduledPackets.Clear();
            innerTransport.Dispose();
        }

        private void FlushDuePackets()
        {
            double now = timeProvider();
            while (TryFindNextDuePacket(now, out int packetIndex))
            {
                ScheduledPacket packet = scheduledPackets[packetIndex];
                scheduledPackets.RemoveAt(packetIndex);
                TransportSendResult result = innerTransport.Send(packet.ConnectionId, packet.Delivery, new ArraySegment<byte>(packet.Payload));
                if (result == TransportSendResult.Success)
                {
                    ReleasedPacketCount++;
                }
                else
                {
                    FailedReleaseCount++;
                }
            }
        }

        private bool TryFindNextDuePacket(double now, out int packetIndex)
        {
            packetIndex = -1;
            for (int i = 0; i < scheduledPackets.Count; i++)
            {
                ScheduledPacket candidate = scheduledPackets[i];
                if (candidate.ReleaseTimeSeconds > now)
                {
                    continue;
                }

                if (packetIndex < 0 || IsScheduledBefore(candidate, scheduledPackets[packetIndex]))
                {
                    packetIndex = i;
                }
            }

            return packetIndex >= 0;
        }

        private static bool IsScheduledBefore(ScheduledPacket left, ScheduledPacket right)
        {
            return left.ReleaseTimeSeconds < right.ReleaseTimeSeconds || left.ReleaseTimeSeconds == right.ReleaseTimeSeconds && left.EnqueueOrder < right.EnqueueOrder;
        }

        private static byte[] CopyPayload(ArraySegment<byte> payload)
        {
            byte[] copy = new byte[payload.Count];
            if (payload.Count > 0)
            {
                Array.Copy(payload.Array, payload.Offset, copy, 0, payload.Count);
            }

            return copy;
        }

        private int FindPreviousReorderCandidate(ScheduledPacket packet)
        {
            for (int i = scheduledPackets.Count - 1; i >= 0; i--)
            {
                ScheduledPacket candidate = scheduledPackets[i];
                if (candidate.ConnectionId == packet.ConnectionId && candidate.Delivery == packet.Delivery)
                {
                    return i;
                }
            }

            return -1;
        }

        private void RemoveQueuedPackets(TransportConnectionId connectionId)
        {
            for (int i = scheduledPackets.Count - 1; i >= 0; i--)
            {
                if (scheduledPackets[i].ConnectionId == connectionId)
                {
                    scheduledPackets.RemoveAt(i);
                }
            }
        }

        private void ResetSimulationState()
        {
            scheduledPackets.Clear();
            random = new DeterministicRandom(config.RandomSeed);
            nextEnqueueOrder = 0;
            DroppedPacketCount = 0;
            ReleasedPacketCount = 0;
            FailedReleaseCount = 0;
        }
    }
}

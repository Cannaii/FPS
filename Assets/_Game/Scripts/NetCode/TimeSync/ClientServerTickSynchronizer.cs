using System;
using AFPS.Core.Collections;

namespace AFPS.NetCode.TimeSync
{
    /// <summary>
    /// 根据请求往返时间估算客户端当前时刻对应的服务器世界 Tick，并平滑多个有效样本。
    /// </summary>
    public sealed class ClientServerTickSynchronizer
    {
        private readonly TickBuffer<ulong> pendingRequests;
        private readonly double tickDurationMicroseconds;
        private readonly double maxRoundTripMicroseconds;
        private readonly double smoothingFactor;
        private bool hasServerTickSample;
        private uint lastRawServerTick;
        private double lastUnwrappedWholeServerTick;
        private double lastUnwrappedServerTickTime;
        private double smoothedServerTickOffset;

        public bool HasEstimate { get; private set; }

        public double LastNetworkRoundTripMilliseconds { get; private set; }

        public ClientServerTickSynchronizer(int serverTickRate, int pendingRequestCapacity = 32, double maxRoundTripMilliseconds = 1000.0, double smoothingFactor = 0.2)
        {
            if (serverTickRate <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(serverTickRate));
            }

            if (pendingRequestCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pendingRequestCapacity));
            }

            if (maxRoundTripMilliseconds <= 0.0 || double.IsNaN(maxRoundTripMilliseconds) || double.IsInfinity(maxRoundTripMilliseconds))
            {
                throw new ArgumentOutOfRangeException(nameof(maxRoundTripMilliseconds));
            }

            if (smoothingFactor <= 0.0 || smoothingFactor > 1.0 || double.IsNaN(smoothingFactor))
            {
                throw new ArgumentOutOfRangeException(nameof(smoothingFactor));
            }

            pendingRequests = new TickBuffer<ulong>(pendingRequestCapacity);
            tickDurationMicroseconds = 1000000.0 / serverTickRate;
            maxRoundTripMicroseconds = maxRoundTripMilliseconds * 1000.0;
            this.smoothingFactor = smoothingFactor;
        }

        /// <summary>
        /// 记录一个已经成功交给传输层的请求序号与客户端发送时间。
        /// </summary>
        public void RegisterSentRequest(uint requestSequence, ulong clientSendTimestampMicroseconds)
        {
            pendingRequests.Store(requestSequence, clientSendTimestampMicroseconds);
        }

        /// <summary>
        /// 处理服务器响应。服务器处理耗时会从总往返时间中扣除，再用网络 RTT 的一半估算下行时间。
        /// </summary>
        public bool TryProcessResponse(in TimeSyncResponse response, ulong clientReceiveTimestampMicroseconds, out ServerTickSyncSample sample)
        {
            sample = default;
            if (!pendingRequests.TryGet(response.RequestSequence, out ulong registeredSendTimestamp) || registeredSendTimestamp != response.ClientSendTimestampMicroseconds)
            {
                return false;
            }

            if (clientReceiveTimestampMicroseconds < registeredSendTimestamp || response.ServerSendTimestampMicroseconds < response.ServerReceiveTimestampMicroseconds)
            {
                return false;
            }

            ulong totalElapsed = clientReceiveTimestampMicroseconds - registeredSendTimestamp;
            ulong serverProcessing = response.ServerSendTimestampMicroseconds - response.ServerReceiveTimestampMicroseconds;
            if (serverProcessing > totalElapsed)
            {
                return false;
            }

            double networkRoundTrip = totalElapsed - serverProcessing;
            if (networkRoundTrip > maxRoundTripMicroseconds)
            {
                return false;
            }

            double unwrappedWholeServerTick;
            if (!hasServerTickSample)
            {
                unwrappedWholeServerTick = response.ServerWorldTick;
            }
            else
            {
                int tickDelta = unchecked((int)(response.ServerWorldTick - lastRawServerTick));
                if (tickDelta < 0)
                {
                    return false;
                }

                unwrappedWholeServerTick = lastUnwrappedWholeServerTick + tickDelta;
            }

            double serverTickAtSend = unwrappedWholeServerTick + response.ServerTickFraction / 65535.0;
            if (hasServerTickSample && serverTickAtSend <= lastUnwrappedServerTickTime)
            {
                return false;
            }

            double oneWayMicroseconds = networkRoundTrip * 0.5;
            double estimatedServerTickAtReceive = serverTickAtSend + oneWayMicroseconds / tickDurationMicroseconds;
            double clientTimeInServerTicks = clientReceiveTimestampMicroseconds / tickDurationMicroseconds;
            double measuredOffset = estimatedServerTickAtReceive - clientTimeInServerTicks;
            smoothedServerTickOffset = HasEstimate ? smoothedServerTickOffset + (measuredOffset - smoothedServerTickOffset) * smoothingFactor : measuredOffset;

            hasServerTickSample = true;
            lastRawServerTick = response.ServerWorldTick;
            lastUnwrappedWholeServerTick = unwrappedWholeServerTick;
            lastUnwrappedServerTickTime = serverTickAtSend;
            HasEstimate = true;
            LastNetworkRoundTripMilliseconds = networkRoundTrip / 1000.0;
            sample = new ServerTickSyncSample(response.RequestSequence, LastNetworkRoundTripMilliseconds, oneWayMicroseconds / 1000.0, estimatedServerTickAtReceive, smoothedServerTickOffset);
            return true;
        }

        /// <summary>
        /// 将当前客户端单调时钟映射为连续的服务器世界 Tick 时间。
        /// </summary>
        public bool TryGetEstimatedServerTick(ulong clientTimestampMicroseconds, out double estimatedServerTick)
        {
            if (!HasEstimate)
            {
                estimatedServerTick = default;
                return false;
            }

            estimatedServerTick = clientTimestampMicroseconds / tickDurationMicroseconds + smoothedServerTickOffset;
            return true;
        }
    }
}

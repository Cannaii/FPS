using System;
using System.Collections.Generic;
using System.Diagnostics;
using AFPS.NetCode.TimeSync;
using AFPS.NetCode.Transport;

namespace AFPS.NetCode.Sessions
{
    public sealed partial class NetworkMovementSessionManager
    {
        // 网络采样/保活使用单调微秒，不依赖输入 Tick、Time.timeScale 或玩家是否移动。
        private const ulong TimeSyncIntervalMicroseconds = 500000;
        private const ulong TimeSyncExpiryMicroseconds = 3000000;
        private readonly Func<ulong> timeSyncClock;
        private readonly Dictionary<TransportConnectionId, ServerTimeSyncResponder> timeSyncResponders =
            new Dictionary<TransportConnectionId, ServerTimeSyncResponder>();
        private ClientTimeSyncSession clientTimeSync;
        private uint timeSyncWorldTick;
        private float timeSyncTickAlpha;
        private ulong lastTimeSyncAttempt;
        private ulong lastTimeSyncResponse;
        private bool hasTimeSyncAttempt;
        private bool hasTimeSyncResponse;

        public bool HasTimeSync => clientTimeSync != null && hasTimeSyncResponse
            && unchecked(timeSyncClock() - lastTimeSyncResponse) <= TimeSyncExpiryMicroseconds;

        /// <summary>由帧循环更新服务器相位并发送保活；Tick 相位范围 [0,1]。</summary>
        public void AdvanceTimeSync(uint worldTick, float tickAlpha)
        {
            if (float.IsNaN(tickAlpha) || tickAlpha < 0f || tickAlpha > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(tickAlpha));
            }
            timeSyncWorldTick = worldTick;
            timeSyncTickAlpha = tickAlpha;
            if (clientTimeSync == null)
            {
                return;
            }
            ulong now = timeSyncClock();
            if (hasTimeSyncAttempt && unchecked(now - lastTimeSyncAttempt) < TimeSyncIntervalMicroseconds)
            {
                return;
            }
            // 发送失败也限频，避免传输层暂不可用时每帧重试造成忙循环。
            lastTimeSyncAttempt = now;
            hasTimeSyncAttempt = true;
            if (clientTimeSync.SendRequest(now, out _) == TransportSendResult.Success)
            {
                Metrics.TimeSyncRequestsSent++;
            }
        }

        public bool TryGetSynchronizedServerTick(out double tick)
        {
            tick = default;
            return HasTimeSync && clientTimeSync.Synchronizer.TryGetEstimatedServerTick(timeSyncClock(), out tick);
        }

        private bool ReceiveTimeSync(ArraySegment<byte> packet)
        {
            ulong now = timeSyncClock();
            if (clientTimeSync == null || !clientTimeSync.TryReceiveResponse(packet, now, out var sample))
            {
                return false;
            }
            double previous = Metrics.LastNetworkRoundTripMilliseconds;
            Metrics.LastNetworkRoundTripMilliseconds = sample.NetworkRoundTripMilliseconds;
            if (hasTimeSyncResponse)
            {
                Metrics.RoundTripJitterMilliseconds += (System.Math.Abs(previous - sample.NetworkRoundTripMilliseconds)
                    - Metrics.RoundTripJitterMilliseconds) * 0.2;
            }
            Metrics.TimeSyncResponsesReceived++;
            lastTimeSyncResponse = now;
            hasTimeSyncResponse = true;
            return true;
        }

        private void ResetClientTimeSync(TransportConnectionId connection)
        {
            clientTimeSync = connection.IsValid
                ? new ClientTimeSyncSession(clientTransport, connection,
                    new ClientServerTickSynchronizer((int)System.Math.Round(1d / tickDeltaTime)))
                : null;
            hasTimeSyncAttempt = false;
            hasTimeSyncResponse = false;
            Metrics.TimeSyncRequestsSent = 0;
            Metrics.TimeSyncResponsesReceived = 0;
            Metrics.LastNetworkRoundTripMilliseconds = 0;
            Metrics.RoundTripJitterMilliseconds = 0;
        }

        private static ulong ReadMonotonicMicroseconds()
        {
            long timestamp = Stopwatch.GetTimestamp();
            // 先商后余避免长时间运行时 timestamp * 1000000 溢出。
            return (ulong)(timestamp / Stopwatch.Frequency) * 1000000UL
                + (ulong)((timestamp % Stopwatch.Frequency) * 1000000L / Stopwatch.Frequency);
        }
    }
}

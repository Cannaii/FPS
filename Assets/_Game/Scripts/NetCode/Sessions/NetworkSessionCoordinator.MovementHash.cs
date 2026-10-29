using System;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Metrics;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.Transport;
using AFPS.Simulation.Characters;

namespace AFPS.NetCode.Sessions
{
    public sealed partial class NetworkSessionCoordinator
    {
        private const uint StateHashIntervalInputTicks = 25;
        private readonly byte[] stateHashBuffer = new byte[ClientMovementHashReportCodec.PacketSize];
        private ClientMovementHashReport pendingStateHash;
        private bool hasPendingStateHash;
        private bool hasSentStateHash;
        private uint lastSentStateHashTick;
        private uint stateHashSequence = 1;

        public MovementHashComparison LastStateHashComparison { get; private set; }
        public MovementHashComparison LastStateDrift { get; private set; }
        public event Action<MovementHashComparison> StateDriftDetected;

        private void ResetClientStateHash()
        {
            hasPendingStateHash = false;
            hasSentStateHash = false;
            stateHashSequence = 1;
        }

        private void ObserveClientStateHash(in PlayerMovementState predicted, in PlayerInputCommand canonicalInput)
        {
            if (WorldContext == null || RemotePlayers == null || RemotePlayers.LocalEntityId == 0)
            {
                return;
            }
            if (predicted.Tick % StateHashIntervalInputTicks == 0
                && (!hasSentStateHash || SequenceMath.IsNewer(predicted.Tick, lastSentStateHashTick))
                && (!hasPendingStateHash || SequenceMath.IsNewer(predicted.Tick, pendingStateHash.InputTick)))
            {
                if (hasPendingStateHash)
                {
                    Metrics.StateHashReportsSuperseded++;
                }
                // 立即取预测值，后续回滚不得覆盖这份原始证据；缓存只有一条，发送失败也不无限积累。
                pendingStateHash = new ClientMovementHashReport(RemotePlayers.LocalEntityId, predicted.Tick,
                    PlayerMovementStateHash.Compute(predicted), MovementInputHash.Compute(canonicalInput));
                hasPendingStateHash = true;
            }
            if (hasPendingStateHash && ClientMovementHashReportCodec.TrySerialize(pendingStateHash, stateHashSequence,
                    new ArraySegment<byte>(stateHashBuffer), out int count)
                && clientTransport.Send(clientConnectionId, TransportDelivery.ReliableSequenced,
                    new ArraySegment<byte>(stateHashBuffer, 0, count)) == TransportSendResult.Success)
            {
                hasSentStateHash = true;
                lastSentStateHashTick = pendingStateHash.InputTick;
                hasPendingStateHash = false;
                stateHashSequence = unchecked(stateHashSequence + 1);
                Metrics.StateHashReportsSent++;
            }
        }

        private bool ReceiveClientStateHash(TransportConnectionId connectionId, ArraySegment<byte> packet)
        {
            if (!recoveringConnections.ContainsKey(connectionId)
                && serverSessions.TryGetValue(connectionId, out var session)
                && ClientMovementHashReportCodec.TryDeserialize(packet, out uint sequence, out var report)
                && report.EntityId == session.EntityId && session.StateDrift.TryReceive(report, sequence))
            {
                Metrics.StateHashReportsReceived++;
                return true;
            }
            Metrics.StateHashReportsRejected++;
            return false;
        }

        private void OnStateHashCompared(MovementHashComparison comparison)
        {
            LastStateHashComparison = comparison;
            switch (comparison.Status)
            {
                case MovementHashComparisonStatus.Matched:
                    Metrics.StateHashMatches++;
                    break;
                case MovementHashComparisonStatus.StateMismatch:
                    Metrics.StateHashMismatches++;
                    LastStateDrift = comparison;
                    StateDriftDetected?.Invoke(comparison);
                    break;
                case MovementHashComparisonStatus.DifferentAppliedInput:
                    Metrics.StateHashDifferentInputs++;
                    break;
                case MovementHashComparisonStatus.HistoryUnavailable:
                    Metrics.StateHashHistoryMisses++;
                    break;
            }
        }
    }
}

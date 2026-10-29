using System;
using AFPS.NetCode.Prediction;
using AFPS.NetCode.Runtime;
using AFPS.NetCode.Transport;
using AFPS.Simulation.Characters;
using AFPS.Simulation.World;

namespace AFPS.NetCode.Sessions
{
    public sealed partial class NetworkSessionCoordinator
    {
        private readonly SimulationSessionAdmission admission;

        /// <summary>显式定点世界；过渡模式为 null，继续沿用原有会话生命周期。</summary>
        public SimulationWorldContext WorldContext { get; }

        public ConnectionAdmissionStatus ClientAdmissionStatus => admission != null
            ? admission.ClientStatus
            : ClientSession != null ? ConnectionAdmissionStatus.Admitted : ConnectionAdmissionStatus.Disconnected;

        /// <summary>收到有效 Welcome 后可显示服务器期望的指纹，包括版本拒绝时的诊断信息。</summary>
        public SimulationWorldIdentity? ExpectedServerIdentity => admission?.ExpectedServerIdentity;

        private void ResendSessionAssignment(TransportConnectionId connectionId)
        {
            if (serverEntityIds.TryGetValue(connectionId, out uint entityId))
            {
                SendAssignment(connectionId, entityId);
            }
        }

        public bool HandleConnected(NetworkTransportSide side, TransportConnectionId connectionId)
        {
            return admission != null
                ? admission.HandleConnected(side, connectionId)
                : CreateConnectedSession(side, connectionId);
        }

        public bool HandleDisconnected(NetworkTransportSide side, TransportConnectionId connectionId)
        {
            return admission != null
                ? admission.HandleDisconnected(side, connectionId)
                : RemoveConnectedSession(side, connectionId);
        }

        public bool TryHandleData(
            NetworkTransportSide side,
            TransportConnectionId connectionId,
            ArraySegment<byte> packet,
            out MovementReconciliationResult reconciliationResult)
        {
            reconciliationResult = default;
            if (admission != null)
            {
                if (admission.TryHandleControl(side, connectionId, packet, out bool handled))
                {
                    return handled;
                }

                if (!admission.IsAdmitted(side, connectionId))
                {
                    return false;
                }
            }

            return HandleSessionData(side, connectionId, packet, out reconciliationResult);
        }
    }
}

using System;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Protocol;

namespace AFPS.NetCode.StateReplication
{
    /// <summary>
    /// 客户端解码服务器权威状态，并拒绝重复、过时或时间倒退的状态包。
    /// </summary>
    public sealed class ClientMovementStateReceiver
    {
        private bool hasAcceptedState;
        private uint lastPacketSequence;
        private uint lastServerTick;
        private uint lastProcessedInputTick;

        /// <summary>
        /// 解码并验证一个完整权威状态包。返回 true 时才允许进入预测比较和回滚流程。
        /// </summary>
        public bool TryReceivePacket(ArraySegment<byte> packet, out AuthoritativeMovementState authoritativeState, out MovementStateReceiveResult result)
        {
            authoritativeState = default;
            if (!AuthoritativeMovementStateCodec.TryDeserialize(packet, out PacketHeader header, out AuthoritativeMovementState decodedState))
            {
                result = new MovementStateReceiveResult(MovementStateReceiveStatus.InvalidPacket);
                return false;
            }

            if (hasAcceptedState && !SequenceMath.IsNewer(header.Sequence, lastPacketSequence))
            {
                result = CreateResult(MovementStateReceiveStatus.StaleOrDuplicateSequence, header.Sequence, decodedState);
                return false;
            }

            if (hasAcceptedState && SequenceMath.IsOlder(decodedState.ServerTick, lastServerTick))
            {
                result = CreateResult(MovementStateReceiveStatus.RegressiveServerTick, header.Sequence, decodedState);
                return false;
            }

            if (hasAcceptedState && SequenceMath.IsOlder(decodedState.LastProcessedInputTick, lastProcessedInputTick))
            {
                result = CreateResult(MovementStateReceiveStatus.RegressiveInputAcknowledgement, header.Sequence, decodedState);
                return false;
            }

            hasAcceptedState = true;
            lastPacketSequence = header.Sequence;
            lastServerTick = decodedState.ServerTick;
            lastProcessedInputTick = decodedState.LastProcessedInputTick;
            authoritativeState = decodedState;
            result = CreateResult(MovementStateReceiveStatus.Accepted, header.Sequence, decodedState);
            return true;
        }

        private static MovementStateReceiveResult CreateResult(MovementStateReceiveStatus status, uint sequence, in AuthoritativeMovementState state)
        {
            return new MovementStateReceiveResult(status, sequence, state.ServerTick, state.LastProcessedInputTick);
        }
    }
}

using System;
using AFPS.Core.Collections;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Transport;
using AFPS.Simulation.Characters;

namespace AFPS.NetCode.InputReplication
{
    /// <summary>
    /// 从预测输入历史中收集最新连续命令，并通过不可靠有序通道重复发送近期输入。
    /// </summary>
    public sealed class ClientInputBatchSender
    {
        private readonly IGameTransport transport;
        private readonly TransportConnectionId serverConnectionId;
        private readonly TickBuffer<PlayerInputCommand> inputHistory;
        private readonly PlayerInputCommand[] batchCommands;
        private readonly byte[] packetBuffer;
        private uint nextPacketSequence;

        /// <summary>
        /// 每个包最多重复携带多少个最近 Tick 的输入。
        /// </summary>
        public int RedundancyCount => batchCommands.Length;

        public ClientInputBatchSender(IGameTransport transport, TransportConnectionId serverConnectionId, TickBuffer<PlayerInputCommand> inputHistory, int redundancyCount, uint initialPacketSequence = 1)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            this.inputHistory = inputHistory ?? throw new ArgumentNullException(nameof(inputHistory));
            if (!serverConnectionId.IsValid)
            {
                throw new ArgumentException("服务器连接 ID 必须有效。", nameof(serverConnectionId));
            }

            if (redundancyCount <= 0 || redundancyCount > InputCommandBatchCodec.MaxCommandCount)
            {
                throw new ArgumentOutOfRangeException(nameof(redundancyCount), $"输入冗余数量必须位于 1 到 {InputCommandBatchCodec.MaxCommandCount}。");
            }

            this.serverConnectionId = serverConnectionId;
            batchCommands = new PlayerInputCommand[redundancyCount];
            packetBuffer = new byte[InputCommandBatchCodec.GetPacketSize(redundancyCount)];
            nextPacketSequence = initialPacketSequence;
        }

        /// <summary>
        /// 发送以 latestTick 结尾的连续输入后缀；历史中出现缺口时不会跨过缺口发送更旧命令。
        /// </summary>
        public bool TrySendLatest(uint latestTick, out InputBatchSendResult result)
        {
            int commandCount = CountAvailableSuffix(latestTick);
            if (commandCount == 0)
            {
                result = new InputBatchSendResult(InputBatchSendStatus.MissingLatestInput, TransportSendResult.TransportError, latestTick, latestTick, 0, nextPacketSequence, 0);
                return false;
            }

            uint firstTick = unchecked(latestTick - (uint)commandCount + 1);
            for (int i = 0; i < commandCount; i++)
            {
                inputHistory.TryGet(unchecked(firstTick + (uint)i), out batchCommands[i]);
            }

            InputCommandBatch batch = new InputCommandBatch(new ArraySegment<PlayerInputCommand>(batchCommands, 0, commandCount));
            uint packetSequence = nextPacketSequence;
            if (!InputCommandBatchCodec.TrySerialize(batch, packetSequence, new ArraySegment<byte>(packetBuffer), out int packetBytes))
            {
                result = new InputBatchSendResult(InputBatchSendStatus.SerializationFailed, TransportSendResult.TransportError, firstTick, latestTick, commandCount, packetSequence, 0);
                return false;
            }

            TransportSendResult transportResult = transport.Send(serverConnectionId, TransportDelivery.UnreliableSequenced, new ArraySegment<byte>(packetBuffer, 0, packetBytes));
            if (transportResult == TransportSendResult.Success)
            {
                nextPacketSequence = unchecked(nextPacketSequence + 1);
            }

            InputBatchSendStatus status = transportResult == TransportSendResult.Success ? InputBatchSendStatus.Sent : InputBatchSendStatus.TransportRejected;
            result = new InputBatchSendResult(status, transportResult, firstTick, latestTick, commandCount, packetSequence, packetBytes);
            return result.Succeeded;
        }

        private int CountAvailableSuffix(uint latestTick)
        {
            int count = 0;
            while (count < batchCommands.Length)
            {
                uint tick = unchecked(latestTick - (uint)count);
                if (!inputHistory.TryGet(tick, out _))
                {
                    break;
                }

                count++;
            }

            return count;
        }
    }
}

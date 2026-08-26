using System;
using AFPS.Core.Collections;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Protocol;
using AFPS.Simulation.Characters;

namespace AFPS.NetCode.InputReplication
{
    /// <summary>
    /// 解码客户端输入批次，按 Tick 去重并只按连续顺序向服务器模拟交付命令。
    /// </summary>
    public sealed class ServerInputCommandReceiver
    {
        private readonly TickBuffer<PlayerInputCommand> receiveWindow;
        private readonly PlayerInputCommand[] decodeBuffer = new PlayerInputCommand[InputCommandBatchCodec.MaxCommandCount];

        /// <summary>
        /// 服务器下一条等待交付给权威模拟的客户端输入 Tick。
        /// </summary>
        public uint NextExpectedTick { get; private set; }

        /// <summary>
        /// 未来输入接收窗口的容量；过远 Tick 会被拒绝，避免恶意覆盖和无界等待。
        /// </summary>
        public int WindowCapacity => receiveWindow.Capacity;

        public ServerInputCommandReceiver(uint firstExpectedTick, int windowCapacity)
        {
            if (windowCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(windowCapacity), "服务器输入接收窗口必须大于零。");
            }

            NextExpectedTick = firstExpectedTick;
            receiveWindow = new TickBuffer<PlayerInputCommand>(windowCapacity);
        }

        /// <summary>
        /// 接收一个完整 AFPS 输入包。返回 false 表示包头、长度或输入负载不合法。
        /// </summary>
        public bool TryReceivePacket(ArraySegment<byte> packet, out InputBatchReceiveResult result)
        {
            result = default;
            if (!InputCommandBatchCodec.TryDeserialize(packet, decodeBuffer, 0, out PacketHeader header, out InputCommandBatch batch))
            {
                return false;
            }

            int acceptedCount = 0;
            int duplicateCount = 0;
            int rejectedCount = 0;
            for (int i = 0; i < batch.CommandCount; i++)
            {
                PlayerInputCommand command = batch.Commands.Array[batch.Commands.Offset + i];
                int distanceFromExpected = unchecked((int)(command.Tick - NextExpectedTick));
                if (distanceFromExpected < 0)
                {
                    duplicateCount++;
                    continue;
                }

                if (distanceFromExpected >= WindowCapacity)
                {
                    rejectedCount++;
                    continue;
                }

                if (receiveWindow.TryGet(command.Tick, out _))
                {
                    duplicateCount++;
                    continue;
                }

                receiveWindow.Store(command.Tick, command);
                acceptedCount++;
            }

            result = new InputBatchReceiveResult(header.Sequence, batch.FirstTick, batch.LastTick, acceptedCount, duplicateCount, rejectedCount);
            return true;
        }

        /// <summary>
        /// 仅当 NextExpectedTick 已收到时交付命令；存在 Tick 缺口时返回 false 并等待后续冗余包补齐。
        /// </summary>
        public bool TryDequeueNext(out PlayerInputCommand command)
        {
            if (!receiveWindow.TryGet(NextExpectedTick, out command))
            {
                return false;
            }

            NextExpectedTick = unchecked(NextExpectedTick + 1);
            return true;
        }
    }
}

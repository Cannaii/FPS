using System;
using AFPS.Simulation.Characters;

namespace AFPS.NetCode.Messages
{
    /// <summary>
    /// 一组连续 Tick 的玩家输入。Commands 引用调用方拥有的数组，不复制输入数据。
    /// </summary>
    public readonly struct InputCommandBatch
    {
        /// <summary>
        /// 批次中第一条输入所属的客户端模拟 Tick。
        /// </summary>
        public readonly uint FirstTick;

        /// <summary>
        /// 按 Tick 升序排列的连续输入命令数组片段。
        /// </summary>
        public readonly ArraySegment<PlayerInputCommand> Commands;

        /// <summary>
        /// 批次中的输入数量。
        /// </summary>
        public int CommandCount => Commands.Count;

        /// <summary>
        /// 批次最后一条输入所属的客户端模拟 Tick；空批次返回 FirstTick。
        /// </summary>
        public uint LastTick => CommandCount == 0 ? FirstTick : unchecked(FirstTick + (uint)CommandCount - 1);

        public InputCommandBatch(ArraySegment<PlayerInputCommand> commands)
        {
            Commands = commands;
            FirstTick = commands.Count > 0 ? commands.Array[commands.Offset].Tick : 0;
        }

        internal InputCommandBatch(uint firstTick, ArraySegment<PlayerInputCommand> commands)
        {
            FirstTick = firstTick;
            Commands = commands;
        }
    }
}

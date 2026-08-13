namespace AFPS.Core.Collections
{
    /// <summary>
    /// 使用固定容量按 Tick 保存历史数据的环形缓冲区。
    /// 当不同 Tick 映射到同一个槽位时，新数据会覆盖旧数据。
    /// </summary>
    /// <typeparam name="T">
    /// 每个 Tick 需要保存的数据类型，例如输入命令或玩家状态。
    /// </typeparam>
    public sealed class TickBuffer<T>
    {
        /// <summary>
        /// 环形缓冲区中的单个存储槽位。
        /// 除了保存数据，还必须记录该数据真正对应的 Tick。
        /// </summary>
        private struct Entry
        {
            /// <summary>
            /// 当前槽位保存的数据所对应的 Tick。
            /// </summary>
            public uint Tick;

            /// <summary>
            /// 当前槽位保存的实际数据。
            /// </summary>
            public T Value;

            /// <summary>
            /// 当前槽位是否已经保存过有效数据。
            /// Tick 为 0 也可能是合法 Tick，因此不能用 Tick 是否为 0 判断。
            /// </summary>
            public bool IsValid;
        }

        /// <summary>
        /// 环形缓冲区包含的固定数量槽位。
        /// 创建后不会改变容量。
        /// </summary>
        private readonly Entry[] entries;

        /// <summary>
        /// 获取该缓冲区最多能够保存多少个 Tick 的数据。
        /// </summary>
        public int Capacity => entries.Length;

        /// <summary>
        /// 创建指定容量的 Tick 环形缓冲区。
        /// </summary>
        /// <param name="capacity">最多保存的 Tick 数据数量。</param>
        /// <exception cref="System.ArgumentOutOfRangeException">
        /// 当容量小于或等于零时抛出。
        /// </exception>
        public TickBuffer(int capacity)
        {
            if (capacity <= 0)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(capacity),
                    "TickBuffer 的容量必须大于零。");
            }

            entries = new Entry[capacity];
        }

        /// <summary>
        /// 将 Tick 映射到环形缓冲区中的数组索引。
        /// </summary>
        /// <param name="tick">需要映射的 Tick。</param>
        /// <returns>Tick 对应的数组索引。</returns>
        private int GetIndex(uint tick)
        {
            return (int)(tick % (uint)entries.Length);
        }

        /// <summary>
        /// 保存指定 Tick 对应的数据。
        /// 如果槽位中存在较旧的数据，该数据会被覆盖。
        /// </summary>
        /// <param name="tick">数据对应的模拟 Tick。</param>
        /// <param name="value">需要保存的数据。</param>
        public void Store(uint tick, in T value)
        {
            int index = GetIndex(tick);

            entries[index] = new Entry
            {
                Tick = tick,
                Value = value,
                IsValid = true
            };
        }

        /// <summary>
        /// 尝试读取指定 Tick 对应的数据。
        /// </summary>
        /// <param name="tick">需要读取的模拟 Tick。</param>
        /// <param name="value">
        /// 读取成功时返回保存的数据；失败时返回该类型的默认值。
        /// </param>
        /// <returns>缓冲区中是否仍然保存着该 Tick 的数据。</returns>
        public bool TryGet(uint tick, out T value)
        {
            int index = GetIndex(tick);
            Entry entry = entries[index];

            if (!entry.IsValid || entry.Tick != tick)
            {
                value = default;
                return false;
            }

            value = entry.Value;
            return true;
        }

        /// <summary>
        /// 清除缓冲区中保存的所有 Tick 数据。
        /// 清空后，所有 Tick 查询都会返回不存在。
        /// </summary>
        public void Clear()
        {
            System.Array.Clear(entries, 0, entries.Length);
        }
    }
}
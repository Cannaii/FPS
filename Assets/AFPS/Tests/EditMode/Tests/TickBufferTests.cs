using AFPS.Core.Collections;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    /// <summary>
    /// 验证按 Tick 保存历史数据的环形缓冲区。
    /// </summary>
    public class TickBufferTests
    {
        /// <summary>
        /// 验证保存数据后，可以通过对应 Tick 将其读取出来。
        /// </summary>
        [Test]
        public void Store_ThenGetSameTick_ReturnsStoredValue()
        {
            TickBuffer<int> buffer = new TickBuffer<int>(capacity: 4);

            int storedValue = 123;
            buffer.Store(tick: 10, storedValue);

            bool found = buffer.TryGet(tick: 10, out int result);

            Assert.IsTrue(found);
            Assert.AreEqual(123, result);
        }

        /// <summary>
        /// 验证新 Tick 覆盖相同槽位后，
        /// 不能再把新数据错误地当成旧 Tick 的数据。
        /// </summary>
        [Test]
        public void Store_OverwritesSameSlot_OldTickIsNoLongerAvailable()
        {
            TickBuffer<int> buffer = new TickBuffer<int>(capacity: 4);

            buffer.Store(tick: 0, 100);
            buffer.Store(tick: 4, 400);

            bool foundOld = buffer.TryGet(tick: 0, out _);

            bool foundNew = buffer.TryGet(tick: 4, out int newValue);

            Assert.IsFalse(foundOld);
            Assert.IsTrue(foundNew);
            Assert.AreEqual(400, newValue);
        }

        /// <summary>
        /// 验证清空缓冲区后，之前保存的数据无法再被读取。
        /// </summary>
        [Test]
        public void Clear_RemovesAllStoredValues()
        {
            TickBuffer<int> buffer = new TickBuffer<int>(capacity: 4);

            buffer.Store(tick: 1, 100);
            buffer.Store(tick: 2, 200);

            buffer.Clear();

            bool foundTick1 = buffer.TryGet(tick: 1, out _);

            bool foundTick2 = buffer.TryGet(tick: 2, out _);

            Assert.IsFalse(foundTick1);
            Assert.IsFalse(foundTick2);
        }
    }
}
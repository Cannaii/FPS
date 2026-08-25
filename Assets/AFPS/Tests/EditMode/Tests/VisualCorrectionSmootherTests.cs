using AFPS.Presentation.Characters;
using NUnit.Framework;
using UnityEngine;

namespace AFPS.Tests.EditMode
{
    /// <summary>
    /// 验证模拟校正产生的视觉偏移能够独立于模拟状态逐渐衰减。
    /// </summary>
    public class VisualCorrectionSmootherTests
    {
        /// <summary>
        /// 验证经过一个半衰期后，视觉偏移减少到原来的一半。
        /// </summary>
        [Test]
        public void Update_AfterOneHalfLife_RetainsHalfOffset()
        {
            VisualCorrectionSmoother smoother = new VisualCorrectionSmoother();
            smoother.Capture(new Vector3(2f, 0f, 0f), Vector3.zero, 3f);

            Vector3 result = smoother.Update(0.1f, 0.1f);

            Assert.AreEqual(1f, result.x, 0.0001f);
            Assert.AreEqual(0f, result.y, 0.0001f);
            Assert.AreEqual(0f, result.z, 0.0001f);
        }

        /// <summary>
        /// 验证校正距离超过允许范围时不进行缓慢追赶。
        /// </summary>
        [Test]
        public void Capture_ExceedsMaximumDistance_ClearsOffset()
        {
            VisualCorrectionSmoother smoother = new VisualCorrectionSmoother();
            smoother.Capture(new Vector3(2f, 0f, 0f), Vector3.zero, 1f);

            Assert.AreEqual(Vector3.zero, smoother.Offset);
        }
    }
}

namespace AFPS.NetCode.Prediction
{
    /// <summary>
    /// 描述服务器权威状态与客户端同 Tick 预测状态之间的误差。
    /// </summary>
    public readonly struct PredictionError
    {
        /// <summary>
        /// 本次比较对应的客户端输入 Tick。
        /// </summary>
        public readonly uint InputTick;

        /// <summary>
        /// 权威位置与预测位置之间的世界空间距离，单位为米。
        /// </summary>
        public readonly float Position;

        /// <summary>
        /// 权威速度与预测速度之间的差值大小，单位为米每秒。
        /// </summary>
        public readonly float Velocity;

        /// <summary>
        /// 权威状态与预测状态的落地标记是否不同。
        /// </summary>
        public readonly bool GroundedMismatch;

        public PredictionError(uint inputTick, float position, float velocity, bool groundedMismatch)
        {
            InputTick = inputTick;
            Position = position;
            Velocity = velocity;
            GroundedMismatch = groundedMismatch;
        }

        /// <summary>
        /// 判断本次误差是否超过允许范围，需要执行客户端校正。
        /// </summary>
        public bool Exceeds(float positionThreshold, float velocityThreshold)
        {
            return Position > positionThreshold || Velocity > velocityThreshold || GroundedMismatch;
        }
    }
}

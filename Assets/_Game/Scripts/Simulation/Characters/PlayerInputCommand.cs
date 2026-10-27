using AFPS.Math;

namespace AFPS.Simulation.Characters
{
    /// <summary>
    /// 描述单次Tick中输入的用户命令
    /// </summary>
    public struct PlayerInputCommand
    {
        private sbyte moveXGrid;
        private sbyte moveYGrid;
        private ushort yawCentidegrees;
        private short pitchCentidegrees;
        public sbyte MoveXGrid => moveXGrid;
        public sbyte MoveYGrid => moveYGrid;
        public ushort YawCentidegrees => yawCentidegrees;
        public short PitchCentidegrees => pitchCentidegrees;
        public Fix MoveXFixed => InputAxisGrid.ToFix(moveXGrid);
        public Fix MoveYFixed => InputAxisGrid.ToFix(moveYGrid);
        public Fix LookYawFixedDegrees => InputAngleGrid.YawToFix(yawCentidegrees);
        public Fix LookPitchFixedDegrees => InputAngleGrid.PitchToFix(pitchCentidegrees);

        /// <summary>定点模拟/测试构造输入的入口，始终落到规范网格。</summary>
        public void SetFixedControls(Fix moveX, Fix moveY, Fix yawDegrees, Fix pitchDegrees)
        {
            moveXGrid = InputAxisGrid.FromFix(moveX);
            moveYGrid = InputAxisGrid.FromFix(moveY);
            yawCentidegrees = InputAngleGrid.YawFromFix(yawDegrees);
            pitchCentidegrees = InputAngleGrid.PitchFromFix(pitchDegrees);
        }

        /// <summary>网络解码入口；非法域不修改命令。sbyte -128 永远不是合法轴档位。</summary>
        public bool TrySetEncodedControls(sbyte moveX, sbyte moveY, ushort yaw, short pitch)
        {
            if (moveX < -InputAxisGrid.GridMax || moveY < -InputAxisGrid.GridMax || !InputAngleGrid.IsValid(yaw, pitch))
            {
                return false;
            }

            moveXGrid = moveX;
            moveYGrid = moveY;
            yawCentidegrees = yaw;
            pitchCentidegrees = pitch;
            return true;
        }

        /// <summary>为缺失的输入 Tick 创建替代命令：直接复制网格，保留朝向，清除所有一次性事件。</summary>
        public PlayerInputCommand CreateFallback(uint inputTick, bool repeatMovement)
        {
            PlayerInputCommand result = this;
            result.Tick = inputTick;
            if (!repeatMovement)
            {
                result.moveXGrid = 0;
                result.moveYGrid = 0;
                result.SprintHeld = false;
            }

            result.JumpPressed = false;
            result.DashPressed = false;
            result.WallBouncePressed = false;
            result.FirePressed = false;
            result.ReloadPressed = false;
            result.SwitchWeaponPressed = false;
            result.ShotSequence = 0;
            result.ShotServerTick = 0;
            return result;
        }

        /// <summary>
        /// 该输入命令对应的客户端模拟 Tick 编号。
        /// 服务器使用它确定输入顺序，客户端使用它进行输入确认和预测校正。
        /// </summary>
        public uint Tick;
        /// <summary>
        /// 玩家在本地水平方向上的移动输入。
        /// -1 表示向左，1 表示向右，0 表示没有水平移动输入。
        /// </summary>
         // 迁移兼容边界：设备/旧浮点模拟使用这些属性；命令实际仅存整数，不存 float。
        // 后续定点模拟只消费 Move*Fixed/Look*FixedDegrees，不经这些 float 视图。
        public float MoveX { get => MoveXFixed.ToFloat(); set => moveXGrid = InputAxisGrid.FromFloat(value); }
        /// <summary>
        /// 玩家在本地前后方向上的移动输入。
        /// -1 表示向后，1 表示向前，0 表示没有前后移动输入。
        /// </summary>
        public float MoveY { get => MoveYFixed.ToFloat(); set => moveYGrid = InputAxisGrid.FromFloat(value); }
        /// <summary>
        /// 玩家在本 Tick 希望采用的水平观察角，单位为度。
        /// 该值由客户端输入层产生，经过网络量化后同时供客户端预测和服务器权威模拟使用。
        /// </summary>
        public float LookYaw { get => LookYawFixedDegrees.ToFloat(); set => yawCentidegrees = InputAngleGrid.YawFromFloat(value); }
        /// <summary>
        /// 玩家在本 Tick 希望采用的垂直观察角，单位为度，向上为负、向下为正。
        /// 该值会被限制在第一人称相机允许的俯仰范围内。
        /// </summary>
        public float LookPitch { get => LookPitchFixedDegrees.ToFloat(); set => pitchCentidegrees = InputAngleGrid.PitchFromFloat(value); }

        /// <summary>
        /// 玩家是否在当前 Tick 按下了跳跃键。
        /// 该字段表示一次跳跃输入事件，不表示玩家当前是否处于跳跃状态。
        /// </summary>
        public bool JumpPressed;
        /// <summary>持续快跑意图；短输入替代可跟随移动保留，进入中性替代时清除。</summary>
        public bool SprintHeld;
        /// <summary>瞬时冲刺按下边沿；所有替代输入必须清除，不能因丢包再次扣充能。</summary>
        public bool DashPressed;
        /// <summary>E/左肩的一次性蹬墙反弹请求；替代输入不得复用。</summary>
        public bool WallBouncePressed;
        /// <summary>玩家是否在当前 Tick 请求射击；这是一次性事件，替代输入不得复用。</summary>
        public bool FirePressed;
        /// <summary>一次性换弹请求；缺失输入不得重复启动换弹。</summary>
        public bool ReloadPressed;
        /// <summary>一次性切换到下一武器；缺失输入不能重复切换。</summary>
        public bool SwitchWeaponPressed;
        /// <summary>客户端为每次射击请求分配的单调递增序号；未射击时为零。</summary>
        public uint ShotSequence;
        /// <summary>客户端估计的开火服务器 Tick；服务器只在受限历史窗口内使用。</summary>
        public uint ShotServerTick;
    }
}

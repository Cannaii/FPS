using System;
using AFPS.Math;
using AFPS.NetCode.InputReplication;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.StateReplication;
using AFPS.NetCode.Transport;
using AFPS.NetCode.Weapons;
using AFPS.Simulation.Characters;
using AFPS.Simulation.Characters.Collision;
using AFPS.Simulation.Weapons;

namespace AFPS.NetCode.Sessions
{
    /// <summary>
    /// 组装单个已连接玩家的输入接收窗口、服务器权威模拟和状态发送。
    /// 每次服务器世界 Tick 最多处理一条连续客户端输入。
    /// </summary>
    public sealed partial class ServerAuthoritativeMovementSession
    {
        private ServerInputCommandReceiver inputReceiver;
        private ServerAuthoritativeStateSender stateSender;
        private readonly PlayerSimulationConfig simulationConfig;
        private readonly Fix tickDeltaTime;
        private readonly int maxMissingInputWaitTicks;
        private readonly int maxRepeatedMovementTicks;
        private readonly IFixedCharacterCollisionWorld collisionWorld;
        private readonly uint entityId;
        private ServerWeaponFireController weaponFireController;
        private IGameTransport weaponTransport;
        private TransportConnectionId weaponConnection;
        private readonly byte[] weaponStateBuffer = new byte[AuthoritativeWeaponStateCodec.PacketSize];
        private bool hasSentWeaponState;
        private AuthoritativeWeaponState lastSentWeaponState;
        private uint weaponStateSequence;
        private bool hasAdvancedServerTick;
        private bool hasLastReceivedInput;
        private uint lastServerTick;
        private int missingInputWaitTicks;
        private int consecutiveSubstitutedInputTicks;
        private PlayerInputCommand lastReceivedInput;

        /// <summary>
        /// 服务器处理完最近一条客户端输入后持有的未量化权威状态。
        /// </summary>
        public PlayerState CurrentState { get; private set; }

        /// <summary>
        /// 最近一次推进尝试使用真实输入、替代输入还是仍在等待输入。
        /// </summary>
        public ServerInputAdvanceStatus LastAdvanceStatus { get; private set; }

        /// <summary>
        /// 最近一次真正推进权威模拟时使用的输入。等待输入期间保持上一次的值。
        /// </summary>
        public PlayerInputCommand LastAppliedInput { get; private set; }
        public AFPS.NetCode.Metrics.ServerStateDriftTracker StateDrift { get; }
            = new AFPS.NetCode.Metrics.ServerStateDriftTracker();

        /// <summary>
        /// 当前缺失输入已经等待的服务器世界 Tick 数；收到真实连续输入后重置为零。
        /// </summary>
        public int MissingInputWaitTicks => missingInputWaitTicks;

        /// <summary>
        /// 当前连续使用替代输入推进模拟的 Tick 数；收到真实连续输入后重置为零。
        /// </summary>
        public int ConsecutiveSubstitutedInputTicks => consecutiveSubstitutedInputTicks;

        /// <summary>最近一次权威模拟 Tick 的射击验证结果；无武器或未请求射击时不接受。</summary>
        public WeaponFireResult LastWeaponFireResult { get; private set; }

        public bool GameplayEnabled { get; set; } = true;

        public bool WeaponEnabled { get; set; } = true;

        public void Teleport(UnityEngine.Vector3 position)
        {
            Teleport(new FixVec3(Fix.FromFloat(position.x), Fix.FromFloat(position.y), Fix.FromFloat(position.z)));
        }

        /// <summary>权威传送停止快跑/Dash，保留疲劳、充能、输入Tick和朝向；不能借传送刷新资源。</summary>
        public void Teleport(FixVec3 position)
        {
            PlayerState state = CurrentState;
            state.PositionFixed = position;
            state.VelocityFixed = FixVec3.Zero;
            // 目标未经过碰撞确认，不能凭传送授予落地和新的空中跳；下一Tick由运动器重新判定。
            state.IsGrounded = false;
            PlayerJumpSimulation.CancelGroundOpportunity(ref state);
            PlayerWallSimulation.ClearContact(ref state);
            state.IsSprinting = false;
            PlayerDashSimulation.Cancel(ref state);
            PlayerStanceSimulation.CancelSlide(ref state);
            state.IsWallRunning = false;
            PlayerClimbSimulation.Clear(ref state);
            PlayerGrappleSimulation.Cancel(ref state);
            state.ClimbCooldownRemainingFixed = simulationConfig.Climb.ReattachDelay;
            CurrentState = state;
        }

        /// <summary>权威重生恢复满体力，位置单位为米；输入Tick与射击序号沿用当前连接。</summary>
        public void Respawn(FixVec3 position)
        {
            Teleport(position);
            PlayerState restored = CurrentState;
            restored.StaminaSpentFixed = Fix.Zero;
            restored.SprintExhausted = false;
            restored.DashSpentCharges = 0;
            restored.DashRechargeRemainingFixed = Fix.Zero;
            restored.AirJumpsUsed = 0;
            restored.WallCooldownRemainingFixed = Fix.Zero;
            restored.IsCrouched = false;
            restored.WallRunUsedFixed = Fix.Zero;
            restored.ClimbCooldownRemainingFixed = Fix.Zero;
            restored.GrappleConsumed = false;
            CurrentState = restored;
            ResetLoadoutForRespawn();
            // 即使弹量恰好未改变，也必须向拥有者立即确认新生命的武器状态。
            hasSentWeaponState = false;
        }

        public ServerAuthoritativeMovementSession(IGameTransport transport, TransportConnectionId clientConnectionId, in PlayerState initialState, in PlayerSimulationConfig simulationConfig, float tickDeltaTime, int inputWindowCapacity, int maxMissingInputWaitTicks = 2, int maxRepeatedMovementTicks = 2, ICharacterCollisionWorld collisionWorld = null, uint entityId = 0, WeaponSimulationConfig? weaponConfig = null, IFixedCharacterCollisionWorld fixedCollisionWorld = null)
        {
            if (tickDeltaTime <= 0f || float.IsNaN(tickDeltaTime) || float.IsInfinity(tickDeltaTime))
            {
                throw new ArgumentOutOfRangeException(nameof(tickDeltaTime));
            }

            if (maxMissingInputWaitTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxMissingInputWaitTicks), "缺失输入等待 Tick 数不能小于零。");
            }

            if (maxRepeatedMovementTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxRepeatedMovementTicks), "持续移动复用 Tick 数不能小于零。");
            }

            this.simulationConfig = simulationConfig;
            this.tickDeltaTime = SimulationStep.FromSeconds(tickDeltaTime);
            this.maxMissingInputWaitTicks = maxMissingInputWaitTicks;
            this.maxRepeatedMovementTicks = maxRepeatedMovementTicks;
            if (collisionWorld != null && fixedCollisionWorld != null)
            {
                throw new ArgumentException("只能指定一种碰撞后端。", nameof(fixedCollisionWorld));
            }
            this.collisionWorld = fixedCollisionWorld ?? LegacyCharacterCollisionWorldAdapter.Adapt(collisionWorld);
            this.entityId = entityId;
            weaponTransport = transport;
            weaponConnection = clientConnectionId;
            if (weaponConfig.HasValue)
            {
                var crouched = new PlayerState { IsCrouched = true };
                weaponFireController = new ServerWeaponFireController(weaponConfig.Value,
                    PlayerStanceSimulation.EyeHeight(crouched, weaponConfig.Value.EyeHeightFixed, simulationConfig));
                primaryWeapon = weaponFireController;
                var secondaryConfig = WeaponCatalog.SecondaryFor(weaponConfig.Value);
                secondaryWeapon = new ServerWeaponFireController(secondaryConfig,
                    PlayerStanceSimulation.EyeHeight(crouched, secondaryConfig.EyeHeightFixed, simulationConfig));
                secondaryWeapon.SetEquipped(false);
            }
            CurrentState = initialState;
            StateDrift.Reset(initialState.Tick);
            inputReceiver = new ServerInputCommandReceiver(unchecked(initialState.Tick + 1), inputWindowCapacity);
            stateSender = new ServerAuthoritativeStateSender(transport, clientConnectionId);
        }

        /// <summary>
        /// 解码并保存客户端输入包。命令只进入接收窗口，不会在网络事件回调中直接推进世界模拟。
        /// </summary>
        public bool TryReceiveInputPacket(ArraySegment<byte> packet, out InputBatchReceiveResult result)
        {
            if (IsRetainedOffline || RecoverySnapshotPending)
            {
                result = default;
                return false;
            }
            return inputReceiver.TryReceivePacket(packet, out result);
        }

        /// <summary>
        /// 在一个新的服务器世界 Tick 中处理一条真实或安全替代输入，并发送对应权威确认。
        /// 缺失输入尚未达到等待上限时返回 false，服务器状态不会推进。
        /// </summary>
        public bool TryAdvance(uint serverWorldTick, out AuthoritativePlayerState authoritativeState, out AuthoritativeStateSendResult sendResult)
        {
            authoritativeState = default;
            sendResult = default;
            if (hasAdvancedServerTick && !SequenceMath.IsNewer(serverWorldTick, lastServerTick))
            {
                throw new ArgumentException("服务器世界 Tick 必须单调递增。", nameof(serverWorldTick));
            }

            hasAdvancedServerTick = true;
            lastServerTick = serverWorldTick;
            if (IsRetainedOffline || RecoverySnapshotPending)
            {
                // 恢复种子尚未完整入队时不消费输入，也不产生替代输入或重复开火。
                LastAdvanceStatus = ServerInputAdvanceStatus.WaitingForInput;
                return false;
            }
            if (inputReceiver.TryDequeueNext(out PlayerInputCommand command))
            {
                hasLastReceivedInput = true;
                lastReceivedInput = command;
                missingInputWaitTicks = 0;
                consecutiveSubstitutedInputTicks = 0;
                LastAdvanceStatus = ServerInputAdvanceStatus.ReceivedInput;
                return SimulateAndSend(serverWorldTick, command, out authoritativeState, out sendResult);
            }

            if (missingInputWaitTicks < maxMissingInputWaitTicks)
            {
                missingInputWaitTicks++;
                LastAdvanceStatus = ServerInputAdvanceStatus.WaitingForInput;
                return false;
            }

            if (!inputReceiver.TryAdvancePastMissingCommand(out uint substitutedTick))
            {
                throw new InvalidOperationException("输入接收窗口状态在同一个服务器 Tick 内发生了意外变化。");
            }

            bool repeatContinuousInput = hasLastReceivedInput && consecutiveSubstitutedInputTicks < maxRepeatedMovementTicks;
            command = lastReceivedInput.CreateFallback(substitutedTick, repeatContinuousInput);
            if (!hasLastReceivedInput)
            {
                // 尚未收到真实输入时只从初始权威状态取朝向；已有命令直接复制整数档位。
                command.SetFixedControls(command.MoveXFixed, command.MoveYFixed, CurrentState.YawFixedDegrees, CurrentState.PitchFixedDegrees);
            }
            consecutiveSubstitutedInputTicks++;
            LastAdvanceStatus = repeatContinuousInput ? ServerInputAdvanceStatus.RepeatedContinuousInput : ServerInputAdvanceStatus.NeutralFallback;
            return SimulateAndSend(serverWorldTick, command, out authoritativeState, out sendResult);
        }

        private bool SimulateAndSend(uint serverWorldTick, in PlayerInputCommand command, out AuthoritativePlayerState authoritativeState, out AuthoritativeStateSendResult sendResult)
        {
            PlayerInputCommand appliedCommand = command;
            if (!GameplayEnabled)
            {
                appliedCommand.MoveX = 0f;
                appliedCommand.MoveY = 0f;
                appliedCommand.JumpPressed = false;
                appliedCommand.SprintHeld = false;
                appliedCommand.DashPressed = false;
                appliedCommand.WallBouncePressed = false;
                appliedCommand.CrouchHeld = false;
                appliedCommand.GrappleHeld = false;
                PlayerState stoppedState = CurrentState;
                PlayerDashSimulation.Cancel(ref stoppedState);
                PlayerGrappleSimulation.Cancel(ref stoppedState);
                if (stoppedState.IsClimbing)
                {
                    PlayerClimbSimulation.Detach(ref stoppedState, simulationConfig.Climb);
                }
                CurrentState = stoppedState;
                appliedCommand.FirePressed = false;
                appliedCommand.ReloadPressed = false;
                appliedCommand.SwitchWeaponPressed = false;
                appliedCommand.ShotSequence = 0;
            }
            else if (!WeaponEnabled)
            {
                appliedCommand.FirePressed = false;
                appliedCommand.ReloadPressed = false;
                appliedCommand.SwitchWeaponPressed = false;
                appliedCommand.ShotSequence = 0;
            }

            ApplyWeaponSwitch(serverWorldTick, ref appliedCommand);
            LastAppliedInput = appliedCommand;
            CurrentState = PlayerSimulation.Simulate(CurrentState, appliedCommand, simulationConfig, tickDeltaTime, collisionWorld);
            StateDrift.Record(CurrentState, appliedCommand);
            LastWeaponFireResult = ProcessActiveWeapon(serverWorldTick, appliedCommand);
            SendWeaponState(serverWorldTick, command);
            SendShotRejections(serverWorldTick, command);
            authoritativeState = new AuthoritativePlayerState(serverWorldTick, appliedCommand.Tick, CurrentState);
            stateSender.TrySend(authoritativeState, out sendResult);
            return true;
        }
        private bool SendWeaponState(uint serverTick, in PlayerInputCommand command, bool force = false)
        {
            if (weaponFireController == null || entityId == 0)
            {
                return true;
            }
            var state = new AuthoritativeWeaponState(entityId, serverTick, command.Tick,
                weaponFireController.Ammo, weaponFireController.Capacity,
                weaponFireController.RemainingReloadTicks(serverTick), weaponFireController.IsEquipped,
                weaponFireController.WeaponId, weaponFireController.IsEquipped ? RemainingEquipTicks(serverTick) : 0);
            // 动作结算（包括拒绝）必须确认；空闲基线每 10 Tick 更新，避免每帧可靠包积压。
            if (!force && hasSentWeaponState && !command.FirePressed && !command.ReloadPressed && !command.SwitchWeaponPressed
                && state.Ammo == lastSentWeaponState.Ammo && state.IsReloading == lastSentWeaponState.IsReloading
                && state.IsEquipped == lastSentWeaponState.IsEquipped
                && state.IsEquipping == lastSentWeaponState.IsEquipping
                && unchecked(serverTick - lastSentWeaponState.ServerTick) < 10)
            {
                return true;
            }
            if (AuthoritativeWeaponStateCodec.TrySerialize(state, weaponStateSequence,
                new ArraySegment<byte>(weaponStateBuffer), out int count)
                && weaponTransport.Send(weaponConnection, TransportDelivery.ReliableSequenced,
                    new ArraySegment<byte>(weaponStateBuffer, 0, count)) == TransportSendResult.Success)
            {
                lastSentWeaponState = state;
                hasSentWeaponState = true;
                weaponStateSequence = unchecked(weaponStateSequence + 1);
                return true;
            }
            return false;
        }
    }
}

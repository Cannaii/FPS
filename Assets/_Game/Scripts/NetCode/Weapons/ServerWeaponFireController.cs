using AFPS.Math;
using AFPS.NetCode.Protocol;
using AFPS.Simulation.Characters;
using AFPS.Simulation.Weapons;

namespace AFPS.NetCode.Weapons
{
    /// <summary>验证客户端射击意图，并仅由权威玩家状态生成射击起点与方向。</summary>
    public sealed class ServerWeaponFireController
    {
        // 当前基础换弹规则：100 个服务器世界 Tick；50Hz 时约 2 秒。扩展为可配置值时必须加入世界指纹。
        public const uint ReloadDurationTicks = 100;
        private uint reloadStartedTick;
        public bool IsReloading { get; private set; }
        private readonly WeaponSimulationConfig config;
        private bool equipped = true;
        private bool hasObservedSequence;
        private bool hasAcceptedShot;
        private uint lastObservedSequence;
        private uint lastAcceptedServerTick;

        public int Ammo { get; private set; }
        public bool IsEquipped => equipped;

        public ServerWeaponFireController(in WeaponSimulationConfig config)
        {
            if (!config.IsValid)
            {
                throw new System.ArgumentException("武器配置无效。", nameof(config));
            }

            this.config = config;
            Ammo = config.MagazineCapacity;
        }

        public void SetEquipped(bool value)
        {
            equipped = value;
            if (!value)
            {
                IsReloading = false;
            }
        }

        /// <summary>测试/出生重置入口；玩家换弹必须通过 ReloadPressed 和服务器 Tick 计时。</summary>
        public void Reload()
        {
            Ammo = config.MagazineCapacity;
            IsReloading = false;
        }

        public WeaponFireResult Process(
            uint serverTick,
            uint entityId,
            in PlayerInputCommand command,
            in PlayerState authoritativeState)
        {
            if (IsReloading && unchecked(serverTick - reloadStartedTick) >= ReloadDurationTicks)
            {
                Ammo = config.MagazineCapacity;
                IsReloading = false;
            }
            // 同 Tick 换弹优先；重复请求不重置计时，满弹匣忽略换弹请求。
            if (command.ReloadPressed && equipped && !IsReloading && Ammo < config.MagazineCapacity)
            {
                IsReloading = true;
                reloadStartedTick = serverTick;
            }
            if (!command.FirePressed)
            {
                return Reject(WeaponFireRejectReason.NotRequested);
            }

            if (command.ShotSequence == 0)
            {
                return Reject(WeaponFireRejectReason.InvalidSequence);
            }

            if (hasObservedSequence && !SequenceMath.IsNewer(command.ShotSequence, lastObservedSequence))
            {
                return Reject(WeaponFireRejectReason.DuplicateOrOldSequence);
            }

            // 即使后续验证失败也消费序号，避免同一请求反复尝试；失败不消耗弹药或冷却。
            hasObservedSequence = true;
            lastObservedSequence = command.ShotSequence;
            if (!equipped)
            {
                return Reject(WeaponFireRejectReason.WeaponUnavailable);
            }

            if (IsReloading)
            {
                return Reject(WeaponFireRejectReason.Reloading);
            }

            if (hasAcceptedShot && unchecked(serverTick - lastAcceptedServerTick) < config.FireIntervalTicks)
            {
                return Reject(WeaponFireRejectReason.FireRateLimited);
            }

            if (Ammo <= 0)
            {
                return Reject(WeaponFireRejectReason.EmptyMagazine);
            }

            if (!WeaponAim.TryGetDirection(authoritativeState.YawFixedDegrees,
                authoritativeState.PitchFixedDegrees, out FixVec3 direction))
            {
                return Reject(WeaponFireRejectReason.InvalidAim);
            }

            // 眼高相加使用宽 Raw，避免大坐标经 float 丢失低位；越界时不扣弹、不启动冷却。
            long originY = (long)authoritativeState.PositionFixed.Y.Raw + config.EyeHeightFixed.Raw;
            if (originY > int.MaxValue || originY < int.MinValue)
            {
                return Reject(WeaponFireRejectReason.InvalidOrigin);
            }

            FixVec3 origin = new FixVec3(authoritativeState.PositionFixed.X,
                Fix.FromRaw((int)originY), authoritativeState.PositionFixed.Z);
            AuthoritativeShot shot = new AuthoritativeShot(
                entityId, command.ShotSequence, serverTick, config,
                origin, direction, command.ShotServerTick);
            Ammo--;
            hasAcceptedShot = true;
            lastAcceptedServerTick = serverTick;
            return new WeaponFireResult(true, WeaponFireRejectReason.None, Ammo, shot);
        }

        private WeaponFireResult Reject(WeaponFireRejectReason reason)
        {
            AuthoritativeShot noShot = default;
            return new WeaponFireResult(false, reason, Ammo, noShot);
        }
    }
}

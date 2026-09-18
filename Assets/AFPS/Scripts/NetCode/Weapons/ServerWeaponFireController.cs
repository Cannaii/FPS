using AFPS.NetCode.Protocol;
using AFPS.Simulation.Characters;
using AFPS.Simulation.Weapons;
using UnityEngine;

namespace AFPS.NetCode.Weapons
{
    /// <summary>验证客户端射击意图，并仅由权威玩家状态生成射击起点与方向。</summary>
    public sealed class ServerWeaponFireController
    {
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

        public void SetEquipped(bool value) => equipped = value;

        public void Reload() => Ammo = config.MagazineCapacity;

        public WeaponFireResult Process(uint serverTick, uint entityId, in PlayerInputCommand command, in PlayerState authoritativeState)
        {
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

            hasObservedSequence = true;
            lastObservedSequence = command.ShotSequence;
            if (!equipped)
            {
                return Reject(WeaponFireRejectReason.WeaponUnavailable);
            }

            if (hasAcceptedShot && unchecked(serverTick - lastAcceptedServerTick) < config.FireIntervalTicks)
            {
                return Reject(WeaponFireRejectReason.FireRateLimited);
            }

            if (Ammo <= 0)
            {
                return Reject(WeaponFireRejectReason.EmptyMagazine);
            }

            if (!TryGetAimDirection(authoritativeState.Yaw, authoritativeState.Pitch, out Vector3 direction))
            {
                return Reject(WeaponFireRejectReason.InvalidAim);
            }

            Vector3 origin = authoritativeState.Position + Vector3.up * config.EyeHeight;
            AuthoritativeShot shot = new AuthoritativeShot(entityId, command.ShotSequence, serverTick, config, origin, direction, command.ShotServerTick);
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

        private static bool TryGetAimDirection(float yaw, float pitch, out Vector3 direction)
        {
            direction = default;
            if (float.IsNaN(yaw) || float.IsInfinity(yaw) || float.IsNaN(pitch) || float.IsInfinity(pitch) || pitch < -89.9f || pitch > 89.9f)
            {
                return false;
            }

            direction = Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward;
            return direction.sqrMagnitude > 0.999f && direction.sqrMagnitude < 1.001f;
        }
    }
}

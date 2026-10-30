using AFPS.NetCode.Protocol;
using AFPS.NetCode.Weapons;
using AFPS.Simulation.Characters;
using AFPS.Simulation.Weapons;

namespace AFPS.NetCode.Sessions
{
    public sealed partial class ServerAuthoritativePlayerSession
    {
        private ServerWeaponFireController primaryWeapon;
        private ServerWeaponFireController secondaryWeapon;
        private bool hasSwitchedWeapon;
        private uint switchedWeaponTick;
        private bool hasObservedGlobalShot;
        private uint lastGlobalShotSequence;

        internal RemoteWeaponState CreateRemoteWeaponState(uint serverTick, bool active)
        {
            if (weaponFireController == null)
            {
                return default;
            }
            bool equipped = active && weaponFireController.IsEquipped;
            return new RemoteWeaponState(entityId, serverTick, weaponFireController.WeaponId,
                equipped ? weaponFireController.RemainingReloadTicks(serverTick) : 0,
                equipped ? RemainingEquipTicks(serverTick) : 0, equipped);
        }

        private uint RemainingEquipTicks(uint tick) => hasSwitchedWeapon
            ? WeaponCatalog.EquipDurationTicks - System.Math.Min(WeaponCatalog.EquipDurationTicks, unchecked(tick - switchedWeaponTick))
            : 0;

        private void ApplyWeaponSwitch(uint tick, ref PlayerInputCommand command)
        {
            if (!command.SwitchWeaponPressed || weaponFireController == null)
            {
                return;
            }
            // 换武器取消旧武器换弹，保留各自弹匣和冷却；同 Tick 不能又切换又开火/换弹。
            weaponFireController.SetEquipped(false);
            weaponFireController = weaponFireController == primaryWeapon ? secondaryWeapon : primaryWeapon;
            switchedWeaponTick = tick;
            hasSwitchedWeapon = true;
            hasSentWeaponState = false;
            command.FirePressed = false;
            command.ReloadPressed = false;
            command.ShotSequence = 0;
        }

        private WeaponFireResult ProcessActiveWeapon(uint tick, in PlayerInputCommand command)
        {
            if (weaponFireController == null)
            {
                return default;
            }
            weaponFireController.SetEquipped(GameplayEnabled && WeaponEnabled);
            var applied = command;
            WeaponFireRejectReason reject = WeaponFireRejectReason.None;
            if (command.FirePressed)
            {
                // 序号属于连接而非武器；切换到尚未开过火的武器也不能重放旧射击。
                if (hasObservedGlobalShot && !SequenceMath.IsNewer(command.ShotSequence, lastGlobalShotSequence))
                {
                    reject = WeaponFireRejectReason.DuplicateOrOldSequence;
                }
                else
                {
                    hasObservedGlobalShot = true;
                    lastGlobalShotSequence = command.ShotSequence;
                    if (RemainingEquipTicks(tick) > 0)
                    {
                        reject = WeaponFireRejectReason.WeaponUnavailable;
                    }
                }
            }
            if (RemainingEquipTicks(tick) > 0)
            {
                applied.ReloadPressed = false;
            }
            if (reject != WeaponFireRejectReason.None)
            {
                applied.FirePressed = false;
            }
            var result = weaponFireController.Process(tick, entityId, applied, CurrentState);
            return reject == WeaponFireRejectReason.None ? result
                : new WeaponFireResult(false, reject, weaponFireController.Ammo, default);
        }

        private void ResetLoadoutForRespawn()
        {
            primaryWeapon?.ResetForRespawn();
            secondaryWeapon?.ResetForRespawn();
            secondaryWeapon?.SetEquipped(false);
            weaponFireController = primaryWeapon;
            hasSwitchedWeapon = false;
            // 全局射击序号保留；新生命不能重放旧生命的请求。
        }
    }
}

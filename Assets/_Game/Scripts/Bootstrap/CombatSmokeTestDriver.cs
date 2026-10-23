using System;
using System.Collections.Generic;
using AFPS.Input;
using AFPS.NetCode.Match;
using AFPS.Simulation.Weapons;
using UnityEngine;

namespace AFPS.Bootstrap
{
    /// <summary>仅命令行启用的真实双端战斗回归；不在正常玩家流程中运行。</summary>
    [DefaultExecutionOrder(100)]
    public sealed class CombatSmokeTestDriver : MonoBehaviour
    {
        public static bool IsRequested => Array.Exists(Environment.GetCommandLineArgs(),
            value => value.Equals("-afpsCombatSmoke", StringComparison.OrdinalIgnoreCase));
        private UnityNetworkMovementController movement;
        private LocalPlayerInputCollector input;
        private readonly List<uint> entities = new List<uint>();
        private double configuredAt;
        private double startedAt = -1;
        private bool switchOut;
        private bool launcherFire;
        private bool switchBack;
        private bool rifleFire;
        private bool switchAgain;
        private bool reload;
        private bool finalSwitch;
        private bool forcedFirst;
        private bool forcedSecond;
        private uint forcedInputTick;
        private bool sawLaunch;
        private bool sawImpact;
        private bool sawDeath;
        private bool sawRespawn;
        private bool rifleAmmo;
        private bool launcherAmmo;
        private bool reloadStarted;
        private bool reloadCompleted;
        private bool rejected;
        private bool damage;
        private bool synced;
        private uint localEntity;
        private bool rifleView;
        private bool launcherView;
        private bool reloadView;
        private bool explosionView;
        private bool remoteMuzzle;

        public void Configure(UnityNetworkMovementController controller, LocalPlayerInputCollector collector)
        {
            movement = controller;
            input = collector;
            configuredAt = Time.realtimeSinceStartupAsDouble;
            Application.runInBackground = true;
        }

        private bool Passed => sawLaunch && sawImpact && sawDeath && sawRespawn && synced && explosionView
            && (localEntity == 1
                ? rifleAmmo && launcherAmmo && reloadCompleted && rejected && rifleView && launcherView && reloadView
                : damage && remoteMuzzle && rifleView);

        private void Update()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            var manager = movement.SessionManager;
            if (manager?.ClientSession == null)
            {
                if (Passed || now - configuredAt > 20d)
                {
                    Finish(Passed);
                }
                return;
            }
            var replicas = manager.RemotePlayers;
            if (replicas == null || replicas.LocalEntityId == 0 || replicas.Count == 0
                || !manager.ClientWeapon.HasState || manager.ClientDeathmatchState == null)
            {
                if (now - configuredAt > 20d)
                {
                    Finish(false);
                }
                return;
            }
            localEntity = replicas.LocalEntityId;
            if (startedAt < 0)
            {
                startedAt = now;
            }
            double elapsed = now - startedAt;
            var state = manager.ClientWeapon.State;
            bool launcher = manager.ClientWeaponConfiguration.FireMode == WeaponFireMode.Projectile;
            sawLaunch |= movement.ProjectileLaunchCount > 0;
            sawImpact |= movement.ProjectileImpactCount > 0;
            rejected |= movement.RejectedShotCount > 0;
            damage |= movement.ReceivedDamageCount > 0;
            synced |= manager.HasTimeSync;
            rifleView |= movement.PresentedWeaponId == WeaponSimulationConfig.DefaultRifle.WeaponId;
            launcherView |= movement.PresentedWeaponId == WeaponCatalog.SecondaryFor(WeaponSimulationConfig.DefaultRifle).WeaponId;
            reloadView |= movement.PresentedReloading;
            explosionView |= movement.ExplosionEffectCount > 0;
            remoteMuzzle |= movement.RemoteMuzzleCount > 0;
            if (manager.ClientDeathmatchState.TryGetPlayer(2, out var victim))
            {
                sawDeath |= !victim.IsAlive;
                sawRespawn |= sawDeath && victim.IsAlive;
            }

            bool fireNow = false;
            bool switchNow = false;
            bool reloadNow = false;
            if (localEntity == 1)
            {
                rifleAmmo |= !launcher && rifleFire && state.Ammo == 29;
                launcherAmmo |= launcher && switchAgain && state.Ammo == 4;
                reloadStarted |= launcher && state.IsReloading;
                reloadCompleted |= launcher && reloadStarted && !state.IsReloading && state.Ammo == 5;
                if (!switchOut && elapsed >= 0.2d)
                {
                    switchOut = switchNow = true;
                }
                if (!launcherFire && launcher && manager.ClientWeapon.CanFire && elapsed >= 0.6d)
                {
                    launcherFire = fireNow = true;
                }
                if (!switchBack && launcherFire && launcher && state.Ammo == 4 && elapsed >= 1.4d)
                {
                    switchBack = switchNow = true;
                }
                if (!rifleFire && switchBack && !launcher && manager.ClientWeapon.CanFire && elapsed >= 2d)
                {
                    rifleFire = fireNow = true;
                }
                if (!switchAgain && rifleAmmo && elapsed >= 4.8d)
                {
                    switchAgain = switchNow = true;
                }
                if (!reload && launcherAmmo && manager.ClientWeapon.CanReload && elapsed >= 5.2d)
                {
                    reload = reloadNow = true;
                }
                if (!finalSwitch && reloadCompleted && elapsed >= 7.6d)
                {
                    finalSwitch = switchNow = true;
                }
                if (!forcedFirst && finalSwitch && !launcher && manager.ClientWeapon.CanFire && elapsed >= 9d)
                {
                    forcedFirst = fireNow = true;
                    forcedInputTick = unchecked(manager.ClientSession.CurrentState.Tick + 1);
                }
                else if (forcedFirst && !forcedSecond && manager.ClientSession.CurrentState.Tick >= forcedInputTick)
                {
                    // 下一输入 Tick 再请求一次射击，必须由真实服务器射速裁决返回拒绝。
                    forcedSecond = fireNow = true;
                }
            }
            float yaw = 0f;
            float pitch = 0f;
            replicas.CopyEntityIds(entities);
            foreach (uint entity in entities)
            {
                if (replicas.TrySample(entity, replicas.LatestServerTick, out var target))
                {
                    Vector3 direction = target.Position + Vector3.up - manager.ClientSession.CurrentState.Position - Vector3.up * 1.6f;
                    yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                    pitch = -Mathf.Atan2(direction.y, new Vector2(direction.x, direction.z).magnitude) * Mathf.Rad2Deg;
                    break;
                }
            }
            input.SetAutomationInput(0, 0, yaw, pitch, false, firePressed: fireNow,
                reloadPressed: reloadNow, switchWeaponPressed: switchNow);
            if (elapsed >= 11d || now - configuredAt > 25d)
            {
                Finish(Passed);
            }
        }

        private void Finish(bool success)
        {
            enabled = false;
            input.ClearAutomationInput();
            Debug.Log($"[AFPS_COMBAT_RESULT] {(success ? "PASS" : "FAIL")} entity={localEntity}"
                + $" launch={sawLaunch} impact={sawImpact} death={sawDeath} respawn={sawRespawn}"
                + $" rifleAmmo={rifleAmmo} launcherAmmo={launcherAmmo} reload={reloadCompleted}"
                + $" rejected={rejected} damage={damage} synced={synced} rifleView={rifleView}"
                + $" launcherView={launcherView} reloadView={reloadView} explosionView={explosionView} remoteMuzzle={remoteMuzzle}");
            Application.Quit(success ? 0 : 2);
        }
    }
}

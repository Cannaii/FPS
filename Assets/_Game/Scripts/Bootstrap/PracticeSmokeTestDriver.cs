using System;
using AFPS.Input;
using AFPS.NetCode.Match;
using AFPS.NetCode.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace AFPS.Bootstrap
{
    /// <summary>仅-afpsPracticeSmoke启用；从真实菜单点击单人练习，验证单客户端操作闭环。</summary>
    [DefaultExecutionOrder(100)]
    public sealed class PracticeSmokeTestDriver : MonoBehaviour
    {
        public static bool IsRequested => Array.Exists(Environment.GetCommandLineArgs(),
            value => value.Equals("-afpsPracticeSmoke", StringComparison.OrdinalIgnoreCase));
        private UnityNetworkBootstrap bootstrap;
        private UnityNetworkMovementController movement;
        private LocalPlayerInputCollector input;
        private double configuredAt;
        private double startedAt = -1d;
        private bool menuClicked;
        private bool moved;
        private bool jumped;
        private bool rifle;
        private bool automatic;
        private bool reload;
        private bool refilled;
        private bool launcher;
        private bool rifleView;
        private bool launcherView;
        private bool reloadView;
        private int nextEvent;
        private Vector3 origin;

        public void Configure(UnityNetworkBootstrap owner)
        {
            bootstrap = owner;
            movement = owner.GetComponent<UnityNetworkMovementController>();
            input = FindObjectOfType<LocalPlayerInputCollector>(true);
            configuredAt = Time.realtimeSinceStartupAsDouble;
            Application.runInBackground = true;
        }

        private void Update()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (!menuClicked && bootstrap.Runtime == null)
            {
                foreach (var button in FindObjectsOfType<Button>())
                {
                    if (button.name == "Practice Button")
                    {
                        menuClicked = true;
                        button.onClick.Invoke();
                        break;
                    }
                }
            }
            var manager = movement.SessionManager;
            bool ready = movement.CanEnterGame && input.enabled && manager?.ClientDeathmatchState != null
                && manager.ClientDeathmatchState.Phase == DeathmatchPhase.Playing;
            if (!ready)
            {
                if (now - configuredAt > 20d)
                {
                    Finish(false);
                }
                return;
            }
            if (startedAt < 0d)
            {
                startedAt = now;
                origin = manager.ClientSession.CurrentState.Position;
            }
            double elapsed = now - startedAt;
            var state = manager.ClientSession.LastAuthoritativeState;
            moved |= Vector2.Distance(new Vector2(origin.x, origin.z), new Vector2(state.Position.x, state.Position.z)) > 0.5f;
            jumped |= state.Position.y > origin.y + 0.3f;
            rifle |= manager.ShotResultVersion > 0 && manager.LastReceivedShotResult.Shot.WeaponId == 1;
            automatic |= manager.ShotResultVersion >= 4;
            reload |= manager.ClientWeapon.HasState && manager.ClientWeapon.State.IsReloading;
            refilled |= reload && manager.ClientWeapon.HasState && manager.ClientWeapon.State.Ammo == 30;
            launcher |= manager.ClientWeapon.HasState && manager.ClientWeapon.State.WeaponId == 2
                && manager.ClientWeapon.State.Ammo == 4;
            rifleView |= movement.PresentedWeaponId == 1;
            launcherView |= movement.PresentedWeaponId == 2;
            reloadView |= movement.PresentedReloading;

            bool jump = nextEvent == 0 && elapsed >= 0.1d;
            bool requestReload = nextEvent == 1 && elapsed >= 1.5d;
            bool switchWeapon = nextEvent == 2 && elapsed >= 4.3d;
            bool fireLauncher = nextEvent == 3 && elapsed >= 5d;
            if (jump || requestReload || switchWeapon || fireLauncher)
            {
                nextEvent++;
            }
            input.SetAutomationInput(0f, elapsed < 1d ? 1f : 0f, 0f, 0f, jump,
                firePressed: fireLauncher, fireHeld: elapsed >= 0.2d && elapsed < 1.2d,
                reloadPressed: requestReload, switchWeaponPressed: switchWeapon);
            if (elapsed >= 7d)
            {
                bool passed = menuClicked && bootstrap.ActiveMode == NetworkLaunchMode.Practice
                    && manager.IsLocalPractice && manager.ServerSessionCount == 1
                    && FindObjectOfType<NetworkConnectionMenu>() == null
                    && moved && jumped && rifle && automatic && reload && refilled && launcher
                    && rifleView && launcherView && reloadView && movement.PresentedShotResultCount > 0;
                Finish(passed);
            }
        }

        private void Finish(bool passed)
        {
            enabled = false;
            input?.ClearAutomationInput();
            Debug.Log($"[AFPS_PRACTICE_RESULT] {(passed ? "PASS" : "FAIL")} menu={menuClicked}"
                + $" moved={moved} jumped={jumped} rifle={rifle} auto={automatic} reload={reload}"
                + $" refilled={refilled} launcher={launcher} rifleView={rifleView}"
                + $" launcherView={launcherView} reloadView={reloadView}");
            Application.Quit(passed ? 0 : 2);
        }
    }
}

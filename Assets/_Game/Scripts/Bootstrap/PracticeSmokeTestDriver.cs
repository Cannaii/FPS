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
        private bool checkedArms;
        private bool singleArms = true;
        private int capturePhase;

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
            if (movement.PresentedWeaponId != 0 && Camera.main != null)
            {
                int arms = 0;
                foreach (var renderer in Camera.main.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if (renderer.enabled && renderer.name.IndexOf("arm", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        arms++;
                    }
                }
                checkedArms = true;
                singleArms &= arms == 1;
            }
            CapturePresentation(elapsed);

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
                    && rifleView && launcherView && reloadView && movement.PresentedShotResultCount > 0
                    && checkedArms && singleArms;
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
                + $" launcherView={launcherView} reloadView={reloadView} singleArms={checkedArms && singleArms}");
            Application.Quit(passed ? 0 : 2);
        }

        private void CapturePresentation(double elapsed)
        {
            if (!Array.Exists(Environment.GetCommandLineArgs(),
                value => value.Equals("-afpsPracticeCapture", StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }
            // 显式图形验收才保存截图；秒数分别覆盖步枪、换弹和切换后的发射器。
            if ((capturePhase == 0 && elapsed >= 0.8d)
                || (capturePhase == 1 && elapsed >= 2d) || (capturePhase == 2 && elapsed >= 5.4d))
            {
                string[] names = { "rifle", "reload", "launcher" };
                string directory = System.IO.Path.Combine(Environment.CurrentDirectory, "Temp");
                System.IO.Directory.CreateDirectory(directory);
                CaptureCameras(System.IO.Path.Combine(directory, "hands-fix-" + names[capturePhase] + ".png"));
                capturePhase++;
            }
        }

        private static void CaptureCameras(string path)
        {
            Camera main = Camera.main;
            if (main == null)
            {
                return;
            }
            // 隐藏的batchmode窗口可能没有有效后缓冲。按深度渲染世界相机与武器相机，
            // 保存实际模型/动画的合成结果；不包含ScreenSpaceOverlay HUD，不修改相机原目标。
            var target = RenderTexture.GetTemporary(Screen.width, Screen.height, 24);
            RenderTexture previous = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                Camera[] cameras = main.GetComponentsInChildren<Camera>();
                Array.Sort(cameras, (left, right) => left.depth.CompareTo(right.depth));
                foreach (var camera in cameras)
                {
                    if (!camera.enabled)
                    {
                        continue;
                    }
                    RenderTexture original = camera.targetTexture;
                    try
                    {
                        camera.targetTexture = target;
                        camera.Render();
                    }
                    finally
                    {
                        camera.targetTexture = original;
                    }
                }
                RenderTexture.active = target;
                pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0f, 0f, target.width, target.height), 0, 0);
                pixels.Apply();
                System.IO.File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                if (pixels != null)
                {
                    Destroy(pixels);
                }
            }
        }
    }
}

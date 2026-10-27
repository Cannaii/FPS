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
        private bool menuCaptured;
        private double scenarioSeconds;
        private double lastScenarioUpdate;

        public void Configure(UnityNetworkBootstrap owner)
        {
            bootstrap = owner;
            movement = owner.GetComponent<UnityNetworkMovementController>();
            input = FindObjectOfType<LocalPlayerInputCollector>(true);
            configuredAt = Time.realtimeSinceStartupAsDouble;
            Application.runInBackground = true;
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                // 隐藏验证窗口不等待显示器垂直同步，避免Present阻塞改变自动化输入调度。
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = 60;
            }
        }

        private void Update()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (!menuClicked && bootstrap.Runtime == null)
            {
                if (!menuCaptured && CaptureRequested)
                {
                    menuCaptured = true;
                    CaptureCameras(CapturePath("menu.png"), true);
                    return;
                }
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
                lastScenarioUpdate = now;
            }
            // 首次字体/Shader编译或截图可能停顿数秒，验证脚本不能直接跳过整段开火/移动窗口。
            // 只限制自动化调度的单帧步长，服务器Tick和真实换弹计时仍按原逻辑运行。
            scenarioSeconds += System.Math.Min(0.05d, System.Math.Max(0d, now - lastScenarioUpdate));
            lastScenarioUpdate = now;
            double elapsed = scenarioSeconds;
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
                CaptureCameras(CapturePath(names[capturePhase] + ".png"), true);
                capturePhase++;
            }
        }

        private static bool CaptureRequested => SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null
            && Array.Exists(Environment.GetCommandLineArgs(), value => value.Equals("-afpsPracticeCapture", StringComparison.OrdinalIgnoreCase));

        private static string CapturePath(string name)
        {
            string directory = System.IO.Path.Combine(Environment.CurrentDirectory,
                "Library/AFPSVerification/Results/ChineseUI-" + Screen.width + "x" + Screen.height);
            System.IO.Directory.CreateDirectory(directory);
            return System.IO.Path.Combine(directory, name);
        }

        internal static void CaptureCameras(string path, bool includeUi = false)
        {
            Camera main = Camera.main;
            if (main == null)
            {
                return;
            }
            // 隐藏的batchmode窗口可能没有有效后缓冲。按深度渲染世界相机与武器相机，
            // 默认保存世界/武器合成，includeUi时额外合成HUD；所有相机目标均在finally恢复。
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
                if (includeUi)
                {
                    CaptureOverlay(target);
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

        private static void CaptureOverlay(RenderTexture target)
        {
            // 隐藏窗口的后缓冲可能全黑；临时用专用相机合成Overlay，finally恢复玩家Canvas状态。
            var cameraRoot = new GameObject("Verification UI Camera");
            var camera = cameraRoot.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = target.height * 0.5f;
            camera.cullingMask = 1 << 30;
            camera.clearFlags = CameraClearFlags.Depth;
            camera.targetTexture = target;
            Canvas[] canvases = FindObjectsOfType<Canvas>();
            Array.Sort(canvases, (left, right) => left.sortingOrder.CompareTo(right.sortingOrder));
            try
            {
                foreach (var canvas in canvases)
                {
                    if (!canvas.isRootCanvas || !canvas.isActiveAndEnabled || canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    {
                        continue;
                    }
                    Camera originalCamera = canvas.worldCamera;
                    float distance = canvas.planeDistance;
                    Transform[] children = canvas.GetComponentsInChildren<Transform>(true);
                    int[] layers = new int[children.Length];
                    for (int i = 0; i < children.Length; i++)
                    {
                        layers[i] = children[i].gameObject.layer;
                        children[i].gameObject.layer = 30;
                    }
                    try
                    {
                        canvas.renderMode = RenderMode.ScreenSpaceCamera;
                        canvas.worldCamera = camera;
                        canvas.planeDistance = 1f;
                        Canvas.ForceUpdateCanvases();
                        foreach (var text in canvas.GetComponentsInChildren<TMPro.TMP_Text>())
                        {
                            text.ForceMeshUpdate();
                        }
                        camera.Render();
                    }
                    finally
                    {
                        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                        canvas.worldCamera = originalCamera;
                        canvas.planeDistance = distance;
                        for (int i = 0; i < children.Length; i++)
                        {
                            children[i].gameObject.layer = layers[i];
                        }
                        Canvas.ForceUpdateCanvases();
                    }
                }
            }
            finally
            {
                camera.targetTexture = null;
                Destroy(cameraRoot);
            }
        }
    }
}

using System;
using AFPS.NetCode.Runtime;
using UnityEngine;

namespace AFPS.Bootstrap
{
    /// <summary>显式命令行回归入口：两个客户端正常离开后让独服自行退出，避免强杀掩盖清理问题。</summary>
    public sealed class ServerShutdownSmokeTestDriver : MonoBehaviour
    {
        // 秒，单调实时时间；仅用于自动验证，不改变正常独服生命周期。
        private const double TimeoutSeconds = 40;
        private UnityNetworkBootstrap bootstrap;
        private UnityNetworkMovementController movement;
        private double startedAt;
        private bool sawTwoPlayers;

        public static bool IsRequested => Array.Exists(Environment.GetCommandLineArgs(),
            argument => argument.Equals("-afpsShutdownSmoke", StringComparison.OrdinalIgnoreCase));

        public void Configure(UnityNetworkBootstrap owner)
        {
            bootstrap = owner;
            movement = owner.GetComponent<UnityNetworkMovementController>();
            startedAt = Time.realtimeSinceStartupAsDouble;
        }

        private void Update()
        {
            if (bootstrap.ActiveMode != NetworkLaunchMode.DedicatedServer)
            {
                Finish(false, "expectedDedicatedServer=True");
                return;
            }

            var manager = movement != null ? movement.SessionManager : null;
            if (manager != null)
            {
                sawTwoPlayers |= manager.ServerSessionCount >= 2;
                if (sawTwoPlayers && manager.ServerSessionCount == 0)
                {
                    Finish(true, "sawTwoPlayers=True remainingConnections=0");
                    return;
                }
            }

            if (Time.realtimeSinceStartupAsDouble - startedAt >= TimeoutSeconds)
            {
                Finish(false, $"timeout=True sawTwoPlayers={sawTwoPlayers}");
            }
        }

        private void Finish(bool passed, string details)
        {
            enabled = false;
            Debug.Log($"[AFPS_SHUTDOWN_RESULT] {(passed ? "PASS" : "FAIL")} {details}");
            Application.Quit(passed ? 0 : 2);
        }
    }
}

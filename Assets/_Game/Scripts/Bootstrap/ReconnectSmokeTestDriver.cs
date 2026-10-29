using System;
using System.IO;
using AFPS.Input;
using AFPS.NetCode.Runtime;
using AFPS.NetCode.Sessions;
using AFPS.NetCode.Transport;
using UnityEngine;

namespace AFPS.Bootstrap
{
    /// <summary>显式命令行双端回归：两次真实驱动断线、自动重拨、同身份恢复及恢复后开火。</summary>
    public sealed class ReconnectSmokeTestDriver : MonoBehaviour
    {
        public static bool IsRequested => Array.Exists(Environment.GetCommandLineArgs(),
            argument => argument.Equals("-afpsReconnectSmoke", StringComparison.OrdinalIgnoreCase));

        private UnityNetworkBootstrap bootstrap;
        private UnityNetworkMovementController movement;
        private LocalPlayerInputCollector input;
        private NetworkMovementSessionManager originalManager;
        private IGameTransport originalTransport;
        private uint entityId;
        private double startedAt;
        private double phaseStartedAt;
        private int phase;
        private bool fired;
        private bool captured;
        private int ammoBeforeDisconnect;
        private int ammoAfterFirst;
        private bool ammoPreserved;
        private bool postRecoveryFire;
        private Vector3 origin;

        public void Configure(UnityNetworkBootstrap owner, UnityNetworkMovementController controller)
        {
            bootstrap = owner;
            movement = controller;
            input = FindObjectOfType<LocalPlayerInputCollector>(true);
            originalManager = controller.SessionManager;
            originalTransport = owner.Runtime.ClientTransport;
            startedAt = Time.realtimeSinceStartupAsDouble;
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
        }

        private void Update()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            var manager = movement.SessionManager;
            if (manager?.ClientSession != null && !SmokeTestLobby.EnsurePlaying(movement))
            {
                return;
            }
            if (now - startedAt >= 25)
            {
                Finish(false, "自动重连回归超时。" + bootstrap.Reconnect.Message);
                return;
            }
            bool ready = manager.CanPredictClientInput && manager.ClientResumeCredential.IsValid
                && manager.RemotePlayers.LocalEntityId != 0;
            if (phase == 0)
            {
                if (!ready || manager.RemotePlayers.Count == 0 || !manager.ClientWeapon.CanFire)
                {
                    return;
                }
                entityId = manager.RemotePlayers.LocalEntityId;
                origin = manager.ClientSession.CurrentState.Position;
                phase = 1;
                phaseStartedAt = now;
            }
            if (phase == 2 || phase == 4)
            {
                if (phase == 2 && !captured && now - phaseStartedAt > 0.1
                    && Array.Exists(Environment.GetCommandLineArgs(), argument => argument == "-afpsReconnectCapture"))
                {
                    captured = true;
                    PracticeSmokeTestDriver.CaptureCameras(Path.Combine(Path.GetDirectoryName(Application.consoleLogPath),
                        "reconnect-overlay-" + entityId + ".png"), true);
                }
                uint expectedRecoveries = phase == 2 ? 1u : 2u;
                if (!ready || bootstrap.Reconnect.CompletedRecoveryCount < expectedRecoveries)
                {
                    return;
                }
                if (manager.RemotePlayers.LocalEntityId != entityId || !ReferenceEquals(manager, originalManager)
                    || !ReferenceEquals(bootstrap.Runtime.ClientTransport, originalTransport))
                {
                    Finish(false, "恢复时改变了实体、管理器或传输对象。");
                    return;
                }
                ammoPreserved |= phase == 2 && manager.ClientWeapon.Ammo == ammoBeforeDisconnect;
                phase++;
                phaseStartedAt = now;
                fired = false;
            }
            if (!ready)
            {
                return;
            }
            bool fire = !fired && now - phaseStartedAt >= 0.15 && manager.ClientWeapon.CanFire;
            fired |= fire;
            input.SetAutomationInput(0, 0.5f, 0, 0, false, fire);
            if (now - phaseStartedAt < 0.9)
            {
                return;
            }
            if (phase == 1)
            {
                ammoBeforeDisconnect = manager.ClientWeapon.State.Ammo;
                bootstrap.DisconnectClientForRecovery();
                phase = 2;
                phaseStartedAt = now;
            }
            else if (phase == 3)
            {
                ammoAfterFirst = manager.ClientWeapon.State.Ammo;
                postRecoveryFire = ammoAfterFirst < ammoBeforeDisconnect;
                bootstrap.DisconnectClientForRecovery();
                phase = 4;
                phaseStartedAt = now;
            }
            else if (phase == 5)
            {
                bool secondFire = manager.ClientWeapon.State.Ammo < ammoAfterFirst;
                bool moved = Vector3.Distance(origin, manager.ClientSession.CurrentState.Position) > 0.2f;
                bool hostAlive = bootstrap.ActiveMode != NetworkLaunchMode.Host || bootstrap.Runtime.ServerTransport.IsRunning;
                bool passed = ammoPreserved && postRecoveryFire && secondFire && moved && hostAlive && manager.HasTimeSync;
                Finish(passed, $"entity={entityId} recoveries={bootstrap.Reconnect.CompletedRecoveryCount}"
                    + $" ammoPreserved={ammoPreserved} fire1={postRecoveryFire} fire2={secondFire}"
                    + $" moved={moved} hostAlive={hostAlive} timeSynced={manager.HasTimeSync}");
            }
        }

        private void Finish(bool passed, string details)
        {
            enabled = false;
            input?.ClearAutomationInput();
            Debug.Log($"[AFPS_RECONNECT_RESULT] {(passed ? "PASS" : "FAIL")} {details}");
            SmokeTestExit.Quit(this, passed ? 0 : 2);
        }
    }
}

using System;
using System.Collections.Generic;
using AFPS.Input;
using AFPS.NetCode.Runtime;
using AFPS.NetCode.Sessions;
using AFPS.NetCode.SnapshotInterpolation;
using AFPS.Simulation.Characters;
using UnityEngine;

namespace AFPS.Bootstrap
{
    /// <summary>
    /// Opt-in standalone-process smoke test used to validate a real Host + Client pair.
    /// It is created only when the player starts with -afpsSmokeTest.
    /// </summary>
    public sealed class NetworkSmokeTestDriver : MonoBehaviour
    {
        private const double TestDurationSeconds = 10d;
        private const double ConnectionTimeoutSeconds = 10d;
        private const float MinimumMovementDistance = 0.5f;
        private const float MinimumJumpHeight = 0.2f;
        private const float TestYaw = 25f;
        private readonly List<uint> remoteEntityIds = new List<uint>();
        private UnityNetworkBootstrap networkBootstrap;
        private UnityNetworkMovementController movementController;
        private LocalPlayerInputCollector inputCollector;
        private double configuredAt;
        private double testStartedAt = -1d;
        private bool jumpSent;
        private bool fireSent;
        private bool reloadSent;
        private bool postReloadFireSent;
        private bool authoritativeHitObserved;
        private bool reloadVerified;
        private bool ownerReloadObserved;
        private bool automaticFireVerified;
        private Vector3 initialPosition;
        private float maximumHeight;
        private uint initialShotResultVersion;
        public static bool IsRequested
        {
            get
            {
                string[] arguments = Environment.GetCommandLineArgs();
                for (int i = 0; i < arguments.Length; i++)
                {
                    if (arguments[i].Equals("-afpsSmokeTest", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public void Configure(UnityNetworkBootstrap bootstrap, UnityNetworkMovementController controller, LocalPlayerInputCollector collector)
        {
            networkBootstrap = bootstrap;
            movementController = controller;
            inputCollector = collector;
            configuredAt = Time.realtimeSinceStartupAsDouble;
            Application.runInBackground = true;
        }

        private void Update()
        {
            if (networkBootstrap == null || movementController == null || inputCollector == null)
            {
                Finish(false, "Smoke test dependencies are missing.");
                return;
            }

            double now = Time.realtimeSinceStartupAsDouble;
            double connectionElapsed = now - configuredAt;
            NetworkMovementSessionManager manager = movementController.SessionManager;
            if (manager?.ClientSession == null)
            {
                if (connectionElapsed >= ConnectionTimeoutSeconds)
                {
                    Finish(false, "Client session was not established before timeout.");
                }

                return;
            }

            RemotePlayerReplicaSet replicas = manager.RemotePlayers;
            if (replicas == null || replicas.LocalEntityId == 0 || replicas.Count == 0)
            {
                if (connectionElapsed >= ConnectionTimeoutSeconds)
                {
                    Finish(false, "Local assignment or remote replica was not established before timeout.");
                }

                return;
            }

            PlayerState localState = manager.ClientSession.CurrentState;
            if (testStartedAt < 0d)
            {
                testStartedAt = now;
                initialPosition = localState.Position;
                maximumHeight = localState.Position.y;
                initialShotResultVersion = manager.ShotResultVersion;
            }

            double elapsed = now - testStartedAt;
            maximumHeight = Mathf.Max(maximumHeight, localState.Position.y);
            if (manager.ShotResultVersion != initialShotResultVersion)
            {
                var shotResult = manager.LastReceivedShotResult;
                authoritativeHitObserved |= shotResult.DidHit && shotResult.TargetEntityId != 0;
                reloadVerified |= shotResult.Shot.ShotSequence == 2 && shotResult.RemainingAmmo == 29;
                automaticFireVerified |= shotResult.Shot.ShotSequence >= 4;
            }
            ownerReloadObserved |= manager.ClientWeapon.HasState && manager.ClientWeapon.State.IsReloading;
            bool aimingAtOpponent = elapsed < 1.5d;
            float yaw = TestYaw;
            bool hasAimTarget = false;
            if (aimingAtOpponent)
            {
                // 实体按连接到达顺序分配，Host 的本地客户端也可能后到；瞄准实际快照，不假设出生点左右。
                replicas.CopyEntityIds(remoteEntityIds);
                foreach (uint entityId in remoteEntityIds)
                {
                    if (!replicas.TrySample(entityId, replicas.LatestServerTick, out RemotePlayerRenderState target))
                    {
                        continue;
                    }

                    Vector3 delta = target.Position - localState.Position;
                    if (delta.x * delta.x + delta.z * delta.z <= 0.0001f)
                    {
                        continue;
                    }

                    yaw = Mathf.Repeat(Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg, 360f);
                    hasAimTarget = true;
                    break;
                }
            }

            // 新会话首个实体先发一枪命中，再验证换弹和连续请求；覆盖 Host 与 Dedicated。
            // 只选一个射手，所有客户端都观察相同射击序号与换弹后弹量。
            bool requestFire = replicas.LocalEntityId == 1 && hasAimTarget
                && !fireSent && elapsed >= 0.5d;
            if (requestFire)
            {
                fireSent = true;
            }

            bool requestReload = replicas.LocalEntityId == 1 && !reloadSent && elapsed >= 2d;
            reloadSent |= requestReload;
            if (replicas.LocalEntityId == 1 && !postReloadFireSent && elapsed >= 4.5d)
            {
                requestFire = true;
                postReloadFireSent = true;
            }
            // 一次换弹后单发检查恢复弹量，再持续按住验证多次请求，不依赖画面或实体连接顺序。
            bool holdFire = replicas.LocalEntityId == 1 && elapsed >= 5.5d && elapsed < 5.85d;
            bool requestJump = !jumpSent && elapsed >= 4d;
            if (requestJump)
            {
                jumpSent = true;
            }

            inputCollector.SetAutomationInput(
                aimingAtOpponent ? 0f : 0.2f, aimingAtOpponent ? 0f : 0.65f,
                yaw, aimingAtOpponent ? 0f : -8f, requestJump, requestFire,
                fireHeld: holdFire, reloadPressed: requestReload);
            if (elapsed >= TestDurationSeconds)
            {
                Evaluate(manager, localState);
            }
        }

        private void Evaluate(NetworkMovementSessionManager manager, in PlayerState localState)
        {
            RemotePlayerReplicaSet replicas = manager.RemotePlayers;
            replicas?.CopyEntityIds(remoteEntityIds);
            bool hasAssignment = replicas != null && replicas.LocalEntityId != 0;
            bool hasRemote = replicas != null && replicas.Count > 0 && remoteEntityIds.Count > 0;
            bool sampledRemote = false;
            if (hasRemote)
            {
                sampledRemote = replicas.TrySample(remoteEntityIds[0], replicas.LatestServerTick, out _);
            }

            Vector3 displacement = localState.Position - initialPosition;
            displacement.y = 0f;
            bool moved = displacement.magnitude >= MinimumMovementDistance;
            bool jumped = maximumHeight - initialPosition.y >= MinimumJumpHeight;
            bool turned = Mathf.Abs(Mathf.DeltaAngle(localState.Yaw, TestYaw)) <= 1f;
            bool receivedShotResult = manager.ShotResultVersion != initialShotResultVersion;
            bool authoritativeHit = receivedShotResult && authoritativeHitObserved;
            bool weaponSynced = manager.ClientWeapon.HasState
                && manager.ClientWeapon.Ammo == manager.ClientWeapon.State.Ammo
                && (replicas.LocalEntityId != 1 || ownerReloadObserved);
            bool timeSynced = manager.HasTimeSync && manager.Metrics.TimeSyncResponsesReceived >= 2;
            bool serverQuality = manager.Metrics.TimeSyncAcknowledgementsSent >= 2
                && (manager.ServerSessionCount == 0 || manager.Metrics.ServerRoundTripSamplesReceived >= 2);
            bool adaptiveInterpolation = replicas.HasAdaptiveInterpolation
                && manager.Metrics.RemoteInterpolationDelayTicks >= 3d
                && manager.Metrics.RemoteInterpolationDelayTicks <= 12d;
            var diagnostics = movementController.GetComponent<NetworkDebugPanel>();
            bool diagnosticsReady = diagnostics != null && diagnostics.IsConfigured;
            if (diagnosticsReady)
            {
                bool wasVisible = diagnostics.IsVisible;
                diagnostics.SetVisible(true);
                diagnosticsReady = diagnostics.DisplayedText.Contains("往返延迟")
                    && diagnostics.DisplayedText.Contains("预测校正") && diagnostics.DisplayedText.Contains("快照");
                diagnostics.SetVisible(wasVisible);
            }
            bool shotPresented = movementController.PresentedShotResultCount > 0
                && (replicas.LocalEntityId != 1 || movementController.ConfirmedLocalHitCount > 0);
            bool footstepsPresented = movementController.LocalFootstepCount > 0
                && movementController.RemoteFootstepCount > 0;
            bool passed = hasAssignment && hasRemote && sampledRemote && moved && jumped && turned
                && authoritativeHit && timeSynced && reloadVerified && automaticFireVerified
                && weaponSynced && shotPresented && footstepsPresented && adaptiveInterpolation && serverQuality && diagnosticsReady;
            string details = $"mode={networkBootstrap.ActiveMode} entity={replicas?.LocalEntityId ?? 0}"
                + $" remoteCount={replicas?.Count ?? 0} moved={moved} jumped={jumped} turned={turned}"
                + $" sampledRemote={sampledRemote} authoritativeHit={authoritativeHit} timeSynced={timeSynced}"
                + $" reloadVerified={reloadVerified} autoFire={automaticFireVerified} weaponSynced={weaponSynced}"
                + $" rttMs={manager.Metrics.LastNetworkRoundTripMilliseconds:F3} position={localState.Position}";
            details += $" shotPresented={shotPresented} presentedCount={movementController.PresentedShotResultCount}";
            details += $" footsteps={footstepsPresented} localSteps={movementController.LocalFootstepCount}"
                + $" remoteSteps={movementController.RemoteFootstepCount}";
            details += $" remoteSnapshotsSent={manager.Metrics.SnapshotPacketsSent}"
                + $" remoteSnapshotBytes={manager.Metrics.SnapshotBytesSent}";
            details += $" adaptiveInterpolation={adaptiveInterpolation}"
                + $" interpolationTicks={manager.Metrics.RemoteInterpolationDelayTicks:F3}"
                + $" targetInterpolationTicks={manager.Metrics.TargetRemoteInterpolationDelayTicks:F3}";
            details += $" serverQuality={serverQuality} timeSyncAcks={manager.Metrics.TimeSyncAcknowledgementsSent}"
                + $" serverRttSamples={manager.Metrics.ServerRoundTripSamplesReceived}"
                + $" inputWaits={manager.Metrics.ServerInputWaits} repeatedInputs={manager.Metrics.ServerRepeatedInputTicks}"
                + $" neutralInputs={manager.Metrics.ServerNeutralInputTicks}";
            details += $" diagnostics={diagnosticsReady}";
            Finish(passed, details);
        }

        private void Finish(bool passed, string details)
        {
            enabled = false;
            inputCollector?.ClearAutomationInput();
            Debug.Log($"[AFPS_SMOKE_RESULT] {(passed ? "PASS" : "FAIL")} {details}");
            SmokeTestExit.Quit(this, passed ? 0 : 2);
        }
    }
}

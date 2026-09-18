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
            bool aimingAtOpponent = elapsed < 1.5d;
            float yaw = aimingAtOpponent ? (networkBootstrap.ActiveMode == NetworkLaunchMode.Host ? 90f : 270f) : TestYaw;
            bool requestFire = networkBootstrap.ActiveMode == NetworkLaunchMode.Host && !fireSent && elapsed >= 0.5d;
            if (requestFire)
            {
                fireSent = true;
            }

            bool requestJump = !jumpSent && elapsed >= 4d;
            if (requestJump)
            {
                jumpSent = true;
            }

            inputCollector.SetAutomationInput(aimingAtOpponent ? 0f : 0.2f, aimingAtOpponent ? 0f : 0.65f, yaw, aimingAtOpponent ? 0f : -8f, requestJump, requestFire);
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
            bool authoritativeHit = receivedShotResult && manager.LastReceivedShotResult.DidHit && manager.LastReceivedShotResult.TargetEntityId != 0;
            bool passed = hasAssignment && hasRemote && sampledRemote && moved && jumped && turned && authoritativeHit;
            string details = $"mode={networkBootstrap.ActiveMode} entity={replicas?.LocalEntityId ?? 0} remoteCount={replicas?.Count ?? 0} moved={moved} jumped={jumped} turned={turned} sampledRemote={sampledRemote} authoritativeHit={authoritativeHit} position={localState.Position}";
            Finish(passed, details);
        }

        private void Finish(bool passed, string details)
        {
            enabled = false;
            inputCollector?.ClearAutomationInput();
            Debug.Log($"[AFPS_SMOKE_RESULT] {(passed ? "PASS" : "FAIL")} {details}");
            Application.Quit(passed ? 0 : 2);
        }
    }
}

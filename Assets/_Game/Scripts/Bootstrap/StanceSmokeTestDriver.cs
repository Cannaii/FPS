using System;
using System.Collections.Generic;
using AFPS.Input;
using AFPS.Math;
using AFPS.NetCode.Match;
using AFPS.Presentation.Characters;
using AFPS.Simulation.Characters;
using AFPS.Simulation.World;
using UnityEngine;

namespace AFPS.Bootstrap
{
    /// <summary>仅-afpsStanceSmoke启用：真实UTP验证滑铲穿低通道、松键顶棚约束和远端姿态。</summary>
    [DefaultExecutionOrder(100)]
    public sealed class StanceSmokeTestDriver : MonoBehaviour
    {
        public static bool IsRequested => Array.Exists(Environment.GetCommandLineArgs(),
            value => value.Equals("-afpsStanceSmoke", StringComparison.OrdinalIgnoreCase));

        public static SimulationWorldContext CreateFixture(in PlayerMovementConfig config, Fix deltaTime, int layerMask)
        {
            var roof = new FixedWorldBox(1, 8, FixedSurface.Solid, new FixedAabb(
                new FixVec3(Fix.FromInt(2), Fix.FromRatio(3, 2), Fix.FromInt(-5)),
                new FixVec3(Fix.FromInt(8), Fix.FromInt(3), Fix.FromInt(5))));
            var ground = new FixedWorldPlane(2, 8, FixedSurface.Solid | FixedSurface.Ground, FixedAxis.Y, Fix.Zero);
            var spatial = new SimulationSessionSpatialConfig(-FixVec3.Right * Fix.FromInt(4),
                Fix.FromRatio(1, 10), Fix.FromInt(150));
            return new SimulationWorldContext(new FixedWorld(new[] { roof }, new[] { ground }),
                config, deltaTime, layerMask, spatial);
        }

        private readonly List<uint> entities = new List<uint>();
        private UnityNetworkGameplayController movement;
        private LocalPlayerInputCollector input;
        private double configuredAt;
        private double startedAt = -1d;
        private double crouchedAt;
        private double completedAt = -1d;
        private int phase;
        private bool predictedSlide;
        private bool authoritySlide;
        private bool blocked;
        private bool stood;
        private bool remoteCrouched;
        private bool remoteView;
        private FixVec3 lastPredicted;
        private FixVec3 lastAuthority;

        public void Configure(UnityNetworkGameplayController controller, LocalPlayerInputCollector collector)
        {
            movement = controller;
            input = collector;
            configuredAt = Time.realtimeSinceStartupAsDouble;
            Application.runInBackground = true;
        }

        private void Update()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            var manager = movement.SessionCoordinator;
            if (manager?.ClientSession != null && !SmokeTestLobby.EnsurePlaying(movement))
            {
                return;
            }
            if (manager?.ClientSession == null || manager.RemotePlayers == null || manager.RemotePlayers.Count == 0
                || manager.ClientDeathmatchState == null || manager.ClientDeathmatchState.Phase != DeathmatchPhase.Playing)
            {
                if (completedAt >= 0d && now - completedAt >= 1d)
                {
                    Finish(true);
                }
                else if (now - configuredAt > 25d)
                {
                    Finish(false);
                }
                return;
            }
            if (startedAt < 0d)
            {
                startedAt = now;
            }
            var predicted = manager.ClientSession.CurrentState;
            var authority = manager.ClientSession.LastAuthoritativeState;
            lastPredicted = predicted.PositionFixed;
            lastAuthority = authority.PositionFixed;
            predictedSlide |= predicted.SlideRemainingFixed > Fix.Zero && predicted.VelocityFixed.X > Fix.FromInt(8);
            authoritySlide |= authority.SlideRemainingFixed > Fix.Zero && authority.VelocityFixed.X > Fix.FromInt(8);
            if (phase == 0 && authority.IsGrounded && authority.VelocityFixed.X >= Fix.FromInt(8))
            {
                phase = 1;
                crouchedAt = now;
            }
            else if (phase == 1 && now - crouchedAt > 0.6d && authoritySlide && predictedSlide)
            {
                phase = 2;
            }
            // 已经松键，仍在顶棚内保持低姿态；不是把按住Ctrl误算成顶棚生效。
            blocked |= phase == 2 && now - crouchedAt > 0.7d && authority.IsCrouched && predicted.IsCrouched
                && authority.PositionFixed.X > Fix.FromInt(2) && authority.PositionFixed.X < Fix.FromInt(8);
            stood |= phase == 2 && blocked && !authority.IsCrouched && !predicted.IsCrouched
                && authority.PositionFixed.X > Fix.FromRatio(17, 2);
            manager.RemotePlayers.CopyEntityIds(entities);
            foreach (uint entity in entities)
            {
                if (manager.RemotePlayers.TrySample(entity, manager.RemotePlayers.LatestServerTick + 2d, out var render))
                {
                    remoteCrouched |= render.IsCrouched;
                }
            }
            foreach (var view in FindObjectsOfType<RemotePlayerView>())
            {
                remoteView |= view.transform.localScale.y < 0.8f;
            }
            input.SetAutomationInput(0f, 1f, 90f, 0f, false,
                sprintHeld: phase == 0, crouchHeld: phase == 1);
            bool passed = predictedSlide && authoritySlide && blocked && stood && remoteCrouched && remoteView && manager.HasTimeSync;
            if (passed && completedAt < 0d)
            {
                // Host不能在自己刚站起时立即销毁服务器，需给另一端留出权威回传/退出余量。
                completedAt = now;
            }
            if (completedAt >= 0d && now - completedAt >= 1d)
            {
                Finish(true);
            }
            else if (now - startedAt > 12d)
            {
                Finish(false);
            }
        }

        private void Finish(bool passed)
        {
            enabled = false;
            input.ClearAutomationInput();
            Debug.Log($"[AFPS_STANCE_RESULT] {(passed ? "PASS" : "FAIL")} phase={phase}"
                + $" predictedSlide={predictedSlide} authoritySlide={authoritySlide} blocked={blocked} stood={stood}"
                + $" remoteCrouched={remoteCrouched} remoteView={remoteView} predicted={lastPredicted} authority={lastAuthority}");
            SmokeTestExit.Quit(this, passed ? 0 : 2);
        }
    }
}

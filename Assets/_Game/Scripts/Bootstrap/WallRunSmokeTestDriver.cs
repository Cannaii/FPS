using System;
using AFPS.Input;
using AFPS.Math;
using AFPS.NetCode.Match;
using AFPS.Simulation.Characters;
using AFPS.Simulation.World;
using UnityEngine;

namespace AFPS.Bootstrap
{
    /// <summary>仅-afpsWallRunSmoke启用：真实UTP验证墙跑/预算耗尽/松键不刷新/落地重启。</summary>
    [DefaultExecutionOrder(100)]
    public sealed class WallRunSmokeTestDriver : MonoBehaviour
    {
        public static bool IsRequested => Array.Exists(Environment.GetCommandLineArgs(),
            value => value.Equals("-afpsWallRunSmoke", StringComparison.OrdinalIgnoreCase));

        public static SimulationWorldContext CreateFixture(in PlayerSimulationConfig config, Fix deltaTime, int layerMask)
        {
            var wall = new FixedWorldBox(1, 8, FixedSurface.Solid | FixedSurface.Wall, new FixedAabb(
                new FixVec3(Fix.FromInt(2), Fix.Zero, Fix.FromInt(-100)),
                new FixVec3(Fix.FromInt(3), Fix.FromInt(40), Fix.FromInt(100))));
            var ground = new FixedWorldPlane(2, 8, FixedSurface.Solid | FixedSurface.Ground, FixedAxis.Y, Fix.Zero);
            // 角色间不参与移动碰撞；相同X确保所有端的初始胶囊都恰好贴墙，而非部分嵌入墙内。
            var spatial = new SimulationSessionSpatialConfig(FixVec3.Right * Fix.FromRatio(3, 2), Fix.Zero, Fix.FromInt(150));
            return new SimulationWorldContext(new FixedWorld(new[] { wall }, new[] { ground }),
                config, deltaTime, layerMask, spatial);
        }

        private UnityNetworkMovementController movement;
        private LocalPlayerInputCollector input;
        private double configuredAt;
        private double startedAt = -1d;
        private double phaseAt;
        private double completedAt = -1d;
        private int phase;
        private bool predictedRun;
        private bool authorityRun;
        private bool spent;
        private bool expired;
        private bool noRefresh;
        private bool landingReset;
        private bool restarted;

        public void Configure(UnityNetworkMovementController controller, LocalPlayerInputCollector collector)
        {
            movement = controller;
            input = collector;
            configuredAt = Time.realtimeSinceStartupAsDouble;
            Application.runInBackground = true;
        }

        private void Update()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            var manager = movement.SessionManager;
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
                phaseAt = now;
            }
            var predicted = manager.ClientSession.CurrentState;
            var authority = manager.ClientSession.LastAuthoritativeState;
            Fix duration = manager.WorldContext.Configuration.WallRun.Duration;
            bool jump = false;
            predictedRun |= predicted.IsWallRunning && predicted.VelocityFixed.Z > Fix.FromInt(8);
            authorityRun |= authority.IsWallRunning && authority.VelocityFixed.Z > Fix.FromInt(8);
            spent |= authority.IsWallRunning && authority.StaminaSpentFixed > Fix.FromRatio(1, 10);
            if (phase == 0 && now - phaseAt > 0.1d)
            {
                jump = true;
                phase = 1;
            }
            else if (phase == 1 && predictedRun && authorityRun)
            {
                phase = 2;
            }
            else if (phase == 2 && predicted.WallRunUsedFixed == duration && authority.WallRunUsedFixed == duration
                && !predicted.IsWallRunning && !authority.IsWallRunning)
            {
                expired = true;
                phase = 3;
                phaseAt = now;
            }
            else if (phase == 3 && now - phaseAt > 0.12d)
            {
                phase = 4;
                phaseAt = now;
            }
            else if (phase == 4 && now - phaseAt > 0.12d && !predicted.IsGrounded && !authority.IsGrounded
                && !predicted.IsWallRunning && !authority.IsWallRunning
                && predicted.WallRunUsedFixed == duration && authority.WallRunUsedFixed == duration)
            {
                noRefresh = true;
                phase = 5;
            }
            else if (phase == 5 && predicted.IsGrounded && authority.IsGrounded
                && predicted.WallRunUsedFixed == Fix.Zero && authority.WallRunUsedFixed == Fix.Zero)
            {
                landingReset = true;
                jump = true;
                phase = 6;
            }
            else if (phase == 6 && predicted.IsWallRunning && authority.IsWallRunning
                && predicted.WallRunUsedFixed > Fix.Zero && authority.WallRunUsedFixed > Fix.Zero
                && predicted.WallRunUsedFixed < duration && authority.WallRunUsedFixed < duration)
            {
                restarted = true;
                phase = 7;
            }
            input.SetAutomationInput(0f, 1f, 0f, 0f, jump, sprintHeld: phase != 3);
            bool passed = predictedRun && authorityRun && spent && expired && noRefresh && landingReset && restarted && manager.HasTimeSync;
            if (passed && completedAt < 0d)
            {
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
            Debug.Log($"[AFPS_WALLRUN_RESULT] {(passed ? "PASS" : "FAIL")} phase={phase}"
                + $" predicted={predictedRun} authority={authorityRun} spent={spent} expired={expired}"
                + $" noRefresh={noRefresh} landingReset={landingReset} restarted={restarted}");
            SmokeTestExit.Quit(this, passed ? 0 : 2);
        }
    }
}

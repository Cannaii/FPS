using System;
using AFPS.Input;
using AFPS.Math;
using AFPS.NetCode.Match;
using AFPS.Simulation.Characters;
using AFPS.Simulation.World;
using UnityEngine;

namespace AFPS.Bootstrap
{
    /// <summary>仅-afpsClimbSmoke启用：真实UTP验证上下梯、悬停、两端退出与跳离。</summary>
    [DefaultExecutionOrder(100)]
    public sealed class ClimbSmokeTestDriver : MonoBehaviour
    {
        public static bool IsRequested => Array.Exists(Environment.GetCommandLineArgs(),
            value => value.Equals("-afpsClimbSmoke", StringComparison.OrdinalIgnoreCase));

        public static SimulationWorldContext CreateFixture(in PlayerSimulationConfig config, Fix deltaTime, int layerMask)
        {
            var ladder = new FixedWorldBox(1, 8, FixedSurface.Ladder, new FixedAabb(
                new FixVec3(Fix.FromInt(2), Fix.Zero, Fix.FromInt(-1)),
                new FixVec3(Fix.FromRatio(21, 10), Fix.FromInt(4), Fix.One)));
            var floor = new FixedWorldPlane(2, 8, FixedSurface.Ground | FixedSurface.Solid, FixedAxis.Y, Fix.Zero);
            var spatial = new SimulationSessionSpatialConfig(FixVec3.Right * Fix.FromRatio(3, 2), Fix.Zero, Fix.FromInt(150));
            return new SimulationWorldContext(new FixedWorld(new[] { ladder }, new[] { floor }),
                config, deltaTime, layerMask, spatial);
        }

        private UnityNetworkMovementController movement;
        private LocalPlayerInputCollector input;
        private double configuredAt;
        private double startedAt = -1d;
        private double phaseAt;
        private double completedAt = -1d;
        private int phase;
        private bool predictedClimb;
        private bool authorityClimb;
        private bool held;
        private bool descended;
        private bool bottomExit;
        private bool topExit;
        private bool predictedJump;
        private bool authorityJump;

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
            }
            var predicted = manager.ClientSession.CurrentState;
            var authority = manager.ClientSession.LastAuthoritativeState;
            predictedClimb |= predicted.IsClimbing && predicted.VelocityFixed.Y > Fix.FromInt(2);
            authorityClimb |= authority.IsClimbing && authority.VelocityFixed.Y > Fix.FromInt(2);
            bool jump = false;
            if (phase == 0 && predictedClimb && authorityClimb && authority.PositionFixed.Y >= Fix.FromRatio(11, 10))
            {
                phase = 1;
                phaseAt = now;
            }
            else if (phase == 1 && now - phaseAt > 0.25d && predicted.IsClimbing && authority.IsClimbing
                && predicted.VelocityFixed.Y == Fix.Zero && authority.VelocityFixed.Y == Fix.Zero)
            {
                held = true;
                phase = 2;
            }
            else if (phase == 2)
            {
                descended |= authority.IsClimbing && authority.VelocityFixed.Y < Fix.FromInt(-2);
                if (descended && !predicted.IsClimbing && !authority.IsClimbing
                    && predicted.IsGrounded && authority.IsGrounded)
                {
                    bottomExit = true;
                    phase = 3;
                }
            }
            else if (phase == 3 && predicted.ClimbCooldownRemainingFixed == Fix.Zero
                && authority.ClimbCooldownRemainingFixed == Fix.Zero)
            {
                phase = 4;
            }
            else if (phase == 4 && !predicted.IsClimbing && !authority.IsClimbing
                && predicted.ClimbCooldownRemainingFixed > Fix.Zero && authority.ClimbCooldownRemainingFixed > Fix.Zero
                && predicted.PositionFixed.Y >= Fix.FromRatio(19, 5) && authority.PositionFixed.Y >= Fix.FromRatio(19, 5))
            {
                topExit = true;
                phase = 5;
            }
            else if (phase == 5 && predicted.PositionFixed.X <= Fix.FromRatio(3, 2)
                && authority.PositionFixed.X <= Fix.FromRatio(3, 2))
            {
                phase = 6;
            }
            else if (phase == 6 && predicted.IsClimbing && authority.IsClimbing)
            {
                jump = true;
                phase = 7;
            }
            if (phase == 7)
            {
                predictedJump |= !predicted.IsClimbing && predicted.ClimbCooldownRemainingFixed > Fix.Zero
                    && predicted.VelocityFixed.X < -Fix.One && predicted.VelocityFixed.Y > Fix.FromInt(6);
                authorityJump |= !authority.IsClimbing && authority.ClimbCooldownRemainingFixed > Fix.Zero
                    && authority.VelocityFixed.X < -Fix.One && authority.VelocityFixed.Y > Fix.FromInt(6);
            }
            float vertical = phase == 1 || phase == 3 ? 0f : phase == 2 || phase == 5 ? -1f : 1f;
            if (phase == 6 && predicted.IsClimbing)
            {
                // 重挂成功后停住，等待真正权威确认；不能在等待期间又爬出梯顶而丢失跳离窗口。
                vertical = 0f;
            }
            input.SetAutomationInput(0f, vertical, 90f, 0f, jump);
            bool passed = predictedClimb && authorityClimb && held && descended && bottomExit && topExit
                && predictedJump && authorityJump && manager.HasTimeSync;
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
            Debug.Log($"[AFPS_CLIMB_RESULT] {(passed ? "PASS" : "FAIL")} phase={phase} predicted={predictedClimb}"
                + $" authority={authorityClimb} held={held} down={descended} bottom={bottomExit} top={topExit}"
                + $" predictedJump={predictedJump} authorityJump={authorityJump}");
            SmokeTestExit.Quit(this, passed ? 0 : 2);
        }
    }
}

using System;
using AFPS.Input;
using AFPS.Math;
using AFPS.NetCode.Match;
using AFPS.Simulation.Characters;
using AFPS.Simulation.World;
using UnityEngine;

namespace AFPS.Bootstrap
{
    /// <summary>仅-afpsWallSmoke启用；受控定点墙世界验证真实UTP回传，正常地图不被替换。</summary>
    [DefaultExecutionOrder(100)]
    public sealed class WallSmokeTestDriver : MonoBehaviour
    {
        public static bool IsRequested => Array.Exists(Environment.GetCommandLineArgs(),
            value => value.Equals("-afpsWallSmoke", StringComparison.OrdinalIgnoreCase));

        public static SimulationWorldContext CreateFixture(in PlayerSimulationConfig config, Fix deltaTime, int layerMask)
        {
            var wall = new FixedWorldBox(1, 8, FixedSurface.Solid, new FixedAabb(
                new FixVec3(Fix.FromInt(2), Fix.Zero, Fix.FromInt(-10)),
                new FixVec3(Fix.FromInt(3), Fix.FromInt(10), Fix.FromInt(10))));
            var floor = new FixedWorldPlane(2, 8, FixedSurface.Solid | FixedSurface.Ground, FixedAxis.Y, Fix.Zero);
            var spatial = new SimulationSessionSpatialConfig(FixVec3.Zero, Fix.FromRatio(1, 10), Fix.FromInt(150));
            return new SimulationWorldContext(new FixedWorld(new[] { wall }, new[] { floor }),
                config, deltaTime, layerMask, spatial);
        }

        private UnityNetworkMovementController movement;
        private LocalPlayerInputCollector input;
        private double configuredAt;
        private double startedAt = -1d;
        private double phaseAt;
        private int phase;
        private bool contact;
        private bool predictedJump;
        private bool authorityJump;
        private bool predictedBounce;
        private bool authorityBounce;
        private uint entity;

        public void Configure(UnityNetworkMovementController controller, LocalPlayerInputCollector collector)
        {
            movement = controller;
            input = collector;
            configuredAt = Time.realtimeSinceStartupAsDouble;
            Application.runInBackground = true;
        }

        private bool Passed => contact && predictedJump && authorityJump && predictedBounce && authorityBounce;

        private void Update()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            var manager = movement.SessionManager;
            if (manager?.ClientSession != null && !SmokeTestLobby.EnsurePlaying(movement))
            {
                return;
            }
            if (manager?.ClientSession == null)
            {
                if (Passed || now - configuredAt > 25d)
                {
                    Finish(Passed);
                }
                return;
            }
            if (manager.RemotePlayers == null || manager.RemotePlayers.Count == 0
                || manager.ClientDeathmatchState == null || manager.ClientDeathmatchState.Phase != DeathmatchPhase.Playing)
            {
                if (now - configuredAt > 25d)
                {
                    Finish(false);
                }
                return;
            }
            entity = manager.RemotePlayers.LocalEntityId;
            if (startedAt < 0d)
            {
                startedAt = now;
                phaseAt = now;
            }
            var predicted = manager.ClientSession.CurrentState;
            var authority = manager.ClientSession.LastAuthoritativeState;
            bool jump = false;
            bool dash = false;
            bool bounce = false;
            contact |= authority.WallNormalFixed == -FixVec3.Right;
            predictedJump |= phase >= 2 && phase < 4 && predicted.WallCooldownRemainingFixed > Fix.Zero
                && predicted.VelocityFixed.X < Fix.Zero && predicted.VelocityFixed.Y > Fix.Zero;
            authorityJump |= phase >= 2 && phase < 4 && authority.WallCooldownRemainingFixed > Fix.Zero
                && authority.VelocityFixed.X < Fix.Zero && authority.VelocityFixed.Y > Fix.Zero;
            predictedBounce |= phase >= 6 && predicted.WallCooldownRemainingFixed > Fix.Zero
                && predicted.VelocityFixed.X < Fix.FromInt(-15) && predicted.DashRemainingFixed == Fix.Zero;
            authorityBounce |= phase >= 6 && authority.WallCooldownRemainingFixed > Fix.Zero
                && authority.VelocityFixed.X < Fix.FromInt(-15) && authority.DashRemainingFixed == Fix.Zero;

            if (phase == 0 && now - phaseAt > 0.1d)
            {
                jump = true;
                phase = 1;
            }
            else if (phase == 1 && !predicted.IsGrounded && predicted.WallNormalFixed != FixVec3.Zero
                && !authority.IsGrounded && authority.WallNormalFixed != FixVec3.Zero)
            {
                jump = true;
                phase = 2;
            }
            else if (phase == 2 && authorityJump)
            {
                phase = 3;
            }
            else if (phase == 3 && predicted.IsGrounded && authority.IsGrounded)
            {
                jump = true;
                phase = 4;
                phaseAt = now;
            }
            else if (phase == 4 && now - phaseAt >= 0.1d)
            {
                dash = true;
                phase = 5;
            }
            else if (phase == 5 && !predicted.IsGrounded && predicted.WallNormalFixed != FixVec3.Zero
                && !authority.IsGrounded && authority.WallNormalFixed != FixVec3.Zero
                && predicted.DashRemainingFixed > Fix.Zero && authority.DashRemainingFixed > Fix.Zero
                && authority.WallIncomingVelocityFixed.X > Fix.FromInt(15))
            {
                bounce = true;
                phase = 6;
                phaseAt = now;
            }
            input.SetAutomationInput(1f, 0f, 0f, 0f, jump, dashPressed: dash, wallBouncePressed: bounce);
            if ((Passed && now - phaseAt > 0.3d) || now - startedAt > 12d || now - configuredAt > 25d)
            {
                Finish(Passed && manager.HasTimeSync);
            }
        }

        private void Finish(bool passed)
        {
            enabled = false;
            input.ClearAutomationInput();
            Debug.Log($"[AFPS_WALL_RESULT] {(passed ? "PASS" : "FAIL")} entity={entity} phase={phase}"
                + $" contact={contact} predictedJump={predictedJump} authorityJump={authorityJump}"
                + $" predictedBounce={predictedBounce} authorityBounce={authorityBounce}");
            SmokeTestExit.Quit(this, passed ? 0 : 2);
        }
    }
}

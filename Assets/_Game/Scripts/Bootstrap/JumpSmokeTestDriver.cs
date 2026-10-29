using System;
using AFPS.Input;
using AFPS.Math;
using AFPS.NetCode.Match;
using UnityEngine;

namespace AFPS.Bootstrap
{
    /// <summary>仅-afpsJumpSmoke启用；通过真实权威回传检查地面跳、双跳及两次落地资源重置。</summary>
    [DefaultExecutionOrder(100)]
    public sealed class JumpSmokeTestDriver : MonoBehaviour
    {
        public static bool IsRequested => Array.Exists(Environment.GetCommandLineArgs(),
            value => value.Equals("-afpsJumpSmoke", StringComparison.OrdinalIgnoreCase));
        private UnityNetworkGameplayController movement;
        private LocalPlayerInputCollector input;
        private double configuredAt;
        private double startedAt = -1d;
        private int nextEvent;
        private bool predictedAirJump;
        private bool groundJump;
        private bool landingReset;
        private bool synchronized;
        private bool airJumpSeenThisFlight;
        private int authoritativeAirJumpFlights;
        private uint entity;

        public void Configure(UnityNetworkGameplayController controller, LocalPlayerInputCollector collector)
        {
            movement = controller;
            input = collector;
            configuredAt = Time.realtimeSinceStartupAsDouble;
            Application.runInBackground = true;
        }

        private bool Passed => predictedAirJump && groundJump && landingReset
            && authoritativeAirJumpFlights >= 2 && synchronized;

        private void Update()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            var manager = movement.SessionCoordinator;
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
            }
            double elapsed = now - startedAt;
            var predicted = manager.ClientSession.CurrentState;
            var authority = manager.ClientSession.LastAuthoritativeState;
            predictedAirJump |= predicted.AirJumpsUsed == 1 && predicted.VelocityFixed.Y > Fix.Zero;
            groundJump |= authority.GroundJumpConsumed && authority.AirJumpsUsed == 0
                && !authority.IsGrounded && authority.VelocityFixed.Y > Fix.Zero;
            if (!airJumpSeenThisFlight && authority.AirJumpsUsed == 1 && authority.VelocityFixed.Y > Fix.Zero)
            {
                authoritativeAirJumpFlights++;
                airJumpSeenThisFlight = true;
            }
            if (authority.IsGrounded && authority.AirJumpsUsed == 0 && !authority.GroundJumpConsumed)
            {
                landingReset |= airJumpSeenThisFlight && authority.CoyoteRemainingFixed > Fix.Zero;
                airJumpSeenThisFlight = false;
            }
            synchronized |= manager.HasTimeSync;

            // 两轮地面跳→双跳，中间插入一次应被拒绝的第三跳；落地后再次验证资源可用。
            bool jump = (nextEvent == 0 && elapsed >= 0.2d)
                || (nextEvent == 1 && elapsed >= 0.4d) || (nextEvent == 2 && elapsed >= 0.6d)
                || (nextEvent == 3 && elapsed >= 2.5d) || (nextEvent == 4 && elapsed >= 2.8d);
            if (jump)
            {
                nextEvent++;
            }
            input.SetAutomationInput(0f, 0f, 0f, 0f, jump);
            if (elapsed >= 5d || now - configuredAt > 25d)
            {
                Finish(Passed);
            }
        }

        private void Finish(bool success)
        {
            enabled = false;
            input.ClearAutomationInput();
            Debug.Log($"[AFPS_JUMP_RESULT] {(success ? "PASS" : "FAIL")} entity={entity}"
                + $" predicted={predictedAirJump} ground={groundJump} airFlights={authoritativeAirJumpFlights}"
                + $" landingReset={landingReset} synced={synchronized}");
            SmokeTestExit.Quit(this, success ? 0 : 2);
        }
    }
}

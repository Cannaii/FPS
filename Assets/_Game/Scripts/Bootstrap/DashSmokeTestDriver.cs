using System;
using AFPS.Input;
using AFPS.Math;
using AFPS.NetCode.Match;
using UnityEngine;

namespace AFPS.Bootstrap
{
    /// <summary>仅-afpsDashSmoke启用；用真实UTP客户端验收空中Dash、充能消耗与恢复的权威回传。</summary>
    [DefaultExecutionOrder(100)]
    public sealed class DashSmokeTestDriver : MonoBehaviour
    {
        public static bool IsRequested => Array.Exists(Environment.GetCommandLineArgs(),
            value => value.Equals("-afpsDashSmoke", StringComparison.OrdinalIgnoreCase));
        private UnityNetworkMovementController movement;
        private LocalPlayerInputCollector input;
        private double configuredAt;
        private double startedAt = -1d;
        private int nextEvent;
        private bool predictedDash;
        private bool authoritativeDash;
        private bool airborneDash;
        private bool spentBoth;
        private bool recovered;
        private bool synchronized;
        private uint entity;

        public void Configure(UnityNetworkMovementController controller, LocalPlayerInputCollector collector)
        {
            movement = controller;
            input = collector;
            configuredAt = Time.realtimeSinceStartupAsDouble;
            Application.runInBackground = true;
        }

        private bool Passed => predictedDash && authoritativeDash && airborneDash
            && spentBoth && recovered && synchronized;

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
            }
            double elapsed = now - startedAt;
            var predicted = manager.ClientSession.CurrentState;
            var authority = manager.ClientSession.LastAuthoritativeState;
            predictedDash |= predicted.DashRemainingFixed > Fix.Zero;
            authoritativeDash |= authority.DashRemainingFixed > Fix.Zero
                && authority.StaminaSpentFixed > Fix.Zero && authority.VelocityFixed.Magnitude > Fix.FromInt(15);
            airborneDash |= authority.DashRemainingFixed > Fix.Zero && !authority.IsGrounded;
            spentBoth |= authority.DashSpentCharges == 2;
            recovered |= elapsed > 4d && spentBoth && authority.DashSpentCharges == 0
                && authority.DashRechargeRemainingFixed == Fix.Zero && authority.StaminaSpentFixed == Fix.Zero;
            synchronized |= manager.HasTimeSync;

            // 每个边沿只注入一次；第二次反向冲刺避免走出地图，第三次应因充能不足被拒绝。
            bool jump = nextEvent == 0 && elapsed >= 0.05d;
            bool dash = (nextEvent == 1 && elapsed >= 0.2d)
                || (nextEvent == 2 && elapsed >= 0.7d) || (nextEvent == 3 && elapsed >= 1.2d);
            if (jump || dash)
            {
                nextEvent++;
            }
            input.SetAutomationInput(0f, 0f, nextEvent >= 3 ? 180f : 0f, 0f, jump, dashPressed: dash);
            if (elapsed >= 6.5d || now - configuredAt > 25d)
            {
                Finish(Passed);
            }
        }

        private void Finish(bool success)
        {
            enabled = false;
            input.ClearAutomationInput();
            Debug.Log($"[AFPS_DASH_RESULT] {(success ? "PASS" : "FAIL")} entity={entity}"
                + $" predicted={predictedDash} authority={authoritativeDash} airborne={airborneDash}"
                + $" spentBoth={spentBoth} recovered={recovered} synced={synchronized}");
            SmokeTestExit.Quit(this, success ? 0 : 2);
        }
    }
}

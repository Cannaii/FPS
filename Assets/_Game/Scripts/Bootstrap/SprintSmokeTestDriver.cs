using System;
using AFPS.Input;
using AFPS.NetCode.Match;
using UnityEngine;

namespace AFPS.Bootstrap
{
    /// <summary>仅 -afpsSprintSmoke 启用；真实客户端检查快跑、耗尽和恢复是否到达服务器并回传。</summary>
    [DefaultExecutionOrder(100)]
    public sealed class SprintSmokeTestDriver : MonoBehaviour
    {
        public static bool IsRequested => Array.Exists(Environment.GetCommandLineArgs(),
            value => value.Equals("-afpsSprintSmoke", StringComparison.OrdinalIgnoreCase));
        private UnityNetworkMovementController movement;
        private LocalPlayerInputCollector input;
        private double configuredAt;
        private double startedAt = -1d;
        private bool predictedSprint;
        private bool authoritativeSprint;
        private bool exhausted;
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

        private bool Passed => predictedSprint && authoritativeSprint && exhausted && recovered && synchronized;

        private void Update()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            var manager = movement.SessionManager;
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
            predictedSprint |= predicted.IsSprinting && predicted.StaminaSpentFixed.Raw > 0;
            authoritativeSprint |= authority.IsSprinting && authority.StaminaSpentFixed.Raw > 0
                && authority.VelocityFixed.Magnitude > AFPS.Math.Fix.FromInt(8);
            exhausted |= authority.SprintExhausted;
            recovered |= elapsed > 6d && exhausted && !authority.SprintExhausted
                && authority.StaminaSpentFixed.Raw == 0;
            synchronized |= manager.HasTimeSync;
            float yaw = Mathf.FloorToInt((float)elapsed / 1.5f) % 4 * 90f;
            input.SetAutomationInput(0f, 1f, yaw, 0f, false,
                sprintHeld: elapsed >= 0.2d && elapsed < 5d);
            if (elapsed >= 11.5d || now - configuredAt > 25d)
            {
                Finish(Passed);
            }
        }

        private void Finish(bool success)
        {
            enabled = false;
            input.ClearAutomationInput();
            Debug.Log($"[AFPS_SPRINT_RESULT] {(success ? "PASS" : "FAIL")} entity={entity}"
                + $" predicted={predictedSprint} authority={authoritativeSprint} exhausted={exhausted}"
                + $" recovered={recovered} synced={synchronized}");
            Application.Quit(success ? 0 : 2);
        }
    }
}

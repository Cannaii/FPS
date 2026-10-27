using System;
using AFPS.Input;
using AFPS.Math;
using AFPS.NetCode.Match;
using AFPS.Simulation.Characters;
using AFPS.Simulation.World;
using UnityEngine;

namespace AFPS.Bootstrap
{
    /// <summary>仅-afpsGrappleSmoke启用；读取真实Owner回传验证卷绳/摆荡/松键/重挂/Dash断绳。</summary>
    [DefaultExecutionOrder(100)]
    public sealed class GrappleSmokeTestDriver : MonoBehaviour
    {
        public static bool IsRequested => Array.Exists(Environment.GetCommandLineArgs(),
            value => value.Equals("-afpsGrappleSmoke", StringComparison.OrdinalIgnoreCase));

        public static SimulationWorldContext CreateFixture(in PlayerSimulationConfig config, Fix deltaTime, int layerMask)
        {
            var anchor = new FixedWorldPlane(1, 8, FixedSurface.Solid | FixedSurface.Grapple,
                FixedAxis.Z, Fix.FromInt(20), positiveNormal: false);
            var floor = new FixedWorldPlane(2, 8, FixedSurface.Solid | FixedSurface.Ground, FixedAxis.Y, Fix.Zero);
            var spatial = new SimulationSessionSpatialConfig(FixVec3.Zero, Fix.One, Fix.FromInt(150));
            return new SimulationWorldContext(new FixedWorld(null, new[] { anchor, floor }), config, deltaTime, layerMask, spatial);
        }

        private UnityNetworkMovementController movement;
        private LocalPlayerInputCollector input;
        private double configuredAt;
        private double startedAt = -1d;
        private double phaseAt;
        private double completedAt = -1d;
        private Fix initialLength;
        private Fix initialX;
        private int phase = -1;
        private bool jumpIssued;
        private bool airborne;
        private bool predicted;
        private bool authority;
        private bool reeled;
        private bool swung;
        private bool rope;
        private bool released;
        private bool reacquired;
        private bool canceled;
        private bool latched;
        private bool captured;

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
            var state = manager.ClientSession.CurrentState;
            var confirmed = manager.ClientSession.LastAuthoritativeState;
            if (startedAt < 0d)
            {
                startedAt = now;
                initialX = state.PositionFixed.X;
            }
            predicted |= state.IsGrappling;
            authority |= confirmed.IsGrappling;
            rope |= movement.HasPresentedGrappleRope;
            bool dash = false;
            bool jump = false;
            if (phase == -1)
            {
                jump = !jumpIssued;
                jumpIssued = true;
                if (!state.IsGrounded && !confirmed.IsGrounded
                    && state.PositionFixed.Y > Fix.FromRatio(1, 10) && confirmed.PositionFixed.Y > Fix.FromRatio(1, 10))
                {
                    phase = 0;
                }
            }
            else if (phase == 0 && state.IsGrappling && confirmed.IsGrappling)
            {
                if (!captured && movement.HasPresentedGrappleRope
                    && SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null
                    && Array.Exists(Environment.GetCommandLineArgs(),
                        value => value.Equals("-afpsGrappleCapture", StringComparison.OrdinalIgnoreCase)))
                {
                    string directory = System.IO.Path.Combine(Environment.CurrentDirectory,
                        "Library/AFPSVerification/Results/GrappleGraphics");
                    System.IO.Directory.CreateDirectory(directory);
                    PracticeSmokeTestDriver.CaptureCameras(System.IO.Path.Combine(directory,
                        "grapple-" + manager.RemotePlayers.LocalEntityId + ".png"));
                    captured = true;
                }
                initialLength = confirmed.GrappleLengthFixed;
                phase = 1;
            }
            else if (phase == 1)
            {
                reeled |= confirmed.IsGrappling && confirmed.GrappleLengthFixed < initialLength - Fix.FromRatio(1, 5);
                swung |= state.PositionFixed.X > initialX + Fix.FromRatio(1, 4)
                    && confirmed.PositionFixed.X > initialX + Fix.FromRatio(1, 4);
                airborne |= swung && state.IsGrappling && confirmed.IsGrappling && !state.IsGrounded && !confirmed.IsGrounded;
                if (reeled && swung && airborne && rope)
                {
                    phase = 2;
                }
            }
            else if (phase == 2 && !state.IsGrappling && !confirmed.IsGrappling
                && !state.GrappleConsumed && !confirmed.GrappleConsumed && !movement.HasPresentedGrappleRope)
            {
                released = true;
                phase = 3;
            }
            else if (phase == 3 && state.IsGrappling && confirmed.IsGrappling)
            {
                reacquired = true;
                dash = true;
                phase = 4;
                phaseAt = now;
            }
            else if (phase == 4 && now - phaseAt > 0.3d
                && state.DashRemainingFixed == Fix.Zero && confirmed.DashRemainingFixed == Fix.Zero
                && !state.IsGrappling && !confirmed.IsGrappling
                && state.GrappleConsumed && confirmed.GrappleConsumed && !movement.HasPresentedGrappleRope)
            {
                canceled = true;
                latched = true;
                phase = 5;
            }
            bool held = phase >= 0 && phase != 2 && phase != 5;
            input.SetAutomationInput(0f, 0f, phase == 1 ? 45f : 0f, phase == 1 ? 0f : -20f,
                jump, dashPressed: dash, grappleHeld: held);
            bool passed = predicted && authority && reeled && swung && airborne && rope && released && reacquired && canceled && latched
                && manager.HasTimeSync;
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
            Debug.Log($"[AFPS_GRAPPLE_RESULT] {(passed ? "PASS" : "FAIL")} phase={phase} predicted={predicted} authority={authority}"
                + $" reeled={reeled} swung={swung} airborne={airborne} rope={rope} released={released} reacquired={reacquired} canceled={canceled} latched={latched}");
            Application.Quit(passed ? 0 : 2);
        }
    }
}

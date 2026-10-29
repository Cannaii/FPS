using System;
using System.IO;
using System.Reflection;
using AFPS.Bootstrap;
using AFPS.Editor.World;
using AFPS.Math;
using AFPS.Simulation.Characters;
using AFPS.Simulation.World;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AFPS.Tests.EditMode
{
    public sealed class TrainingGroundTests
    {
        private Scene scene;
        private SimulationWorldContext context;

        [SetUp]
        public void LoadTrainingWorld()
        {
            scene = EditorSceneManager.OpenScene(TrainingGroundBuilder.ScenePath, OpenSceneMode.Additive);
            foreach (var root in scene.GetRootGameObjects())
            {
                var controller = root.GetComponentInChildren<UnityNetworkGameplayController>();
                if (controller == null)
                {
                    continue;
                }
                Assert.That(controller.TryPrepareSimulation(out var error), Is.True, error);
                context = (SimulationWorldContext)typeof(UnityNetworkGameplayController)
                    .GetField("preparedWorld", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
            }
            Assert.That(context, Is.Not.Null);
        }

        [TearDown]
        public void CloseTrainingWorld()
        {
            if (scene.IsValid())
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void SavedSceneAndCanonicalWorldMatch_WithoutThirdPartyDependencies()
        {
            var rebaked = FixedWorldBakeMenu.BakeSavedScene(TrainingGroundBuilder.ScenePath);
            Assert.That(rebaked.CreateSnapshot(), Is.EqualTo(File.ReadAllBytes(TrainingGroundBuilder.WorldPath)));
            foreach (var path in AssetDatabase.GetDependencies(TrainingGroundBuilder.ScenePath, true))
            {
                Assert.That(path.StartsWith("Assets/ThirdParty/", StringComparison.Ordinal), Is.False, path);
            }
        }

        [Test]
        public void AllEightSpawnSlots_HaveGroundAndStandingRoom()
        {
            for (int i = 0; i < 8; i++)
            {
                var state = State(-8f + i * 2.5f, 0f, -26f);
                for (int tick = 0; tick < 5; tick++)
                {
                    state = Step(state, default);
                }
                Assert.That(state.IsGrounded, Is.True, "spawn " + i);
                Assert.That(state.PositionFixed.Y.ToFloat(), Is.EqualTo(0f).Within(0.02f));
                Assert.That(state.IsCrouched, Is.False);
            }
        }

        [Test]
        public void LowTunnel_BlocksStandingUntilPlayerExits()
        {
            var state = State(-29f, 0f, -18f);
            var move = new PlayerInputCommand { MoveY = 1f, CrouchHeld = true };
            for (int i = 0; i < 90; i++)
            {
                state = Step(state, move);
            }
            Assert.That(state.PositionFixed.Z.ToFloat(), Is.InRange(-17f, -7f));
            state = Step(state, default);
            Assert.That(state.IsCrouched, Is.True, "低顶内松键仍然受头部空间约束");
            move.CrouchHeld = false;
            for (int i = 0; i < 160; i++)
            {
                state = Step(state, move);
            }
            Assert.That(state.PositionFixed.Z.ToFloat(), Is.GreaterThan(-6f));
            Assert.That(state.IsCrouched, Is.False);
        }

        [Test]
        public void LongWall_AllowsWallRunAndWallJump()
        {
            var state = State(25.81f, 1f, -3f);
            state.IsGrounded = false;
            var run = new PlayerInputCommand { MoveY = 1f, SprintHeld = true };
            for (int i = 0; i < 5; i++)
            {
                state = Step(state, run);
            }
            Assert.That(state.IsWallRunning, Is.True);
            // 再靠近到接触皮肤内，确认墙跳使用运动器真实接触，而非墙跑额外Reach。
            state.PositionFixed = new FixVec3(Fix.FromFloat(25.805f), state.PositionFixed.Y, state.PositionFixed.Z);
            state = Step(state, new PlayerInputCommand { JumpPressed = true });
            Assert.That(state.VelocityFixed.X.Raw, Is.GreaterThan(0));
            Assert.That(state.VelocityFixed.Y.Raw, Is.GreaterThan(0));
        }

        [Test]
        public void Ladder_ReachesTopWithoutHittingDeckUnderside()
        {
            var state = State(32f, 0f, 26.4f);
            bool climbed = false;
            float maxHeight = 0f;
            for (int i = 0; i < 140; i++)
            {
                state = Step(state, new PlayerInputCommand { MoveY = 1f });
                climbed |= state.IsClimbing;
                maxHeight = Mathf.Max(maxHeight, state.PositionFixed.Y.ToFloat());
            }
            Assert.That(climbed, Is.True);
            Assert.That(maxHeight, Is.GreaterThanOrEqualTo(3.99f));
            Assert.That(state.IsClimbing, Is.False);
            Assert.That(state.IsGrounded, Is.True);
            Assert.That(state.PositionFixed.Y.ToFloat(), Is.EqualTo(4f).Within(0.02f));
        }

        [Test]
        public void CyanBeam_IsAttachableAndReleasesWithoutRequeryingUnityPhysics()
        {
            var state = State(0f, 0f, 33f);
            state = Step(state, new PlayerInputCommand { LookPitch = -70f, GrappleHeld = true });
            Assert.That(state.IsGrappling, Is.True);
            Assert.That(state.GrapplePointFixed.Y.ToFloat(), Is.InRange(9.6f, 10.4f));
            state = Step(state, default);
            Assert.That(state.IsGrappling, Is.False);
        }

        private static PlayerMovementState State(float x, float y, float z) => new PlayerMovementState
        {
            PositionFixed = new FixVec3(Fix.FromFloat(x), Fix.FromFloat(y), Fix.FromFloat(z)),
            IsGrounded = true
        };

        private PlayerMovementState Step(in PlayerMovementState state, in PlayerInputCommand input) =>
            PlayerMovementSimulation.Simulate(state, input, context.MovementConfiguration, context.TickDeltaTime, context.CollisionWorld);
    }
}

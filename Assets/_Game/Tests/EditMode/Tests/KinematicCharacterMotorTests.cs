using AFPS.Simulation.Characters;
using AFPS.Simulation.Characters.Collision;
using NUnit.Framework;
using UnityEngine;

namespace AFPS.Tests.EditMode
{
    public sealed class KinematicCharacterMotorTests
    {
        [Test]
        public void Move_IntoWall_SlidesAlongWallAndRemovesNormalVelocity()
        {
            PlaneCollisionWorld world = new PlaneCollisionWorld(new CollisionPlane(new Vector3(1f, 0f, 0f), Vector3.left), new CollisionPlane(Vector3.zero, Vector3.up));

            KinematicCharacterMotor.Move(Vector3.zero, new Vector3(2f, 0f, 1f), 1f, TestConfig(), world, out Vector3 position, out Vector3 velocity, out bool grounded);

            Assert.That(position.x, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(position.z, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(velocity.x, Is.Zero.Within(0.0001f));
            Assert.That(velocity.z, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(grounded, Is.True);
        }

        [Test]
        public void Move_IntoCeiling_StopsUpwardMovement()
        {
            PlaneCollisionWorld world = new PlaneCollisionWorld(new CollisionPlane(new Vector3(0f, 2f, 0f), Vector3.down));

            KinematicCharacterMotor.Move(Vector3.zero, new Vector3(0f, 5f, 0f), 1f, TestConfig(), world, out Vector3 position, out Vector3 velocity, out bool grounded);

            Assert.That(position.y, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(velocity.y, Is.Zero.Within(0.0001f));
            Assert.That(grounded, Is.False);
        }

        [Test]
        public void Move_OnWalkableSlope_ProjectsMovementAndRemainsGrounded()
        {
            Vector3 slopeNormal = new Vector3(-0.5f, 1f, 0f).normalized;
            PlaneCollisionWorld world = new PlaneCollisionWorld(new CollisionPlane(Vector3.zero, slopeNormal));

            KinematicCharacterMotor.Move(Vector3.zero, Vector3.right, 1f, TestConfig(), world, out Vector3 position, out _, out bool grounded);

            Assert.That(position.x, Is.EqualTo(0.8f).Within(0.0001f));
            Assert.That(position.y, Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(grounded, Is.True);
        }

        [Test]
        public void Move_IntoTooSteepSlope_TreatsSlopeAsWall()
        {
            Vector3 steepNormal = new Vector3(-1f, 0.2f, 0f).normalized;
            PlaneCollisionWorld world = new PlaneCollisionWorld(new CollisionPlane(Vector3.zero, steepNormal));

            KinematicCharacterMotor.Move(Vector3.zero, Vector3.right, 1f, TestConfig(), world, out Vector3 position, out Vector3 velocity, out bool grounded);

            Assert.That(position.x, Is.Zero.Within(0.0001f));
            Assert.That(position.y, Is.Zero.Within(0.0001f));
            Assert.That(velocity.x, Is.Zero.Within(0.0001f));
            Assert.That(grounded, Is.False);
        }

        [Test]
        public void Move_FallingOntoGround_StopsVerticalVelocityAndBecomesGrounded()
        {
            PlaneCollisionWorld world = new PlaneCollisionWorld(new CollisionPlane(Vector3.zero, Vector3.up));

            KinematicCharacterMotor.Move(Vector3.up, Vector3.down * 2f, 1f, TestConfig(), world, out Vector3 position, out Vector3 velocity, out bool grounded);

            Assert.That(position.y, Is.Zero.Within(0.0001f));
            Assert.That(velocity.y, Is.Zero.Within(0.0001f));
            Assert.That(grounded, Is.True);
        }

        [Test]
        public void Move_AtLowObstacle_UsesStepPath()
        {
            CharacterCollisionConfig config = TestConfig();
            ScriptedStepCollisionWorld world = new ScriptedStepCollisionWorld(config.StepHeight);

            KinematicCharacterMotor.Move(Vector3.zero, Vector3.right, 1f, config, world, out Vector3 position, out _, out bool grounded);

            Assert.That(position.x, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(position.y, Is.EqualTo(config.StepHeight).Within(0.0001f));
            Assert.That(grounded, Is.True);
        }

        [Test]
        public void PlayerSimulation_ReplayAgainstSameCollisionWorldProducesSameState()
        {
            PlaneCollisionWorld world = new PlaneCollisionWorld(new CollisionPlane(Vector3.zero, Vector3.up), new CollisionPlane(new Vector3(0.2f, 0f, 0f), Vector3.left));
            PlayerSimulationConfig config = new PlayerSimulationConfig(6f, 20f, 20f, 8f, TestConfig());
            PlayerState first = new PlayerState { IsGrounded = true };
            PlayerState second = first;

            for (uint tick = 1; tick <= 20; tick++)
            {
                PlayerInputCommand input = new PlayerInputCommand { Tick = tick, MoveX = 1f, MoveY = 1f, JumpPressed = tick == 4 };
                first = PlayerSimulation.Simulate(first, input, config, 0.02f, world);
                second = PlayerSimulation.Simulate(second, input, config, 0.02f, world);
            }

            Assert.That(second.Tick, Is.EqualTo(first.Tick));
            Assert.That(second.Position, Is.EqualTo(first.Position));
            Assert.That(second.Velocity, Is.EqualTo(first.Velocity));
            Assert.That(second.IsGrounded, Is.EqualTo(first.IsGrounded));
        }

        private static CharacterCollisionConfig TestConfig()
        {
            return new CharacterCollisionConfig(0.5f, 2f, 0f, 0.1f, 0.3f, 50f, 4);
        }

        private readonly struct CollisionPlane
        {
            public readonly Vector3 Point;
            public readonly Vector3 Normal;

            public CollisionPlane(Vector3 point, Vector3 normal)
            {
                Point = point;
                Normal = normal.normalized;
            }
        }

        private sealed class PlaneCollisionWorld : ICharacterCollisionWorld
        {
            private readonly CollisionPlane[] planes;

            public PlaneCollisionWorld(params CollisionPlane[] planes)
            {
                this.planes = planes;
            }

            public bool CapsuleCast(Vector3 feetPosition, Vector3 direction, float distance, in CharacterCollisionConfig config, out CharacterCollisionHit hit)
            {
                hit = default;
                float nearestDistance = float.PositiveInfinity;
                Vector3 nearestNormal = default;
                foreach (CollisionPlane plane in planes)
                {
                    float approach = Vector3.Dot(direction, plane.Normal);
                    if (approach >= -0.00001f)
                    {
                        continue;
                    }

                    float signedDistance = Vector3.Dot(feetPosition - plane.Point, plane.Normal);
                    float candidateDistance = Mathf.Max(0f, signedDistance / -approach);
                    if (candidateDistance <= distance && candidateDistance < nearestDistance)
                    {
                        nearestDistance = candidateDistance;
                        nearestNormal = plane.Normal;
                    }
                }

                if (float.IsPositiveInfinity(nearestDistance))
                {
                    return false;
                }

                hit = new CharacterCollisionHit(nearestDistance, nearestNormal);
                return true;
            }
        }

        private sealed class ScriptedStepCollisionWorld : ICharacterCollisionWorld
        {
            private readonly float stepHeight;

            public ScriptedStepCollisionWorld(float stepHeight)
            {
                this.stepHeight = stepHeight;
            }

            public bool CapsuleCast(Vector3 feetPosition, Vector3 direction, float distance, in CharacterCollisionConfig config, out CharacterCollisionHit hit)
            {
                hit = default;
                if (direction.x > 0.9f && feetPosition.y < stepHeight * 0.5f)
                {
                    hit = new CharacterCollisionHit(0f, Vector3.left);
                    return true;
                }

                if (direction.y < -0.9f && feetPosition.y >= stepHeight)
                {
                    hit = new CharacterCollisionHit(0f, Vector3.up);
                    return true;
                }

                return false;
            }
        }
    }
}

using System;
using AFPS.Math;
using AFPS.Core.Collections;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Prediction;
using AFPS.Simulation.Characters;
using AFPS.Simulation.Characters.Collision;
using AFPS.Simulation.World;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class CapsuleWorldTests
    {
        private static Fix F(int value) => Fix.FromInt(value);
        private static FixVec3 V(int x, int y, int z) => new FixVec3(F(x), F(y), F(z));
        private static FixedWorldBox Box(uint id = 1) => new FixedWorldBox(id, 0, FixedSurface.Solid | FixedSurface.Ground, new FixedAabb(V(0, 0, 0), V(2, 2, 2)));
        private static DeterministicHit Cast(FixedWorld world, FixVec3 feet, FixVec3 direction, Fix distance) => world.CapsuleCast(feet, Fix.Half, F(2), direction, distance);
        private static FixedWorld FloorAndWall() =>
            new FixedWorld(new[] { new FixedWorldBox(2, 0, FixedSurface.Solid, new FixedAabb(V(2, 0, -50), V(3, 5, 50))) }, new[] { new FixedWorldPlane(1, 0, FixedSurface.Solid | FixedSurface.Ground, FixedAxis.Y, Fix.Zero) });
        [Test]
        public void FaceCast_UsesPhysicalRadiusAndUnnormalizedDirection()
        {
            var hit = Cast(new FixedWorld(new[] { Box() }), V(-2, 0, 1), V(7, 0, 0), F(4));
            Assert.That(hit.DidHit, Is.True);
            Assert.That(hit.Distance.Raw, Is.EqualTo(98304));
            Assert.That(hit.Normal, Is.EqualTo(-FixVec3.Right));
            Assert.That(hit.Point, Is.EqualTo(new FixVec3(Fix.Zero, Fix.Half, Fix.One)));
        }

        [Test]
        public void RoundedCorner_DoesNotUseAnExpandedBoxApproximation()
        {
            var world = new FixedWorld(new[] { Box() });
            var feet = new FixVec3(F(-2), Fix.Zero, Fix.FromRatio(-2, 5));
            var hit = Cast(world, feet, FixVec3.Right, F(4));
            // z=-0.4,r=0.5：接触 x=-sqrt(0.25-0.16)=-0.3，距离约 1.7。
            Assert.That(hit.DidHit, Is.True);
            Assert.That(hit.Distance.Raw, Is.InRange(111409, 111412));
            Assert.That(hit.Normal.X.Raw, Is.InRange(-39325, -39315));
            Assert.That(hit.Normal.Z.Raw, Is.InRange(-52435, -52420));
            Assert.That(feet.Z.Raw, Is.EqualTo(-26214));
        }

        [Test]
        public void RoundedCorner_ClearanceAvoidsFalseBoxHit()
        {
            var world = new FixedWorld(new[] { Box() });
            // 轨迹只穿过扩展盒的角，始终在真实圆角外。
            var feet = new FixVec3(Fix.FromRatio(-9, 20), Fix.Zero, Fix.FromRatio(-9, 20));
            var hit = Cast(world, feet, new FixVec3(-Fix.One, Fix.Zero, Fix.One), Fix.FromRatio(1, 10));
            Assert.That(hit.DidHit, Is.False);
        }

        [Test]
        public void SphereAtMinimumHeight_HitsThreeDimensionalCorner()
        {
            var world = new FixedWorld(new[] { Box() });
            var hit = world.CapsuleCast(new FixVec3(F(-2), Fix.FromRatio(-5, 2), F(-2)), Fix.Half, Fix.One, V(1, 1, 1), F(5));
            Assert.That(hit.DidHit, Is.True);
            Assert.That(hit.Normal.X.Raw, Is.LessThan(0));
            Assert.That(hit.Normal.Y.Raw, Is.LessThan(0));
            Assert.That(hit.Normal.Z.Raw, Is.LessThan(0));
        }

        [Test]
        public void ThinObstacle_HighSpeedCastDoesNotTunnel()
        {
            var world = new FixedWorld(new[] { new FixedWorldBox(1, 0, FixedSurface.Solid, new FixedAabb(V(10, 0, -2), new FixVec3(Fix.FromRaw(F(10).Raw + 1), F(4), F(2)))) });
            var hit = Cast(world, V(0, 0, 0), FixVec3.Right, F(100));
            Assert.That(hit.DidHit, Is.True);
            Assert.That(hit.Distance.Raw, Is.EqualTo(Fix.FromRatio(19, 2).Raw));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PlaneGroundAndCeiling_RespectCapsuleHeight(bool ceiling)
        {
            var world = new FixedWorld(null, new[] { new FixedWorldPlane(1, 0, FixedSurface.Solid, FixedAxis.Y, ceiling ? F(4) : Fix.Zero, !ceiling) });
            var hit = Cast(world, V(0, 1, 0), ceiling ? FixVec3.Up : -FixVec3.Up, F(2));
            Assert.That(hit.Distance, Is.EqualTo(Fix.One));
            Assert.That(hit.Normal, Is.EqualTo(ceiling ? -FixVec3.Up : FixVec3.Up));
        }

        [Test]
        public void DoubleSidedPlane_DoesNotBlockMovementAwayFromEitherSide()
        {
            var world = new FixedWorld(null, new[] { new FixedWorldPlane(1, 0, FixedSurface.Solid, FixedAxis.X, Fix.Zero, true, true) });
            Assert.That(Cast(world, V(2, 0, 0), FixVec3.Right, F(4)).DidHit, Is.False);
            Assert.That(Cast(world, V(-2, 0, 0), -FixVec3.Right, F(4)).DidHit, Is.False);
            Assert.That(Cast(world, V(2, 0, 0), -FixVec3.Right, F(4)).Distance.Raw, Is.EqualTo(98304));
            Assert.That(Cast(world, V(-2, 0, 0), FixVec3.Right, F(4)).Distance.Raw, Is.EqualTo(98304));
        }

        [Test]
        public void TouchingSurface_OnlyBlocksInwardMotion()
        {
            var world = new FixedWorld(new[] { Box() });
            var feet = new FixVec3(-Fix.Half, Fix.Zero, Fix.One);
            Assert.That(Cast(world, feet, -FixVec3.Right, Fix.One).DidHit, Is.False);
            Assert.That(Cast(world, feet, FixVec3.Forward, Fix.One).DidHit, Is.False);
            var hit = Cast(world, feet, FixVec3.Right, Fix.One);
            Assert.That(hit.DidHit, Is.True);
            Assert.That(hit.Distance.Raw, Is.Zero);
            Assert.That(hit.StartedInside, Is.False);
        }

        [Test]
        public void InitialOverlap_AllowsEscapeAndReportsFurtherPenetration()
        {
            var world = new FixedWorld(new[] { Box() });
            var feet = new FixVec3(Fix.FromRatio(-1, 4), Fix.Zero, Fix.One);
            Assert.That(Cast(world, feet, -FixVec3.Right, Fix.One).DidHit, Is.False);
            var hit = Cast(world, feet, FixVec3.Right, Fix.One);
            Assert.That(hit.DidHit, Is.True);
            Assert.That(hit.StartedInside, Is.True);
            Assert.That(hit.Normal, Is.EqualTo(-FixVec3.Right));
        }

        [Test]
        public void CenterInsideBox_UsesStableEscapeAxis()
        {
            var world = new FixedWorld(new[] { Box() });
            var feet = V(1, 0, 1);
            var hit = Cast(world, feet, FixVec3.Right, Fix.One);
            Assert.That(hit.DidHit, Is.True);
            Assert.That(hit.StartedInside, Is.True);
            Assert.That(hit.Normal, Is.EqualTo(-FixVec3.Right));
            Assert.That(Cast(world, feet, -FixVec3.Right, Fix.One).DidHit, Is.False);
        }

        [Test]
        public void FiltersAndExactEqualTime_UseStableShapeIdentity()
        {
            var a = new FixedWorldBox(20, 3, FixedSurface.Solid, Box().Bounds);
            var b = new FixedWorldBox(4, 3, FixedSurface.Wall, Box().Bounds);
            var world = new FixedWorld(new[] { a, b });
            var feet = V(-2, 0, 1);
            Assert.That(world.CapsuleCast(feet, Fix.Half, F(2), FixVec3.Right, F(4), 1 << 3, FixedSurface.All).ShapeId, Is.EqualTo(4));
            Assert.That(world.CapsuleCast(feet, Fix.Half, F(2), FixVec3.Right, F(4), 1 << 3).ShapeId, Is.EqualTo(20));
            Assert.That(world.CapsuleCast(feet, Fix.Half, F(2), FixVec3.Right, F(4), 1 << 2).DidHit, Is.False);
        }

        [Test]
        public void SubRawDistanceDifference_IsComparedBeforeShapeId()
        {
            var nearer = new FixedWorldBox(20, 0, FixedSurface.Solid, new FixedAabb(V(2, 0, 0), V(3, 2, 2)));
            var farther = new FixedWorldBox(1, 0, FixedSurface.Solid, new FixedAabb(new FixVec3(F(2), Fix.Zero, Fix.FromRaw(1)), new FixVec3(F(3), F(2), F(2))));
            var world = new FixedWorld(new[] { farther, nearer });
            var feet = new FixVec3(Fix.Zero, Fix.Zero, Fix.FromRaw(-6552));
            Assert.That(Cast(new FixedWorld(new[] { nearer }), feet, FixVec3.Right, F(4)).Distance, Is.EqualTo(Cast(new FixedWorld(new[] { farther }), feet, FixVec3.Right, F(4)).Distance));
            var hit = Cast(world, feet, FixVec3.Right, F(4));
            Assert.That(hit.ShapeId, Is.EqualTo(20));
        }

        [Test]
        public void RationalPlaneBeforeIrrationalCorner_WinsEvenWithLargerId()
        {
            var box = new FixedWorldBox(1, 0, FixedSurface.Solid, new FixedAabb(V(2, 0, 0), V(3, 2, 2)));
            var plane = new FixedWorldPlane(20, 0, FixedSurface.Solid, FixedAxis.X, Fix.FromRaw(144178), false);
            var feet = new FixVec3(Fix.Zero, Fix.Zero, Fix.FromRatio(-2, 5));
            var boxHit = Cast(new FixedWorld(new[] { box }), feet, FixVec3.Right, F(4));
            var planeHit = Cast(new FixedWorld(null, new[] { plane }), feet, FixVec3.Right, F(4));
            Assert.That(boxHit.Distance, Is.EqualTo(planeHit.Distance));
            Assert.That(Cast(new FixedWorld(new[] { box }, new[] { plane }), feet, FixVec3.Right, F(4)).ShapeId, Is.EqualTo(20));
        }

        [Test]
        public void RandomSweeps_MatchIndependentConvexDistanceOracle()
        {
            var world = new FixedWorld(new[] { Box() });
            var random = new FixRandom(20261023);
            for (int i = 0; i < 2000; i++)
            {
                var feet = new FixVec3(
                    Fix.FromRaw(random.NextInt(-4 * 65536, 4 * 65536)),
                    Fix.FromRaw(random.NextInt(-4 * 65536, 4 * 65536)),
                    Fix.FromRaw(random.NextInt(-4 * 65536, 4 * 65536)));
                var direction = new FixVec3(Fix.FromRaw(random.NextInt(-65536, 65537)), Fix.FromRaw(random.NextInt(-65536, 65537)), Fix.FromRaw(random.NextInt(-65536, 65537)));
                // 参考求解使用凸距离函数的导数二分找最近点，再二分首次接触；不复用 26 特征求根。
                double expected;
                Fix radius = Fix.FromRaw(random.NextInt(1000, 80001));
                Fix height = Fix.FromRaw(radius.Raw * 2 + random.NextInt(0, 300001));
                expected = Oracle(feet, direction.Normalized(), 8, radius.ToDouble(), height.ToDouble());
                var hit = world.CapsuleCast(feet, radius, height, direction, F(8));
                Assert.That(hit.DidHit, Is.EqualTo(!double.IsNaN(expected)), "sample=" + i);
                if (hit.DidHit && expected > 0)
                {
                    Assert.That(System.Math.Abs(hit.Distance.Raw - expected * 65536), Is.LessThan(1.05), "sample=" + i);
                }
            }
        }

        private static double Oracle(FixVec3 feet, FixVec3 direction, double maximum, double radius = 0.5, double height = 2)
        {
            double ox = feet.X.ToDouble(),
                oy = feet.Y.ToDouble() + radius,
                oz = feet.Z.ToDouble();
            double bottom = -(height - 2 * radius);
            double vx = direction.X.ToDouble(),
                vy = direction.Y.ToDouble(),
                vz = direction.Z.ToDouble();
            double Value(double t, bool derivative)
            {
                double x = ox + vx * t,
                    y = oy + vy * t,
                    z = oz + vz * t;
                double dx = x - System.Math.Max(0, System.Math.Min(2, x));
                double dy = y - System.Math.Max(bottom, System.Math.Min(2, y));
                double dz = z - System.Math.Max(0, System.Math.Min(2, z));
                return derivative ? dx * vx + dy * vy + dz * vz : dx * dx + dy * dy + dz * dz - radius * radius;
            }

            // 随机样本中的初始重叠只用于验证是否可继续深入；内部中心零距离的脱离轴另有定向测试。
            if (Value(0, false) <= 0)
            {
                double dot = Value(0, true);
                if (dot != 0)
                {
                    return dot < 0 ? 0 : double.NaN;
                }

                double[] gaps =
                {
                    ox,
                    2 - ox,
                    oy - bottom,
                    2 - oy,
                    oz,
                    2 - oz
                };
                double[] velocities =
                {
                    -vx,
                    vx,
                    -vy,
                    vy,
                    -vz,
                    vz
                };
                int best = 0;
                for (int a = 1; a < 6; a++)
                {
                    if (gaps[a] < gaps[best])
                    {
                        best = a;
                    }
                }

                return velocities[best] < 0 ? 0 : double.NaN;
            }

            double low = 0,
                high = maximum;
            if (Value(0, true) >= 0)
            {
                return double.NaN;
            }

            if (Value(high, true) > 0)
            {
                for (int n = 0; n < 80; n++)
                {
                    double mid = (low + high) / 2;
                    if (Value(mid, true) < 0)
                    {
                        low = mid;
                    }
                    else
                    {
                        high = mid;
                    }
                }
            }
            else
            {
                low = high;
            }

            double minimum = (low + high) / 2;
            if (Value(minimum, false) > 0)
            {
                return double.NaN;
            }

            low = 0;
            high = minimum;
            for (int n = 0; n < 80; n++)
            {
                double mid = (low + high) / 2;
                if (Value(mid, false) > 0)
                {
                    low = mid;
                }
                else
                {
                    high = mid;
                }
            }

            return (low + high) / 2;
        }

        [Test]
        public void WorldExtremes_DoNotOverflowWideProducts()
        {
            var bounds = new FixedAabb(new FixVec3(Fix.FromRaw(int.MaxValue - 100000), F(-1), F(-1)), new FixVec3(Fix.MaxValue, F(3), F(1)));
            var world = new FixedWorld(new[] { new FixedWorldBox(1, 0, FixedSurface.Solid, bounds) });
            var feet = new FixVec3(Fix.MinValue, Fix.Zero, Fix.Zero);
            Assert.That(Cast(world, feet, FixVec3.Right, Fix.MaxValue).DidHit, Is.False);
            feet = new FixVec3(Fix.FromRaw(int.MaxValue - 200000), Fix.Zero, Fix.Zero);
            var hit = Cast(world, feet, FixVec3.Right, F(4));
            Assert.That(hit.Distance.Raw, Is.EqualTo(67232));
        }

        [Test]
        public void MaximumHeightCapsuleNearWorldBoundary_HasNoIntermediateOverflow()
        {
            var world = new FixedWorld(new[] { Box() });
            var feet = new FixVec3(Fix.One, Fix.MinValue, Fix.One);
            var hit = world.CapsuleCast(feet, Fix.Half, Fix.MaxValue, FixVec3.Up, Fix.One);
            Assert.That(hit.DidHit, Is.True);
            Assert.That(hit.Distance.Raw, Is.EqualTo(1));
            Assert.That(hit.Normal, Is.EqualTo(-FixVec3.Up));
        }

        [Test]
        public void RandomMultipleBoxes_SelectExactEarliestRootRegardlessOfOrder()
        {
            var boxes = new[]
            {
                new FixedWorldBox(30, 0, FixedSurface.Solid, new FixedAabb(V(-1, 0, 0), V(1, 2, 2))),
                new FixedWorldBox(2, 0, FixedSurface.Solid, Box().Bounds),
                new FixedWorldBox(10, 0, FixedSurface.Solid, new FixedAabb(V(1, 0, 0), V(3, 2, 2)))
            };
            var world = new FixedWorld(boxes);
            var random = new FixRandom(20261024);
            for (int sample = 0; sample < 1000; sample++)
            {
                var feet = new FixVec3(
                    Fix.FromRaw(random.NextInt(-4 * 65536, 4 * 65536)),
                    Fix.FromRaw(random.NextInt(-3 * 65536, 4 * 65536)),
                    Fix.FromRaw(random.NextInt(-3 * 65536, 4 * 65536)));
                var direction = new FixVec3(Fix.FromRaw(random.NextInt(-65536, 65537)), Fix.FromRaw(random.NextInt(-65536, 65537)), Fix.FromRaw(random.NextInt(-65536, 65537)));
                double best = double.PositiveInfinity;
                uint id = 0;
                for (int i = 0; i < 3; i++)
                {
                    var local = feet - new FixVec3(F(i - 1), Fix.Zero, Fix.Zero);
                    double expected = Oracle(local, direction.Normalized(), 8);
                    if (double.IsNaN(expected))
                    {
                        continue;
                    }

                    if (expected < best || (expected == best && boxes[i].ShapeId < id))
                    {
                        best = expected;
                        id = boxes[i].ShapeId;
                    }
                }

                var hit = Cast(world, feet, direction, F(8));
                Assert.That(hit.DidHit, Is.EqualTo(id != 0), "sample=" + sample);
                if (hit.DidHit)
                {
                    Assert.That(hit.ShapeId, Is.EqualTo(id), "sample=" + sample);
                }
            }
        }

        [Test]
        public void InvalidDimensionsAndDistance_AreRejected()
        {
            var world = new FixedWorld(null);
            Assert.Throws<ArgumentOutOfRangeException>(() => world.CapsuleCast(FixVec3.Zero, Fix.Zero, Fix.One, FixVec3.Right, Fix.One));
            Assert.Throws<ArgumentOutOfRangeException>(() => world.CapsuleCast(FixVec3.Zero, Fix.One, Fix.One, FixVec3.Right, Fix.One));
            Assert.Throws<ArgumentOutOfRangeException>(() => Cast(world, FixVec3.Zero, FixVec3.Right, -Fix.One));
            Assert.That(Cast(world, FixVec3.Zero, FixVec3.Zero, Fix.One).DidHit, Is.False);
        }

        [Test]
        public void Motor_FixedWorldSlidesAgainstWallAndStopsAtCeiling()
        {
            var backend = new FixedCharacterCollisionWorld(FloorAndWall());
            var config = new CharacterCollisionConfig(Fix.Half, F(2), Fix.Zero, Fix.FromRatio(1, 10), Fix.FromRatio(3, 10), F(50), 4);
            KinematicCharacterMotor.Move(FixVec3.Zero, V(3, 0, 1), Fix.One, config, backend, out var position, out var velocity, out var grounded);
            Assert.That(position.X.Raw, Is.InRange(98300, 98305));
            Assert.That(position.Z.Raw, Is.InRange(65530, 65540));
            Assert.That(velocity.X.Raw, Is.Zero);
            Assert.That(grounded, Is.True);
            var ceiling = new FixedCharacterCollisionWorld(new FixedWorld(new[] { new FixedWorldBox(1, 0, FixedSurface.Solid, new FixedAabb(V(-5, 3, -5), V(5, 4, 5))) }));
            KinematicCharacterMotor.Move(FixVec3.Zero, V(0, 5, 0), Fix.One, config, ceiling, out position, out velocity, out grounded);
            Assert.That(position.Y, Is.EqualTo(Fix.One));
            Assert.That(velocity.Y.Raw, Is.Zero);
            Assert.That(grounded, Is.False);
        }

        [Test]
        public void Motor_StepsOntoLowBoxUsingCapsuleQueries()
        {
            var height = Fix.FromRatio(1, 5);
            var box = new FixedWorldBox(2, 0, FixedSurface.Solid | FixedSurface.Ground, new FixedAabb(new FixVec3(F(1), Fix.Zero, F(-2)), new FixVec3(F(3), height, F(2))));
            var world = new FixedWorld(new[] { box }, new[] { new FixedWorldPlane(1, 0, FixedSurface.Solid | FixedSurface.Ground, FixedAxis.Y, Fix.Zero) });
            var backend = new FixedCharacterCollisionWorld(world);
            KinematicCharacterMotor.Move(FixVec3.Zero, V(2, 0, 0), Fix.One, CharacterCollisionConfig.Default, backend, out var position, out _, out var grounded);
            Assert.That(position.X.Raw, Is.InRange(F(2).Raw - 3, F(2).Raw + 3));
            Assert.That(position.Y.Raw, Is.InRange(height.Raw - 2, height.Raw + 2));
            Assert.That(grounded, Is.True);
        }

        [Test]
        public void PredictionAuthorityAndRollback_ShareIdenticalFixedGeometry()
        {
            var backend = new FixedCharacterCollisionWorld(FloorAndWall());
            var config = new PlayerMovementConfig(F(6), F(20), F(20), F(8));
            var inputs = new TickBuffer<PlayerInputCommand>(256);
            var states = new TickBuffer<PlayerMovementState>(256);
            var state = new PlayerMovementState
            {
                IsGrounded = true
            };
            PlayerMovementState authoritative = default;
            for (uint tick = 1; tick <= 100; tick++)
            {
                var input = new PlayerInputCommand
                {
                    Tick = tick
                };
                input.SetFixedControls(Fix.One, Fix.One, Fix.Zero, Fix.Zero);
                inputs.Store(tick, input);
                state = PlayerMovementSimulation.Simulate(state, input, config, SimulationStep.Step50Hz, backend);
                states.Store(tick, state);
                if (tick == 15)
                {
                    authoritative = state;
                }
            }

            var corrupt = authoritative;
            corrupt.PositionFixed += new FixVec3(Fix.Epsilon, Fix.Zero, Fix.Zero);
            states.Store(15, corrupt);
            var result = MovementPredictionReconciler.Reconcile(new AuthoritativeMovementState(120, 15, authoritative), 100, state, inputs, states, config, SimulationStep.Step50Hz, backend);
            Assert.That(result.Status, Is.EqualTo(MovementReconciliationStatus.Corrected));
            Assert.That(result.ReplayedTickCount, Is.EqualTo(85));
            Assert.That(result.State.RawEquals(state), Is.True);
            Assert.That(state.PositionFixed.X.Raw, Is.InRange(98300, 98305));
        }
    }
}

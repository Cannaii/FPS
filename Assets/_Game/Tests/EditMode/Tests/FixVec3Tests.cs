using System;
using AFPS.Math;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class FixVec3Tests
    {
        private static FixVec3 V(int x, int y, int z) => new FixVec3(Fix.FromInt(x), Fix.FromInt(y), Fix.FromInt(z));
        [Test]
        public void ArithmeticAndProducts_HaveGoldenResults()
        {
            Assert.That(V(1, 2, 3) + V(3, 2, 1), Is.EqualTo(V(4, 4, 4)));
            Assert.That(V(1, 2, 3) - V(3, 2, 1), Is.EqualTo(V(-2, 0, 2)));
            Assert.That(-V(1, 2, 3), Is.EqualTo(V(-1, -2, -3)));
            Assert.That(V(1, 2, 3) * Fix.FromInt(2), Is.EqualTo(V(2, 4, 6)));
            Assert.That(Fix.FromInt(2) * V(1, 2, 3), Is.EqualTo(V(2, 4, 6)));
            Assert.That(V(2, 4, 6) / Fix.FromInt(2), Is.EqualTo(V(1, 2, 3)));
            Assert.That(FixVec3.Dot(V(1, 2, 3), V(3, 2, 1)).Raw, Is.EqualTo(655360));
            Assert.That(FixVec3.Cross(FixVec3.Right, FixVec3.Up), Is.EqualTo(FixVec3.Forward));
            Assert.That(FixVec3.Cross(FixVec3.Up, FixVec3.Right), Is.EqualTo(-FixVec3.Forward));
            Assert.That(V(3, 4, 0).SqrMagnitude.Raw, Is.EqualTo(1638400));
        }
        [Test]
        public void MagnitudeAndNormalization_KeepTinyAndLargeVectors()
        {
            Assert.That(V(3, 4, 0).Magnitude.Raw, Is.EqualTo(327680));
            Assert.That(V(1000, 1000, 1000).Magnitude.Raw, Is.EqualTo(113511681));
            Assert.That(V(3, 4, 0).Normalized().X.Raw, Is.EqualTo(39321));
            Assert.That(V(3, 4, 0).Normalized().Y.Raw, Is.EqualTo(52428));
            Assert.That(FixVec3.Normalize(FixVec3.Zero), Is.EqualTo(FixVec3.Zero));
            Assert.That(new FixVec3(Fix.Epsilon, Fix.Zero, Fix.Zero).Normalized(), Is.EqualTo(FixVec3.Right));
            Assert.That(new FixVec3(Fix.MaxValue, Fix.MaxValue, Fix.MaxValue).Normalized().X.Raw, Is.EqualTo(37837));
            Assert.Throws<OverflowException>(() => { var result = V(1000, 0, 0).SqrMagnitude; });
        }
        [Test]
        public void MovementProjectionReflectionAndClamp_RespectLimits()
        {
            Assert.That(FixVec3.MoveTowards(FixVec3.Zero, V(3, 4, 0), Fix.FromInt(5)), Is.EqualTo(V(3, 4, 0)));
            Assert.That(FixVec3.MoveTowards(FixVec3.Zero, V(3, 4, 0), Fix.One).X.Raw, Is.EqualTo(39321));
            Assert.That(FixVec3.ClampMagnitude(V(3, 4, 0), Fix.FromInt(10)), Is.EqualTo(V(3, 4, 0)));
            Assert.That(FixVec3.ClampMagnitude(V(300, 400, 0), Fix.FromInt(5)), Is.EqualTo(V(3, 4, 0)));
            Assert.That(FixVec3.ClampMagnitude(FixVec3.Zero, Fix.Zero), Is.EqualTo(FixVec3.Zero));
            Assert.That(FixVec3.ProjectOnPlane(V(1, 2, 3), V(0, 2, 0)), Is.EqualTo(V(1, 0, 3)));
            Assert.That(FixVec3.ProjectOnPlane(V(1, 2, 3), FixVec3.Zero), Is.EqualTo(V(1, 2, 3)));
            Assert.That(FixVec3.Reflect(V(1, -2, 3), FixVec3.Up), Is.EqualTo(V(1, 2, 3)));
            Assert.Throws<ArgumentOutOfRangeException>(() => FixVec3.ClampMagnitude(FixVec3.One, -Fix.One));
            Assert.Throws<ArgumentOutOfRangeException>(() => FixVec3.MoveTowards(FixVec3.Zero, FixVec3.One, -Fix.One));
        }
    }
}

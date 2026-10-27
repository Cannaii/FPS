using AFPS.Math;
using AFPS.Presentation.Characters;
using AFPS.Simulation.Characters;
using NUnit.Framework;
using UnityEngine;

namespace AFPS.Tests.EditMode
{
    public sealed class GrappleRopeViewTests
    {
        [Test]
        public void Rope_ConsumesAnchorAndVisualFeetWithoutChangingSimulation()
        {
            var target = new GameObject("Grapple rope test");
            try
            {
                var view = target.AddComponent<GrappleRopeView>();
                var config = new PlayerSimulationConfig(6f, 30f, 20f, 8f);
                var state = new PlayerState
                {
                    IsGrappling = true, GrappleConsumed = true, GrappleShapeId = 1,
                    GrapplePointFixed = FixVec3.Forward * Fix.FromInt(10), GrappleLengthFixed = Fix.FromInt(10)
                };
                var original = state;
                view.Render(state, new Vector3(1f, 0f, 0f), config);
                var line = target.GetComponentInChildren<LineRenderer>();
                Assert.That(view.IsVisible, Is.True);
                Assert.That(line.GetPosition(0).x, Is.EqualTo(1.15f).Within(0.00001f));
                Assert.That(line.GetPosition(1), Is.EqualTo(new Vector3(0f, 0f, 10f)));
                Assert.That(state.RawEquals(original), Is.True);
                view.Render(state, Vector3.zero, config, false);
                Assert.That(view.IsVisible, Is.False);
                state.IsGrappling = false;
                view.Render(state, Vector3.zero, config);
                Assert.That(view.IsVisible, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }
    }
}

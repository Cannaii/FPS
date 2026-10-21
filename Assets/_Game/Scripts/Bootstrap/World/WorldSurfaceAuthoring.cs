using AFPS.Simulation.World;
using UnityEngine;

namespace AFPS.Bootstrap.World
{
    [DisallowMultipleComponent]
    public sealed class WorldSurfaceAuthoring : MonoBehaviour
    {
        [SerializeField]
        private FixedSurface surfaces = FixedSurface.Solid | FixedSurface.Ground | FixedSurface.Wall | FixedSurface.Grapple;
        public FixedSurface Surfaces { get => surfaces; set => surfaces = value; }
    }
}
